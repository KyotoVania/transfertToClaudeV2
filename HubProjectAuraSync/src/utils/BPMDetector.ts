// BPM Detection Module for AuraSync
import type {AudioData} from '../hooks/useAudioAnalyzer';

interface BeatInterval {
    time: number;
    strength: number;
}

export class BPMDetector {
    private beatHistory: BeatInterval[] = [];
    private lastBeatTime: number = 0;
    private bpmHistory: number[] = [];
    private readonly historySize = 20;
    private readonly minBPM = 60;
    private readonly maxBPM = 180;

    // Onset detection state
    private prevSpectralFlux: number = 0;
    private onsetThreshold: number = 0.3;
    private adaptiveThreshold: number[] = new Array(10).fill(0.3);

    detectBPM(audioData: AudioData, currentTime: number): number {
        // Detect onset using spectral flux
        const onset = this.detectOnset(audioData);

        if (onset.detected) {
            const interval = currentTime - this.lastBeatTime;

            // Only consider intervals within reasonable BPM range
            const bpm = 60 / interval;
            if (bpm >= this.minBPM && bpm <= this.maxBPM && interval > 0.2) {
                this.beatHistory.push({
                    time: currentTime,
                    strength: onset.strength
                });
                this.lastBeatTime = currentTime;

                // Keep only recent history
                if (this.beatHistory.length > this.historySize) {
                    this.beatHistory.shift();
                }

                // Calculate BPM from intervals
                if (this.beatHistory.length >= 4) {
                    const calculatedBPM = this.calculateBPMFromHistory();
                    if (calculatedBPM > 0) {
                        this.bpmHistory.push(calculatedBPM);
                        if (this.bpmHistory.length > 10) {
                            this.bpmHistory.shift();
                        }
                    }
                }
            }
        }

        // Return median of recent BPM calculations for stability
        return this.getStableBPM();
    }

    private detectOnset(audioData: AudioData): { detected: boolean; strength: number } {
        // Use spectral flux for onset detection
        const spectralFlux = audioData.spectralFeatures.flux;

        // Update adaptive threshold
        this.adaptiveThreshold.shift();
        this.adaptiveThreshold.push(spectralFlux);
        const avgThreshold = this.adaptiveThreshold.reduce((a, b) => a + b) / this.adaptiveThreshold.length;

        // Detect onset when flux exceeds adaptive threshold
        const threshold = Math.max(this.onsetThreshold, avgThreshold * 1.5);
        const detected = spectralFlux > threshold && spectralFlux > this.prevSpectralFlux * 1.3;

        this.prevSpectralFlux = spectralFlux;

        return {
            detected,
            strength: detected ? spectralFlux / threshold : 0
        };
    }

    private calculateBPMFromHistory(): number {
        if (this.beatHistory.length < 4) return 0;

        // Calculate intervals between consecutive beats
        const intervals: number[] = [];
        for (let i = 1; i < this.beatHistory.length; i++) {
            intervals.push(this.beatHistory[i].time - this.beatHistory[i - 1].time);
        }

        // Find the most common interval using histogram
        const histogram = new Map<number, number>();
        const tolerance = 0.05; // 50ms tolerance

        intervals.forEach(interval => {
            let found = false;
            for (const [key, count] of histogram) {
                if (Math.abs(interval - key) < tolerance) {
                    histogram.set(key, count + 1);
                    found = true;
                    break;
                }
            }
            if (!found) {
                histogram.set(interval, 1);
            }
        });

        // Find most common interval
        let maxCount = 0;
        let mostCommonInterval = 0;
        for (const [interval, count] of histogram) {
            if (count > maxCount) {
                maxCount = count;
                mostCommonInterval = interval;
            }
        }

        // Convert to BPM
        return mostCommonInterval > 0 ? 60 / mostCommonInterval : 0;
    }

    private getStableBPM(): number {
        if (this.bpmHistory.length === 0) return 0;

        // Return median for stability
        const sorted = [...this.bpmHistory].sort((a, b) => a - b);
        const mid = Math.floor(sorted.length / 2);
        return sorted.length % 2 === 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    // Get phase position within current beat (0-1)
    getBeatPhase(currentTime: number, bpm: number): number {
        if (bpm === 0 || this.lastBeatTime === 0) return 0;

        const beatDuration = 60 / bpm;
        const timeSinceLastBeat = currentTime - this.lastBeatTime;
        return (timeSinceLastBeat % beatDuration) / beatDuration;
    }

    // Predict next beat time
    getNextBeatTime(currentTime: number, bpm: number): number {
        if (bpm === 0) return currentTime + 1;

        const beatDuration = 60 / bpm;
        const phase = this.getBeatPhase(currentTime, bpm);
        return currentTime + (1 - phase) * beatDuration;
    }
}

// Hook for using BPM detection in components
import { useRef, useEffect, useState } from 'react';

export function useBPMDetection(audioData: AudioData) {
    const detectorRef = useRef(new BPMDetector());
    const [bpmInfo, setBPMInfo] = useState({
        bpm: 0,
        phase: 0,
        confidence: 0
    });

    useEffect(() => {
        const updateBPM = () => {
            const currentTime = performance.now() / 1000;
            const bpm = detectorRef.current.detectBPM(audioData, currentTime);
            const phase = detectorRef.current.getBeatPhase(currentTime, bpm);

            setBPMInfo({
                bpm: Math.round(bpm),
                phase,
                confidence: bpm > 0 ? 1 : 0 // Can be enhanced with actual confidence calculation
            });
        };

        updateBPM();
    }, [audioData]);

    return bpmInfo;
}

// Utility functions for BPM-synced animations
export const BPMSync = {
    // Get a value that oscillates with the beat (0-1-0)
    sineWave: (phase: number): number => {
        return Math.sin(phase * Math.PI * 2) * 0.5 + 0.5;
    },

    // Get a sawtooth wave (0-1 ramp)
    sawtooth: (phase: number): number => {
        return phase;
    },

    // Get a square wave (0 or 1)
    square: (phase: number): number => {
        return phase < 0.5 ? 0 : 1;
    },

    // Get a pulse at beat start
    pulse: (phase: number, width: number = 0.1): number => {
        return phase < width ? 1 - (phase / width) : 0;
    },

    // Quantize time to nearest beat subdivision
    quantize: (phase: number, subdivisions: number): number => {
        return Math.floor(phase * subdivisions) / subdivisions;
    }
};