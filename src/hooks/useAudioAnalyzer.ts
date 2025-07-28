import { useState, useEffect, useCallback, useRef } from 'react';
import type { AudioData, AudioBands, AudioSourceType, AudioConfig } from '../types/config';

export interface UseAudioAnalyzerReturn {
  audioData: AudioData | null;
  isConnected: boolean;
  sourceType: AudioSourceType;
  audioElement: HTMLAudioElement | null;

  // Source control functions
  setFileSource: (file: File) => Promise<void>;
  setMicrophoneSource: () => Promise<void>;
  disconnect: () => void;

  // Playback control (for file source)
  play: () => Promise<void>;
  pause: () => void;
  setVolume: (volume: number) => void;
  setCurrentTime: (time: number) => void;

  // State
  isPlaying: boolean;
  duration: number;
  currentTime: number;
  volume: number;
  error: string | null;
}

const DEFAULT_AUDIO_CONFIG: AudioConfig = {
  fftSize: 2048,
  smoothingTimeConstant: 0.8,
  minDecibels: -90,
  maxDecibels: -10,
};

export function useAudioAnalyzer(config: Partial<AudioConfig> = {}): UseAudioAnalyzerReturn {
  const audioConfig = { ...DEFAULT_AUDIO_CONFIG, ...config };

  // State
  const [audioData, setAudioData] = useState<AudioData | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [sourceType, setSourceType] = useState<AudioSourceType>('none');
  const [isPlaying, setIsPlaying] = useState(false);
  const [duration, setDuration] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);
  const [volume, setVolumeState] = useState(1);
  const [error, setError] = useState<string | null>(null);

  // Refs
  const audioElementRef = useRef<HTMLAudioElement | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const sourceRef = useRef<AudioNode | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  // Audio analysis function
  const analyzeAudio = useCallback(() => {
    if (!analyserRef.current) return;

    const analyser = analyserRef.current;
    const frequencyData = new Uint8Array(analyser.frequencyBinCount);
    const timeData = new Uint8Array(analyser.frequencyBinCount);

    analyser.getByteFrequencyData(frequencyData);
    analyser.getByteTimeDomainData(timeData);

    // Calculate volume (RMS)
    let sum = 0;
    for (let i = 0; i < timeData.length; i++) {
      const sample = (timeData[i] - 128) / 128;
      sum += sample * sample;
    }
    const volume = Math.sqrt(sum / timeData.length);

    // Calculate frequency bands
    const bassEnd = Math.floor(frequencyData.length * 0.1);
    const midEnd = Math.floor(frequencyData.length * 0.4);

    let bassSum = 0, midSum = 0, trebleSum = 0;

    // Bass (0-10% of frequency range)
    for (let i = 0; i < bassEnd; i++) {
      bassSum += frequencyData[i];
    }

    // Mids (10-40% of frequency range)
    for (let i = bassEnd; i < midEnd; i++) {
      midSum += frequencyData[i];
    }

    // Treble (40-100% of frequency range)
    for (let i = midEnd; i < frequencyData.length; i++) {
      trebleSum += frequencyData[i];
    }

    const bands: AudioBands = {
      bass: bassSum / (bassEnd * 255),
      mid: midSum / ((midEnd - bassEnd) * 255),
      treble: trebleSum / ((frequencyData.length - midEnd) * 255),
    };

    setAudioData({
      volume,
      bands,
      frequencyData,
      timeData,
    });

    animationFrameRef.current = requestAnimationFrame(analyzeAudio);
  }, []);

  // Initialize audio context and analyser
  const initializeAnalyser = useCallback(async () => {
    try {
      if (!audioContextRef.current) {
        audioContextRef.current = new (window.AudioContext || (window as any).webkitAudioContext)();
      }

      const audioContext = audioContextRef.current;

      if (audioContext.state === 'suspended') {
        await audioContext.resume();
      }

      if (!analyserRef.current) {
        analyserRef.current = audioContext.createAnalyser();
        analyserRef.current.fftSize = audioConfig.fftSize;
        analyserRef.current.smoothingTimeConstant = audioConfig.smoothingTimeConstant;
        analyserRef.current.minDecibels = audioConfig.minDecibels;
        analyserRef.current.maxDecibels = audioConfig.maxDecibels;
      }

      return { audioContext, analyser: analyserRef.current };
    } catch (err) {
      setError('Failed to initialize audio context: ' + (err as Error).message);
      throw err;
    }
  }, [audioConfig]);

  // Set file source
  const setFileSource = useCallback(async (file: File) => {
    try {
      setError(null);
      disconnect();

      const { audioContext, analyser } = await initializeAnalyser();

      // Create audio element
      const audioElement = new Audio();
      audioElement.src = URL.createObjectURL(file);
      audioElement.crossOrigin = 'anonymous';

      // Setup event listeners
      audioElement.addEventListener('loadedmetadata', () => {
        setDuration(audioElement.duration);
      });

      audioElement.addEventListener('timeupdate', () => {
        setCurrentTime(audioElement.currentTime);
      });

      audioElement.addEventListener('play', () => setIsPlaying(true));
      audioElement.addEventListener('pause', () => setIsPlaying(false));
      audioElement.addEventListener('ended', () => setIsPlaying(false));

      // Create audio source and connect to analyser
      const source = audioContext.createMediaElementSource(audioElement);
      source.connect(analyser);
      analyser.connect(audioContext.destination);

      audioElementRef.current = audioElement;
      sourceRef.current = source;
      setSourceType('file');
      setIsConnected(true);

      // Start analysis
      analyzeAudio();

    } catch (err) {
      setError('Failed to set file source: ' + (err as Error).message);
    }
  }, [initializeAnalyser, analyzeAudio, disconnect]);

  // Set microphone source
  const setMicrophoneSource = useCallback(async () => {
    try {
      setError(null);
      disconnect();

      const { audioContext, analyser } = await initializeAnalyser();

      // Request microphone access
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      // Create audio source from stream
      const source = audioContext.createMediaStreamSource(stream);
      source.connect(analyser);

      streamRef.current = stream;
      sourceRef.current = source;
      setSourceType('microphone');
      setIsConnected(true);

      // Start analysis
      analyzeAudio();

    } catch (err) {
      setError('Failed to access microphone: ' + (err as Error).message);
    }
  }, [initializeAnalyser, analyzeAudio, disconnect]);

  // Disconnect current source
  const disconnect = useCallback(() => {
    // Stop animation frame
    if (animationFrameRef.current) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }

    // Disconnect audio source
    if (sourceRef.current) {
      sourceRef.current.disconnect();
      sourceRef.current = null;
    }

    // Stop media stream (microphone)
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(track => track.stop());
      streamRef.current = null;
    }

    // Clean up audio element
    if (audioElementRef.current) {
      audioElementRef.current.pause();
      URL.revokeObjectURL(audioElementRef.current.src);
      audioElementRef.current = null;
    }

    setIsConnected(false);
    setSourceType('none');
    setAudioData(null);
    setIsPlaying(false);
    setDuration(0);
    setCurrentTime(0);
    setError(null);
  }, []);

  // Playback controls (for file source)
  const play = useCallback(async () => {
    if (audioElementRef.current && sourceType === 'file') {
      try {
        await audioElementRef.current.play();
      } catch (err) {
        setError('Failed to play audio: ' + (err as Error).message);
      }
    }
  }, [sourceType]);

  const pause = useCallback(() => {
    if (audioElementRef.current && sourceType === 'file') {
      audioElementRef.current.pause();
    }
  }, [sourceType]);

  const setVolume = useCallback((newVolume: number) => {
    const clampedVolume = Math.max(0, Math.min(1, newVolume));
    setVolumeState(clampedVolume);

    if (audioElementRef.current && sourceType === 'file') {
      audioElementRef.current.volume = clampedVolume;
    }
  }, [sourceType]);

  const setCurrentTimeCallback = useCallback((time: number) => {
    if (audioElementRef.current && sourceType === 'file') {
      audioElementRef.current.currentTime = time;
    }
  }, [sourceType]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      disconnect();
      if (audioContextRef.current) {
        audioContextRef.current.close();
      }
    };
  }, [disconnect]);

  return {
    audioData,
    isConnected,
    sourceType,
    audioElement: audioElementRef.current,

    // Source control
    setFileSource,
    setMicrophoneSource,
    disconnect,

    // Playback control
    play,
    pause,
    setVolume,
    setCurrentTime: setCurrentTimeCallback,

    // State
    isPlaying,
    duration,
    currentTime,
    volume,
    error,
  };
}
