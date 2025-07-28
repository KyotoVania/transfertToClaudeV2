import { useEffect, useRef, useState } from 'react';
import { BPMDetector } from '../utils/BPMDetector';
import { YINPitchDetector } from '../utils/YINPitchDetector';
import { TimbreAnalyzer, type TimbreProfile, type MusicalContext } from '../utils/timbreAnalyzer';
import { createMelFilterbank, calculateRobustODF, calculateMedian } from '../utils/melFilterbank';

// --- Type Definitions ---
export interface FrequencyBands {
  bass: number; // 20-250 Hz
  mid: number; // 250-4000 Hz
  treble: number; // 4000-20000 Hz
}

export interface Transients {
  bass: boolean;
  mid: boolean;
  treble: boolean;
  overall: boolean;
}

export interface SpectralFeatures {
  centroid: number; // Brightness indicator (0-1)
  spread: number; // Spectral width (0-1)
  flux: number; // Spectral change rate (0-1)
  rolloff: number; // Frequency below which 85% of energy is contained (0-1)
}

export interface MelodicFeatures {
  dominantFrequency: number; // Hz
  dominantNote: string; // Musical note (e.g., "A4", "C#5")
  noteConfidence: number; // 0-1
  harmonicContent: number; // 0-1, measure of harmonic richness
  pitchClass: number[]; // 12-element chroma vector
}

export interface RhythmicFeatures {
  bpm: number;
  bpmConfidence: number; // 0-100
  beatPhase: number; // 0-1, position within current beat
  subdivision: number; // 1, 2, 4, 8 etc - detected rhythmic subdivision
  groove: number; // 0-100, measure of rhythmic stability
}

export interface AudioData {
  frequencies: Uint8Array;
  waveform: Uint8Array;
  volume: number;
  bands: FrequencyBands;
  dynamicBands: FrequencyBands;
  transients: Transients;
  energy: number;
  dropIntensity: number;
  spectralFeatures: SpectralFeatures;
  melodicFeatures: MelodicFeatures;
  rhythmicFeatures: RhythmicFeatures;
  timbreProfile: TimbreProfile;
  musicalContext: MusicalContext;
  bass: number;
  mids: number;
  treble: number;
  beat: boolean;
  smoothedVolume: number;
}

// --- Configuration ---
const ENVELOPE_CONFIG = {
  minDecay: 0.002,
  maxDecay: 0.001,
  minThreshold: 0.02,
  adaptiveRate: 0.1,
};

const DROP_CONFIG = {
  decay: 0.95,
  threshold: 0.15, // FIXED: Réduit de 0.5 à 0.15 pour détecter des changements plus subtils
  cooldown: 500,
};

const TRANSIENT_CONFIG = {
  bass: { threshold: 0.06, multiplier: 1.6, decay: 0.85 },     // FIXED: Réduit de 0.08 à 0.06
  mid: { threshold: 0.05, multiplier: 1.8, decay: 0.9 },      // FIXED: Réduit de 0.07 à 0.05
  treble: { threshold: 0.04, multiplier: 2.0, decay: 0.92 },  // FIXED: Réduit de 0.06 à 0.04
  overall: { threshold: 0.07, multiplier: 1.5, decay: 0.88 }, // FIXED: Réduit de 0.12 à 0.07, multiplier de 1.7 à 1.5
};

// Musical note frequencies (A4 = 440Hz)
const NOTE_NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
const A4_FREQ = 440;
const A4_MIDI = 69;

// Perceptual weighting curve (A-weighting approximation)
const A_WEIGHTING = (freq: number): number => {
  const f2 = freq * freq;
  const f4 = f2 * f2;
  return (12194 * 12194 * f4) /
      ((f2 + 20.6 * 20.6) * Math.sqrt((f2 + 107.7 * 107.7) * (f2 + 737.9 * 737.9)) * (f2 + 12194 * 12194));
};

// Convert frequency to musical note
const frequencyToNote = (freq: number): { note: string; cents: number } => {
  if (freq <= 0) return { note: 'N/A', cents: 0 };

  const midiNumber = 12 * Math.log2(freq / A4_FREQ) + A4_MIDI;
  const roundedMidi = Math.round(midiNumber);
  const cents = (midiNumber - roundedMidi) * 100;

  const octave = Math.floor(roundedMidi / 12) - 1;
  const noteIndex = roundedMidi % 12;

  return {
    note: `${NOTE_NAMES[noteIndex]}${octave}`,
    cents: Math.round(cents)
  };
};

export function useAudioAnalyzer(audioSource?: HTMLAudioElement) {
  const [audioData, setAudioData] = useState<AudioData>({
    frequencies: new Uint8Array(512),
    waveform: new Uint8Array(512),
    volume: 0,
    bands: { bass: 0, mid: 0, treble: 0 },
    dynamicBands: { bass: 0, mid: 0, treble: 0 },
    transients: { bass: false, mid: false, treble: false, overall: false },
    energy: 0,
    dropIntensity: 0,
    spectralFeatures: { centroid: 0, spread: 0, flux: 0, rolloff: 0 },
    melodicFeatures: {
      dominantFrequency: 0,
      dominantNote: 'N/A',
      noteConfidence: 0,
      harmonicContent: 0,
      pitchClass: new Array(12).fill(0)
    },
    rhythmicFeatures: {
      bpm: 0,
      bpmConfidence: 0,
      beatPhase: 0,
      subdivision: 1,
      groove: 0
    },
    // NEW: Initialiser des valeurs par défaut pour le profil timbral et le contexte musical
    timbreProfile: {
      brightness: 0,
      warmth: 0,
      richness: 0,
      clarity: 0,
      attack: 0,
      dominantChroma: 0,
      harmonicComplexity: 0
    },
    musicalContext: {
      notePresent: false,
      noteStability: 0,
      key: 'C',
      mode: 'unknown',
      tension: 0
    },
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

  // --- Analysis state refs ---
  const prevBandsRef = useRef<FrequencyBands>({ bass: 0, mid: 0, treble: 0 });
  const prevFrequenciesRef = useRef<Float32Array>(new Float32Array(512));
  const transientStateRef = useRef({
    bass: { value: 0, history: new Array(10).fill(0) },
    mid: { value: 0, history: new Array(10).fill(0) },
    treble: { value: 0, history: new Array(10).fill(0) },
    overall: { value: 0, history: new Array(10).fill(0) },
  });
  const bandEnvelopeRef = useRef({
    bass: { min: 0.1, max: 0.2 },
    mid: { min: 0.1, max: 0.2 },
    treble: { min: 0.1, max: 0.2 },
  });
  const energyEnvelopeRef = useRef({ min: 0.1, max: 0.2 });
  const prevNormalizedEnergyRef = useRef(0);
  const dropIntensityRef = useRef(0);
  const lastDropTimeRef = useRef(0);

  // NOUVEAU: Historique d'énergie pour la détection adaptative
  const energyHistoryRef = useRef<number[]>(new Array(20).fill(0.1));

  // NEW: YIN Pitch Detector for superior fundamental frequency detection
  const yinDetectorRef = useRef<YINPitchDetector | null>(null);

  // NEW: Timbre Analyzer for advanced musical analysis
  const timbreAnalyzerRef = useRef<TimbreAnalyzer | null>(null);

  // NOUVEAU: Refs pour le BPM Detector basé sur l'autocorrélation
  const bpmDetectorRef = useRef(new BPMDetector());
  const odfHistoryRef = useRef<number[]>([]); // Historique de l'ODF pour l'ACF
  const lastBeatTimeRef = useRef(0);
  const ODF_SAMPLE_RATE = 43; // Réduit de 45 à 43 Hz pour exactement 256 samples = 5.95 secondes
  const ODF_HISTORY_SIZE = 256; // Environ 5.6 secondes d'historique

  // FIXED: Store real sample rate from AudioContext
  const realSampleRateRef = useRef<number>(44100); // Default fallback

  // NEW: Chromagram smoothing
  const chromaSmoothingRef = useRef<number[]>(new Array(12).fill(0));
  const CHROMA_SMOOTHING = 0.85; // Smoothing factor

  // NEW: Mel filterbank for robust ODF
  const melFilterbankRef = useRef<number[][] | null>(null);
  const prevMelEnergiesRef = useRef<Float32Array | null>(null);
  const MEL_BANDS = 40; // Number of Mel bands for ODF calculation

  // --- Enhanced Analysis Functions ---

  const calculateBands = (frequencies: Uint8Array, sampleRate: number): FrequencyBands => {
    const nyquist = sampleRate / 2;
    const binSize = nyquist / frequencies.length;

    const bassEnd = Math.floor(250 / binSize);
    const midEnd = Math.floor(4000 / binSize);

    let bass = 0, mid = 0, treble = 0;
    let bassWeight = 0, midWeight = 0, trebleWeight = 0;

    for (let i = 1; i < frequencies.length; i++) {
      const freq = i * binSize;
      const magnitude = frequencies[i] / 255;

      const weight = A_WEIGHTING(freq);
      const weightedMagnitude = magnitude * weight;

      if (i <= bassEnd) {
        bass += weightedMagnitude;
        bassWeight += weight;
      } else if (i <= midEnd) {
        mid += weightedMagnitude;
        midWeight += weight;
      } else if (freq < nyquist - binSize) {
        treble += weightedMagnitude;
        trebleWeight += weight;
      }
    }

    return {
      bass: bassWeight > 0 ? bass / bassWeight : 0,
      mid: midWeight > 0 ? mid / midWeight : 0,
      treble: trebleWeight > 0 ? treble / trebleWeight : 0,
    };
  };

  const calculateSpectralFeatures = (frequencies: Uint8Array, sampleRate: number): SpectralFeatures => {
    const nyquist = sampleRate / 2;
    const binSize = nyquist / frequencies.length;

    let totalEnergy = 0;
    let centroidSum = 0;

    // NEW: Initialize Mel filterbank if not already done
    if (!melFilterbankRef.current) {
      melFilterbankRef.current = createMelFilterbank(frequencies.length * 2, MEL_BANDS, sampleRate);
      prevMelEnergiesRef.current = new Float32Array(MEL_BANDS).fill(0);
      console.log('🎵 Mel Filterbank initialized with', MEL_BANDS, 'bands for robust ODF');
    }

    // Calculate spectral centroid, spread, and rolloff
    for (let i = 1; i < frequencies.length - 1; i++) {
      const magnitude = frequencies[i] / 255;
      const freq = i * binSize;

      totalEnergy += magnitude;
      centroidSum += magnitude * freq;
    }

    const centroid = totalEnergy > 0 ? (centroidSum / totalEnergy) / nyquist : 0;

    let cumulativeEnergy = 0;
    let rolloff = 0;
    for (let i = 1; i < frequencies.length - 1; i++) {
      cumulativeEnergy += frequencies[i] / 255;
      if (cumulativeEnergy >= totalEnergy * 0.85) {
        rolloff = (i * binSize) / nyquist;
        break;
      }
    }

    let spreadSum = 0;
    if (totalEnergy > 0) {
      const centroidHz = centroid * nyquist;
      for (let i = 1; i < frequencies.length - 1; i++) {
        const magnitude = frequencies[i] / 255;
        const freq = i * binSize;
        spreadSum += magnitude * Math.pow(freq - centroidHz, 2);
      }
    }
    const spread = totalEnergy > 0 ? Math.sqrt(spreadSum / totalEnergy) / nyquist : 0;

    // NEW: Calculate robust multi-band ODF instead of simple spectral flux
    let flux = 0;
    if (melFilterbankRef.current && prevMelEnergiesRef.current) {
      flux = calculateRobustODF(
        frequencies,
        prevMelEnergiesRef.current,
        melFilterbankRef.current,
        MEL_BANDS
      );
    } else {
      // Fallback to old method if filterbank not ready
      const spectralChanges: number[] = [];
      for (let i = 1; i < frequencies.length - 1; i++) {
        const magnitude = frequencies[i] / 255;
        const prevMag = prevFrequenciesRef.current[i];
        const change = magnitude - prevMag;
        if (change > 0) {
          spectralChanges.push(change);
        }
      }
      flux = calculateMedian(spectralChanges);
    }

    // Update frequency history for potential fallback
    for (let i = 0; i < frequencies.length; i++) {
      prevFrequenciesRef.current[i] = frequencies[i] / 255;
    }

    return {
      centroid: Math.min(1, centroid),
      spread: Math.min(1, spread),
      flux: Math.min(1, flux * 10), // Scale for better dynamic range
      rolloff: Math.min(1, rolloff),
    };
  };

  // YIN-based melodic analysis with robust chromagram
  const calculateMelodicFeatures = (
    waveform: Uint8Array,
    frequencies: Uint8Array,
    sampleRate: number
  ): MelodicFeatures => {
    // Initialize YIN detector with improved settings
    if (!yinDetectorRef.current) {
      yinDetectorRef.current = new YINPitchDetector(sampleRate, 4096, 0.15); // Larger buffer, higher threshold
    }

    // Convert waveform for YIN algorithm with better normalization
    const float32Waveform = new Float32Array(waveform.length);
    let maxValue = 0;

    // First pass: find max value for proper normalization
    for (let i = 0; i < waveform.length; i++) {
      const sample = Math.abs((waveform[i] - 128) / 128);
      if (sample > maxValue) maxValue = sample;
    }

    // Second pass: normalize properly
    const normalizationFactor = maxValue > 0 ? 1 / maxValue : 1;
    for (let i = 0; i < waveform.length; i++) {
      float32Waveform[i] = ((waveform[i] - 128) / 128) * normalizationFactor;
    }

    // YIN pitch detection
    const pitchResult = yinDetectorRef.current.detectPitch(float32Waveform);
    let dominantFreq = pitchResult.frequency;
    let noteConfidence = pitchResult.probability;

    // FALLBACK: If YIN fails, use spectral peak detection
    if (dominantFreq <= 0 || noteConfidence < 0.3) {
      const nyquist = sampleRate / 2;
      const binSize = nyquist / frequencies.length;

      let maxMagnitude = 0;
      let maxBin = 0;

      // Focus on melodic range (80Hz - 1000Hz)
      const minBin = Math.floor(80 / binSize);
      const maxBinLimit = Math.floor(1000 / binSize);

      for (let i = minBin; i < Math.min(maxBinLimit, frequencies.length); i++) {
        if (frequencies[i] > maxMagnitude) {
          maxMagnitude = frequencies[i];
          maxBin = i;
        }
      }

      if (maxMagnitude > 30) { // Minimum threshold for detection
        // Parabolic interpolation for better accuracy
        if (maxBin > 0 && maxBin < frequencies.length - 1) {
          const y1 = frequencies[maxBin - 1];
          const y2 = frequencies[maxBin];
          const y3 = frequencies[maxBin + 1];

          const x0 = (y3 - y1) / (2 * (2 * y2 - y1 - y3));
          dominantFreq = (maxBin + x0) * binSize;
        } else {
          dominantFreq = maxBin * binSize;
        }

        noteConfidence = Math.min(0.8, maxMagnitude / 255); // Cap confidence from spectral method
      }
    }

    const { note } = frequencyToNote(dominantFreq);

    // Robust chromagram calculation with temporal smoothing
    const chroma = new Array(12).fill(0);
    const nyquist = sampleRate / 2;
    const binSize = nyquist / frequencies.length;

    // Map spectrum to pitch classes with proper weighting
    for (let i = 1; i < frequencies.length; i++) {
      const freq = i * binSize;
      const magnitude = frequencies[i] / 255;

      if (freq < 80 || freq > 4000) continue;

      // Find closest MIDI note
      const midiNote = 12 * Math.log2(freq / 440) + 69;
      const pitchClass = ((Math.round(midiNote) % 12) + 12) % 12;

      // Weight by magnitude and perceptual importance
      const weight = magnitude * A_WEIGHTING(freq);

      // Distribute energy to neighboring pitch classes for robustness
      chroma[pitchClass] += weight * 0.7;
      chroma[(pitchClass + 11) % 12] += weight * 0.15;
      chroma[(pitchClass + 1) % 12] += weight * 0.15;
    }

    // Normalize
    const chromaSum = chroma.reduce((a, b) => a + b, 0);
    if (chromaSum > 0) {
      for (let i = 0; i < 12; i++) {
        chroma[i] /= chromaSum;
      }
    }

    // Apply temporal smoothing
    for (let i = 0; i < 12; i++) {
      chromaSmoothingRef.current[i] = chromaSmoothingRef.current[i] * CHROMA_SMOOTHING +
                                       chroma[i] * (1 - CHROMA_SMOOTHING);
      chroma[i] = chromaSmoothingRef.current[i];
    }

    // Calculate harmonic content - FIXED
    let harmonicContent = 0;
    if (dominantFreq > 0 && frequencies.length > 0) {
      const fundamentalBin = Math.floor(dominantFreq / binSize);
      let fundamentalEnergy = 0;
      let harmonicEnergy = 0;

      // Get fundamental energy (average over 3 bins for robustness)
      for (let i = -1; i <= 1; i++) {
        const bin = fundamentalBin + i;
        if (bin >= 0 && bin < frequencies.length) {
          fundamentalEnergy += frequencies[bin] / 255;
        }
      }
      fundamentalEnergy /= 3;

      // Sum harmonic energies
      for (let harmonic = 2; harmonic <= 6; harmonic++) {
        const harmonicBin = Math.floor((dominantFreq * harmonic) / binSize);
        if (harmonicBin < frequencies.length) {
          // Average over neighboring bins
          let energy = 0;
          for (let i = -1; i <= 1; i++) {
            const bin = harmonicBin + i;
            if (bin >= 0 && bin < frequencies.length) {
              energy += frequencies[bin] / 255;
            }
          }
          harmonicEnergy += energy / 3;
        }
      }

      // Calculate ratio (0-1 range)
      if (fundamentalEnergy > 0.01) {
        harmonicContent = Math.min(1, harmonicEnergy / (fundamentalEnergy * 5));
      }
    }

    return {
      dominantFrequency: dominantFreq,
      dominantNote: note,
      noteConfidence,
      harmonicContent,
      pitchClass: chromaSmoothingRef.current
    };
  };

  // Autocorrelation-based rhythmic analysis
  const calculateRhythmicFeatures = (spectralFlux: number, currentTime: number, isOverallTransient: boolean): RhythmicFeatures => {
    // Update ODF history
    odfHistoryRef.current.push(spectralFlux);
    if (odfHistoryRef.current.length > ODF_HISTORY_SIZE) {
      odfHistoryRef.current.shift();
    }

    // BPM detection via autocorrelation
    const bpm = bpmDetectorRef.current.detectBPM(odfHistoryRef.current, ODF_SAMPLE_RATE);
    const confidence = bpmDetectorRef.current.getConfidence();

    // Debug logging for BPM detection
    if (bpm > 0 && confidence > 0.5) {
      console.log(`BPM: ${bpm.toFixed(1)}, Conf: ${(confidence * 100).toFixed(0)}%`);
    }

    // Update beat timing on strong transients
    if (isOverallTransient) {
      lastBeatTimeRef.current = currentTime;
    }

    const beatPhase = bpmDetectorRef.current.getBeatPhase(currentTime, bpm, lastBeatTimeRef.current);

    // Detect rhythmic subdivision
    let subdivision = 1;
    if (audioData.transients) {
      const transientCount = [
        audioData.transients.bass,
        audioData.transients.mid,
        audioData.transients.treble
      ].filter(Boolean).length;

      if (transientCount >= 2) subdivision = 2;
      if (transientCount === 3) subdivision = 4;
    }

    return {
      bpm: Math.round(bpm * 10) / 10,
      bpmConfidence: confidence * 100,
      beatPhase: Math.round(beatPhase * 1000) / 1000,
      subdivision,
      groove: confidence * 100
    };
  };

  const calculateDynamicValue = (value: number, envelope: { min: number; max: number }): number => {
    if (value > envelope.max) {
      envelope.max = value * (1 - ENVELOPE_CONFIG.adaptiveRate) + envelope.max * ENVELOPE_CONFIG.adaptiveRate;
    } else {
      envelope.max *= (1 - ENVELOPE_CONFIG.maxDecay);
    }

    if (value < envelope.min) {
      envelope.min = value * (1 - ENVELOPE_CONFIG.adaptiveRate) + envelope.min * ENVELOPE_CONFIG.adaptiveRate;
    } else {
      envelope.min = envelope.min * (1 + ENVELOPE_CONFIG.minDecay) + ENVELOPE_CONFIG.minThreshold;
    }

    envelope.min = Math.max(0, Math.min(envelope.min, 0.9));
    envelope.max = Math.max(envelope.min + 0.1, Math.min(envelope.max, 1));

    const range = envelope.max - envelope.min;
    return range > 0.01 ? Math.max(0, Math.min(1, (value - envelope.min) / range)) : value;
  };

  const detectTransients = (currentBands: FrequencyBands, energy: number): Transients => {
    const transients: Transients = { bass: false, mid: false, treble: false, overall: false };

    const detectBandTransient = (
        current: number,
        band: 'bass' | 'mid' | 'treble' | 'overall',
        value: number = current
    ): boolean => {
      const config = TRANSIENT_CONFIG[band];
      const state = transientStateRef.current[band];

      state.history.shift();
      state.history.push(value);

      const avgHistory = state.history.reduce((a, b) => a + b, 0) / state.history.length;
      const adaptiveThreshold = Math.max(config.threshold, avgHistory * config.multiplier);

      const isTransient = value > adaptiveThreshold && value > state.value * config.multiplier;

      state.value = state.value * config.decay + value * (1 - config.decay);

      return isTransient;
    };

    transients.bass = detectBandTransient(currentBands.bass, 'bass');
    transients.mid = detectBandTransient(currentBands.mid, 'mid');
    transients.treble = detectBandTransient(currentBands.treble, 'treble');
    transients.overall = detectBandTransient(energy, 'overall');

    return transients;
  };

  const detectDrop = (normalizedEnergy: number): number => {
    const now = Date.now();
    const surge = normalizedEnergy - prevNormalizedEnergyRef.current;

    // FIXED: Améliorations pour éviter le blocage à 0%

    // 1. Seuil adaptatif basé sur l'historique récent
    const recentEnergyAvg = energyHistoryRef.current.reduce((a, b) => a + b, 0) / energyHistoryRef.current.length;
    const adaptiveThreshold = Math.max(DROP_CONFIG.threshold, recentEnergyAvg * 0.3);

    // 2. Détection de surge améliorée avec multiple conditions
    const relativeSurge = prevNormalizedEnergyRef.current > 0 ? surge / prevNormalizedEnergyRef.current : surge;
    const absoluteSurge = surge;
    const energySpike = normalizedEnergy > recentEnergyAvg * 1.4; // Spike absolu

    // 3. Conditions multiples pour déclenchement
    const conditionMet = (
      (absoluteSurge > adaptiveThreshold && now - lastDropTimeRef.current > DROP_CONFIG.cooldown) ||
      (relativeSurge > 0.25 && absoluteSurge > 0.05 && now - lastDropTimeRef.current > DROP_CONFIG.cooldown) ||
      (energySpike && absoluteSurge > 0.03 && now - lastDropTimeRef.current > DROP_CONFIG.cooldown / 2)
    );

    if (conditionMet) {
      // 4. Intensité basée sur la meilleure condition qui a déclenché
      let intensity = 0;
      if (absoluteSurge > adaptiveThreshold) {
        intensity = Math.min(1, absoluteSurge * 2); // Amplification
      } else if (relativeSurge > 0.25) {
        intensity = Math.min(1, relativeSurge * 1.5);
      } else if (energySpike) {
        intensity = Math.min(1, (normalizedEnergy / recentEnergyAvg) * 0.4);
      }

      dropIntensityRef.current = Math.max(dropIntensityRef.current, intensity); // Prendre le max
      lastDropTimeRef.current = now;

      // FIXED: Debug logging amélioré
      console.log(`🔥 Drop detected! Type: ${absoluteSurge > adaptiveThreshold ? 'Absolute' : relativeSurge > 0.25 ? 'Relative' : 'Spike'} | Surge: ${surge.toFixed(3)} | Energy: ${normalizedEnergy.toFixed(3)} | Intensity: ${intensity.toFixed(3)}`);
    }

    // 5. FIXED: Déclin plus graduel pour éviter de tomber à 0 trop vite
    const decayRate = dropIntensityRef.current > 0.1 ? DROP_CONFIG.decay : Math.max(DROP_CONFIG.decay, 0.98);
    dropIntensityRef.current *= decayRate;

    // 6. Plancher minimum pour éviter le blocage complet
    if (dropIntensityRef.current < 0.001) {
      dropIntensityRef.current = 0;
    }

    prevNormalizedEnergyRef.current = normalizedEnergy;

    return dropIntensityRef.current;
  };

  // NOUVEAU: Amélioration de la détection d'énergie pour musique normale
  const calculateEnhancedEnergy = (frequencies: Uint8Array, waveform: Uint8Array): number => {
    // Calcul d'énergie plus robuste combinant plusieurs métriques
    let spectralEnergy = 0;
    let rmsEnergy = 0;
    let peakEnergy = 0;

    // 1. Énergie spectrale pondérée (favorise les fréquences importantes)
    for (let i = 1; i < frequencies.length - 1; i++) {
      const magnitude = frequencies[i] / 255;
      const freq = (i / frequencies.length) * 22050; // Approximation

      // Pondération perceptuelle simple (favorise 200Hz-4kHz)
      let weight = 1.0;
      if (freq >= 200 && freq <= 4000) {
        weight = 1.5; // Boost des fréquences importantes
      } else if (freq < 80 || freq > 8000) {
        weight = 0.5; // Atténue les extrêmes
      }

      spectralEnergy += magnitude * magnitude * weight;
    }
    spectralEnergy = Math.sqrt(spectralEnergy / frequencies.length);

    // 2. Énergie RMS du signal temporel
    for (let i = 0; i < waveform.length; i++) {
      const sample = (waveform[i] - 128) / 128;
      rmsEnergy += sample * sample;
    }
    rmsEnergy = Math.sqrt(rmsEnergy / waveform.length);

    // 3. Détection de pics (pour percussions)
    peakEnergy = Math.max(...Array.from(frequencies)) / 255;

    // Combinaison pondérée des trois métriques
    const combinedEnergy = spectralEnergy * 0.6 + rmsEnergy * 0.3 + peakEnergy * 0.1;

    return combinedEnergy;
  };

  useEffect(() => {
    if (!audioSource) return;

    try {
      audioContextRef.current = new AudioContext();
      analyserRef.current = audioContextRef.current.createAnalyser();

      // FIXED: Capture the real sample rate from AudioContext
      realSampleRateRef.current = audioContextRef.current.sampleRate;
      console.log('🎛️ AudioContext Sample Rate:', realSampleRateRef.current, 'Hz');

    } catch (error) {
      console.error('Failed to initialize AudioContext:', error);
      return;
    }

    analyserRef.current.fftSize = 4096; // Consider 4096 for melody detection
    analyserRef.current.smoothingTimeConstant = 0.75;
    analyserRef.current.minDecibels = -90;
    analyserRef.current.maxDecibels = -10;

    const bufferLength = analyserRef.current.frequencyBinCount;

    try {
      sourceNodeRef.current = audioContextRef.current.createMediaElementSource(audioSource);
      sourceNodeRef.current.connect(analyserRef.current);
      analyserRef.current.connect(audioContextRef.current.destination);
    } catch (error) {
      console.error('Failed to connect audio source:', error);
      return;
    }

    const frequencies = new Uint8Array(bufferLength);
    const waveform = new Uint8Array(bufferLength);
    prevFrequenciesRef.current = new Float32Array(bufferLength);

    const analyze = () => {
      if (!analyserRef.current || !audioContextRef.current) return;

      analyserRef.current.getByteFrequencyData(frequencies);
      analyserRef.current.getByteTimeDomainData(waveform);

      const maxFreq = Math.max(...Array.from(frequencies));
      if (maxFreq < 5) {
        setAudioData(prev => ({
          ...prev,
          volume: 0,
          energy: 0,
          bands: { bass: 0, mid: 0, treble: 0 },
          dynamicBands: { bass: 0, mid: 0, treble: 0 },
          transients: { bass: false, mid: false, treble: false, overall: false },
          dropIntensity: prev.dropIntensity * DROP_CONFIG.decay,
          melodicFeatures: {
            dominantFrequency: 0,
            dominantNote: 'N/A',
            noteConfidence: 0,
            harmonicContent: 0,
            pitchClass: new Array(12).fill(0)
          },
          rhythmicFeatures: {
            ...prev.rhythmicFeatures,
            bpm: 0,
            bpmConfidence: 0,
            beatPhase: 0,
            groove: prev.rhythmicFeatures.groove * 0.95
          }
        }));
        animationRef.current = requestAnimationFrame(analyze);
        return;
      }

      let rms = 0;
      for (let i = 0; i < waveform.length; i++) {
        const sample = (waveform[i] - 128) / 128;
        rms += sample * sample;
      }
      const volume = Math.sqrt(rms / waveform.length);

      let energy = 0;
      for (let i = 1; i < frequencies.length - 1; i++) {
        const magnitude = frequencies[i] / 255;
        energy += magnitude * magnitude;
      }
      energy = Math.sqrt(energy / (frequencies.length - 2));

      // FIXED: Use real sample rate everywhere
      const sampleRate = realSampleRateRef.current;
      const bands = calculateBands(frequencies, sampleRate);
      const spectralFeatures = calculateSpectralFeatures(frequencies, sampleRate);
      const melodicFeatures = calculateMelodicFeatures(waveform, frequencies, sampleRate);

      // NOUVEAU: Utiliser l'énergie améliorée au lieu de l'ancienne méthode
      const enhancedEnergy = calculateEnhancedEnergy(frequencies, waveform);

      // Garder l'ancienne énergie pour compatibilité, mais utiliser la nouvelle pour la détection
      const originalEnergy = energy;
      energy = enhancedEnergy;

      const dynamicBands = {
        bass: calculateDynamicValue(bands.bass, bandEnvelopeRef.current.bass),
        mid: calculateDynamicValue(bands.mid, bandEnvelopeRef.current.mid),
        treble: calculateDynamicValue(bands.treble, bandEnvelopeRef.current.treble),
      };

      const normalizedEnergy = calculateDynamicValue(energy, energyEnvelopeRef.current);

      // NOUVEAU: Mettre à jour l'historique d'énergie pour la détection adaptative
      energyHistoryRef.current.push(normalizedEnergy);
      if (energyHistoryRef.current.length > 20) {
        energyHistoryRef.current.shift();
      }

      const dropIntensity = detectDrop(normalizedEnergy);
      const transients = detectTransients(bands, energy);

      // NOUVEAU: Diagnostics périodiques pour comprendre les valeurs
      const frameCount = performance.now();
      if (Math.floor(frameCount / 2000) !== Math.floor((frameCount - 16) / 2000)) { // Log toutes les 2 secondes
        console.log(`🔍 Audio Diagnostics:
          Energy: ${energy.toFixed(3)} (orig: ${originalEnergy.toFixed(3)})
          Normalized Energy: ${normalizedEnergy.toFixed(3)}
          Drop Intensity: ${dropIntensity.toFixed(3)}
          Transients: B:${transients.bass} M:${transients.mid} T:${transients.treble} O:${transients.overall}
          Bands: B:${bands.bass.toFixed(2)} M:${bands.mid.toFixed(2)} T:${bands.treble.toFixed(2)}
          Dynamic: B:${dynamicBands.bass.toFixed(2)} M:${dynamicBands.mid.toFixed(2)} T:${dynamicBands.treble.toFixed(2)}
          Spectral Flux: ${spectralFeatures.flux.toFixed(3)}`);
      }

      // Update YIN detector with real sample rate if needed
      if (yinDetectorRef.current && yinDetectorRef.current.updateSampleRate) {
        yinDetectorRef.current.updateSampleRate(sampleRate);
      }

      // MODIFICATION: Appel à la nouvelle fonction rythmique avec le transient actuel
      const currentTime = performance.now() / 1000;
      // On passe le flux spectral ET le transient du frame actuel pour une synchronisation parfaite
      const rhythmicFeatures = calculateRhythmicFeatures(spectralFeatures.flux, currentTime, transients.overall);

      // NEW: Use TimbreAnalyzer for advanced musical analysis
      if (!timbreAnalyzerRef.current) {
        timbreAnalyzerRef.current = new TimbreAnalyzer();
      }

      const timbreProfile = timbreAnalyzerRef.current.analyzeTimbre(melodicFeatures, spectralFeatures);
      const musicalContext = timbreAnalyzerRef.current.analyzeMusicalContext(melodicFeatures, timbreProfile);

      setAudioData(prev => ({
        ...prev,
        frequencies: frequencies.slice(),
        waveform: waveform.slice(),
        volume,
        energy,
        bands,
        dynamicBands,
        transients,
        dropIntensity,
        spectralFeatures,
        melodicFeatures,
        rhythmicFeatures,
        timbreProfile,
        musicalContext,
        bass: dynamicBands.bass,
        mids: dynamicBands.mid,
        treble: dynamicBands.treble,
        beat: transients.overall,
        smoothedVolume: volume,
      }));

      prevBandsRef.current = bands;
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
