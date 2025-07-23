import { useEffect, useRef, useState } from 'react';

export interface FrequencyBands {
  bass: number; // 20-250 Hz
  mid: number; // 250-4000 Hz
  treble: number; // 4000-20000 Hz
}

export interface Transients {
  bass: boolean;
  mid: boolean;
  treble: boolean;
}

export interface AudioData {
  frequencies: Uint8Array;
  waveform: Uint8Array;
  volume: number;
  bands: FrequencyBands; // Raw energy values
  dynamicBands: FrequencyBands; // Normalized values (0-1) based on recent history
  transients: Transients;
  energy: number;
  dropIntensity: number; // 0-1 value representing the power of a recent drop, decays over time
  // Legacy compatibility
  bass: number;
  mids: number;
  treble: number;
  beat: boolean; // Deprecated but kept for compatibility
  smoothedVolume: number; // Deprecated
}

// --- Configuration for Dynamic Normalization ---
const ENVELOPE_CONFIG = {
  minDecay: 0.005, // How fast the min value creeps up
  maxDecay: 0.002, // How fast the max value creeps down
  minThreshold: 0.01, // Prevent min from being absolute zero
};

const DROP_CONFIG = {
  decay: 0.97, // How fast the drop intensity fades (lower is faster)
  threshold: 0.4, // Minimum surge in normalized energy to trigger a drop
};

export function useAudioAnalyzer(audioSource?: HTMLAudioElement) {
  const [audioData, setAudioData] = useState<AudioData>({
    frequencies: new Uint8Array(512),
    waveform: new Uint8Array(512),
    volume: 0,
    bands: { bass: 0, mid: 0, treble: 0 },
    dynamicBands: { bass: 0, mid: 0, treble: 0 },
    transients: { bass: false, mid: false, treble: false },
    energy: 0,
    dropIntensity: 0,
    bass: 0,
    mids: 0,
    treble: 0,
    beat: false,
    smoothedVolume: 0,
  });

  const analyserRef = useRef<AnalyserNode | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const animationRef = useRef<number>(0);
  const sourceNodeRef = useRef<MediaElementAudioSourceNode | null>(null);

  // --- Refs for analysis calculations ---
  const prevBandsRef = useRef<FrequencyBands>({ bass: 0, mid: 0, treble: 0 });
  const bandEnvelopeRef = useRef({
    bass: { min: 0.1, max: 0.2 },
    mid: { min: 0.1, max: 0.2 },
    treble: { min: 0.1, max: 0.2 },
  });
  const energyEnvelopeRef = useRef({ min: 0.1, max: 0.2 });
  const prevNormalizedEnergyRef = useRef(0);
  const dropIntensityRef = useRef(0);

  // --- Analysis Helper Functions ---

  const calculateBands = (frequencies: Uint8Array, sampleRate: number): FrequencyBands => {
    const nyquist = sampleRate / 2;
    const binSize = nyquist / frequencies.length;
    const bassEnd = Math.floor(250 / binSize);
    const midEnd = Math.floor(4000 / binSize);
    let bass = 0, mid = 0, treble = 0;
    let bassCount = 0, midCount = 0, trebleCount = 0;
    for (let i = 1; i < frequencies.length; i++) {
      const freq = frequencies[i] / 255;
      if (i <= bassEnd) { bass += freq; bassCount++; }
      else if (i <= midEnd) { mid += freq; midCount++; }
      else { treble += freq; trebleCount++; }
    }
    return {
      bass: bassCount > 0 ? bass / bassCount : 0,
      mid: midCount > 0 ? mid / midCount : 0,
      treble: trebleCount > 0 ? treble / trebleCount : 0,
    };
  };

  const calculateDynamicValue = (value: number, envelope: { min: number; max: number }): number => {
    envelope.max = Math.max(value, envelope.max * (1 - ENVELOPE_CONFIG.maxDecay));
    envelope.min = Math.min(value, envelope.min * (1 + ENVELOPE_CONFIG.minDecay) + ENVELOPE_CONFIG.minThreshold * 0.1);
    const range = Math.max(0.01, envelope.max - envelope.min);
    return Math.max(0, Math.min(1, (value - envelope.min) / range));
  };

  const detectTransients = (currentBands: FrequencyBands): Transients => {
    const prevBands = prevBandsRef.current;
    const transients: Transients = { bass: false, mid: false, treble: false };
    const transientThresholds = { bass: 0.08, mid: 0.06, treble: 0.05 };
    const transientMultipliers = { bass: 1.6, mid: 1.8, treble: 2.0 };
    if (currentBands.bass > prevBands.bass * transientMultipliers.bass && currentBands.bass > transientThresholds.bass) transients.bass = true;
    if (currentBands.mid > prevBands.mid * transientMultipliers.mid && currentBands.mid > transientThresholds.mid) transients.mid = true;
    if (currentBands.treble > prevBands.treble * transientMultipliers.treble && currentBands.treble > transientThresholds.treble) transients.treble = true;
    prevBandsRef.current = currentBands;
    return transients;
  };

  const detectDrop = (normalizedEnergy: number): number => {
    const surge = normalizedEnergy - prevNormalizedEnergyRef.current;
    if (surge > DROP_CONFIG.threshold) {
      dropIntensityRef.current = Math.max(dropIntensityRef.current, surge);
    }
    prevNormalizedEnergyRef.current = normalizedEnergy;
    dropIntensityRef.current *= DROP_CONFIG.decay;
    return dropIntensityRef.current;
  };

  useEffect(() => {
    if (!audioSource) return;

    try {
      audioContextRef.current = new AudioContext();
      analyserRef.current = audioContextRef.current.createAnalyser();
    } catch (error) { console.error('Failed to initialize AudioContext:', error); return; }

    analyserRef.current.fftSize = 1024;
    analyserRef.current.smoothingTimeConstant = 0.3;
    const bufferLength = analyserRef.current.frequencyBinCount;

    try {
      sourceNodeRef.current = audioContextRef.current.createMediaElementSource(audioSource);
      sourceNodeRef.current.connect(analyserRef.current);
      analyserRef.current.connect(audioContextRef.current.destination);
    } catch (error) { console.error('Failed to connect audio source:', error); return; }

    const frequencies = new Uint8Array(bufferLength);
    const waveform = new Uint8Array(bufferLength);

    const analyze = () => {
      if (!analyserRef.current || !audioContextRef.current) return;

      analyserRef.current.getByteFrequencyData(frequencies);
      analyserRef.current.getByteTimeDomainData(waveform);

      const volume = frequencies.reduce((sum, freq) => sum + freq, 0) / frequencies.length / 255;
      const energy = frequencies.reduce((sum, freq) => sum + (freq / 255) ** 2, 0) / frequencies.length;
      const bands = calculateBands(frequencies, audioContextRef.current.sampleRate);
      
      const dynamicBands = {
        bass: calculateDynamicValue(bands.bass, bandEnvelopeRef.current.bass),
        mid: calculateDynamicValue(bands.mid, bandEnvelopeRef.current.mid),
        treble: calculateDynamicValue(bands.treble, bandEnvelopeRef.current.treble),
      };
      const normalizedEnergy = calculateDynamicValue(energy, energyEnvelopeRef.current);
      const dropIntensity = detectDrop(normalizedEnergy);
      const transients = detectTransients(bands);

      setAudioData({
        frequencies: frequencies.slice(),
        waveform: waveform.slice(),
        volume,
        energy,
        bands,
        dynamicBands,
        transients,
        dropIntensity,
        bass: dynamicBands.bass,
        mids: dynamicBands.mid,
        treble: dynamicBands.treble,
        beat: false,
        smoothedVolume: 0,
      });

      animationRef.current = requestAnimationFrame(analyze);
    };

    analyze();

    return () => {
      if (animationRef.current) cancelAnimationFrame(animationRef.current);
      if (sourceNodeRef.current) sourceNodeRef.current.disconnect();
      if (analyserRef.current) analyserRef.current.disconnect();
      if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
        audioContextRef.current.close();
      }
    };
  }, [audioSource]);

  return audioData;
}
