import { useFrame } from '@react-three/fiber';
import { useRef, useEffect, useMemo } from 'react';
import * as THREE from 'three';
import type { AudioData } from '../hooks/useAudioAnalyzer';
import type { SceneDefinition, SceneSettingsSchema } from './sceneTypes';
import type { GlobalSettings } from '../types/config';

// 1. Define the settings interface
interface HarmonicGridSettings {
  gridSize: number;
  spacing: number;
  heightMultiplier: number;
  baseColor: string;
  bassColor: string; // New setting for bass reactivity
  trebleFlashColor: string;
}

// 2. Create the scene component
const HarmonicGridComponent: React.FC<{ audioData: AudioData; config: HarmonicGridSettings; globalConfig: GlobalSettings }> = ({ audioData, config }) => {
  const meshRef = useRef<THREE.InstancedMesh>(null!);
  const dummy = new THREE.Object3D();
  const gridDataRef = useRef<number[][]>([]);
  const flashDecay = useRef(0);

  const { gridSize, spacing, heightMultiplier, baseColor, bassColor, trebleFlashColor } = config;

  // Memoize the color buffer to avoid re-creation
  const colorBuffer = useMemo(() => new Float32Array(gridSize * gridSize * 3), [gridSize]);

  // Initialize or resize the grid data when gridSize changes
  useEffect(() => {
    gridDataRef.current = Array(gridSize).fill(0).map(() => Array(gridSize).fill(0));
  }, [gridSize]);

  useFrame((_, delta) => {
    if (!meshRef.current || gridDataRef.current.length !== gridSize) return;

    const { frequencies, transients, dynamicBands } = audioData; // Use dynamicBands
    const gridData = gridDataRef.current;
    const numRows = gridSize;
    const numCols = gridSize;

    // --- Scrolling Logic (along the X-axis) ---
    for (let row = 0; row < numRows; row++) {
      for (let col = 0; col < numCols - 1; col++) {
        gridData[row][col] = gridData[row][col + 1];
      }
    }

    // --- Logarithmic Frequency Mapping ---
    const maxFreqIndex = frequencies.length;
    const logMax = Math.log(maxFreqIndex);
    for (let row = 0; row < numRows; row++) {
      const logPercent = row / numRows;
      const freqIndex = Math.floor(Math.exp(logPercent * logMax));
      const height = (frequencies[freqIndex] || 0) / 255;
      gridData[row][numCols - 1] = height;
    }

    // --- Transient Flash Effect ---
    if (transients.treble) {
      flashDecay.current = 1.0; // Start the flash
    }

    if (flashDecay.current > 0) {
      flashDecay.current = Math.max(0, flashDecay.current - delta * 2.5); // Fade speed
    }

    // --- Update InstancedMesh ---
    let i = 0;
    const centerX = numCols / 2;
    const centerZ = numRows / 2;
    const base = new THREE.Color(baseColor);
    const bass = new THREE.Color(bassColor);
    const flash = new THREE.Color(trebleFlashColor);

    for (let row = 0; row < numRows; row++) {
      for (let col = 0; col < numCols; col++) {
        const id = i++;
        const height = gridData[row][col] * heightMultiplier;
        const finalHeight = Math.max(0.05, height);

        // Position instance on the XZ plane with spacing
        dummy.position.set((col - centerX) * spacing, finalHeight / 2, (row - centerZ) * spacing);
        dummy.scale.set(1, finalHeight, 1);
        dummy.updateMatrix();
        meshRef.current.setMatrixAt(id, dummy.matrix);

        // Update color: Start with bass interpolation, then apply treble flash
        const bassMixColor = base.clone().lerp(bass, dynamicBands.bass);
        const finalColor = bassMixColor.lerp(flash, flashDecay.current);
        finalColor.toArray(colorBuffer, id * 3);
      }
    }

    meshRef.current.instanceMatrix.needsUpdate = true;
    if (meshRef.current.instanceColor) {
      meshRef.current.instanceColor.needsUpdate = true;
    }
  });

  return (
    <instancedMesh ref={meshRef} args={[undefined, undefined, gridSize * gridSize]}>
      <boxGeometry args={[0.8, 1, 0.8]} />
      <meshStandardMaterial />
      <instancedBufferAttribute attach="instanceColor" args={[colorBuffer, 3]} />
    </instancedMesh>
  );
};

// 3. Define the scene configuration
const schema: SceneSettingsSchema = {
  gridSize: { type: 'slider', label: 'Grid Size', min: 8, max: 64, step: 4 },
  spacing: { type: 'slider', label: 'Spacing', min: 0.5, max: 5, step: 0.1 },
  heightMultiplier: { type: 'slider', label: 'Height Multiplier', min: 1, max: 30, step: 1 },
  baseColor: { type: 'color', label: 'Base Color' },
  bassColor: { type: 'color', label: 'Bass Color' },
  trebleFlashColor: { type: 'color', label: 'Treble Flash Color' },
};

export const harmonicGridScene: SceneDefinition<HarmonicGridSettings> = {
  id: 'harmonicgrid',
  name: 'Harmonic Grid',
  component: HarmonicGridComponent,
  settings: {
    default: {
      gridSize: 32,
      spacing: 1.2,
      heightMultiplier: 15,
      baseColor: '#00ffff',
      bassColor: '#ff00ff', // Default for new bass color
      trebleFlashColor: '#ffffff',
    },
    schema,
  },
};
