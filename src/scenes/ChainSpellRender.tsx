import { useRef, useMemo, useState, useEffect } from 'react';
import { useFrame, useThree } from '@react-three/fiber';
import * as THREE from 'three';
import type { AudioData } from '../hooks/useAudioAnalyzer';
import type { SceneDefinition, SceneSettingsSchema } from './sceneTypes';
import type { GlobalSettings } from '../types/config';

// 1. Define the settings interface
interface ChainSpellSettings {
    // Visual settings
    animationSpeed: number;
    colorIntensity: number;
    fogDensity: number;
    cameraDistance: number;
    // Shader-specific settings
    spellCount: number;
    chainComplexity: number;
    stormIntensity: number;
}

// Vertex shader - simple pass-through for full-screen quad
const vertexShader = `
varying vec2 vUv;

void main() {
  vUv = uv;
  gl_Position = vec4(position, 1.0);
}
`;

// Fragment shader - ported from Shadertoy with frequency band control
const fragmentShader = `
uniform vec3 iResolution;
uniform float iTime;
uniform vec4 iMouse;
uniform float cameraDistance;
uniform float colorIntensity;
uniform float fogDensity;
uniform float spellCount;
uniform float chainComplexity;
uniform float stormIntensity;
// NEW: Array of 32 frequency bands for individual chain control
uniform float u_frequency_bands[32];

varying vec2 vUv;

#define PI 3.14159
#define TAU PI*2.

// Number of raymarching steps
#define STEPS 30.

// Distance minimum for volume collision
#define BIAS 0.001

// Distance minimum 
#define DIST_MIN 0.01

// Rotation matrix
mat2 rot(float a) { 
  float c = cos(a), s = sin(a);
  return mat2(c, -s, s, c); 
}

// Distance field functions
float sdSphere(vec3 p, float r) { 
  return length(p) - r; 
}

float sdCylinder(vec2 p, float r) { 
  return length(p) - r; 
}

float sdTorus(vec3 p, vec2 s) {
  vec2 q = vec2(length(p.xz) - s.x, p.y);
  return length(q) - s.y;
}

float sdBox(vec3 p, vec3 b) {
  vec3 d = abs(p) - b;
  return min(max(d.x, max(d.y, d.z)), 0.0) + length(max(d, 0.0));
}

// Smooth minimum
float smin(float a, float b, float r) {
  float h = clamp(0.5 + 0.5 * (b - a) / r, 0., 1.);
  return mix(b, a, h) - r * h * (1. - h);
}

// Random function 
float rand(vec2 co) { 
  return fract(sin(dot(co * 0.123, vec2(12.9898, 78.233))) * 43758.5453); 
}

// Polar domain repetition
vec3 moda(vec2 p, float count) {
  float an = TAU / count;
  float a = atan(p.y, p.x) + an / 2.;
  float c = floor(a / an);
  a = mod(a, an) - an / 2.;
  return vec3(vec2(cos(a), sin(a)) * length(p), c);
}

// The rhythm of animation - NOW CONTROLLED BY FREQUENCY BANDS
float getLocalWave(float x, int chainId) {
  // Ensure chainId is within bounds
  int safeChainId = clamp(chainId, 0, 31);
  
  // Get frequency energy for this specific chain
  float frequency_energy = u_frequency_bands[safeChainId];
  
  // Apply frequency energy to the wave with enhanced intensity
  float effect_intensity = frequency_energy * 15.0; // Multiplicateur pour rendre l'effet visible
  
  return sin(-iTime + x * 3.) * (1.0 + effect_intensity); 
}

// Displacement in world space of the animation - NOW PER CHAIN
float getWorldWave(float x, int chainId) { 
  return 1. - 0.1 * getLocalWave(x, chainId); 
}

// Camera control - using mouse position for now
vec3 camera(vec3 p) {
  // Use normalized mouse coordinates (-0.5 to 0.5)
  float rotX = (iMouse.x / iResolution.x - 0.5) * PI;
  float rotY = (iMouse.y / iResolution.y - 0.5) * PI;
  
  p.yz *= rot(rotY);
  p.xz *= rot(rotX);
  
  // Apply camera distance (zoom)
  p *= cameraDistance;
  
  return p;
}

// Position of chain - ENHANCED WITH FREQUENCY CONTROL
vec3 posChain(vec3 p, float count, int chainId) {
  float za = atan(p.z, p.x);
  vec3 dir = normalize(p);
  
  // Domain repetition
  vec3 m = moda(p.xz, count);
  p.xz = m.xy;
  
  // Get chain-specific wave with frequency energy
  float lw = getLocalWave(m.z / PI, chainId);
  
  // Get frequency energy for additional effects
  int safeChainId = clamp(chainId, 0, 31);
  float frequency_energy = u_frequency_bands[safeChainId];
  float chain_intensity = frequency_energy * 20.0; // Intensity multiplier
  
  p.x -= 1. - 0.1 * lw;
  
  // The chain shape with frequency-based deformation
  p.z *= 1. - clamp(0.03 / abs(p.z), 0., 1.) * (1.0 + chain_intensity * 0.5);
  
  // Animation of breaking chain - ENHANCED WITH FREQUENCY
  float r1 = lw * smoothstep(0.1, 0.5, lw) * (1.0 + chain_intensity);
  float r2 = lw * smoothstep(0.4, 0.6, lw) * (1.0 + chain_intensity * 0.8);
  p += dir * mix(0., 0.3 * sin(floor(za * 3.)), r1);
  p += dir * mix(0., 0.8 * sin(floor(za * 60.)), r2);
  
  // Rotate chain for animation smoothness with frequency influence
  float a = lw * 0.3 * (1.0 + frequency_energy * 2.0);
  p.xy *= rot(a);
  p.xz *= rot(a);
  return p;
}

// Distance function for spell - ENHANCED WITH FREQUENCY
float mapSpell(vec3 p, int chainId) {
  float scene = 1.;
  float a = atan(p.z, p.x);
  float l = length(p);
  float lw = getLocalWave(a, chainId);
  
  // Get frequency energy for this chain
  int safeChainId = clamp(chainId, 0, 31);
  float frequency_energy = u_frequency_bands[safeChainId];
  float spell_intensity = frequency_energy * 10.0;
  
  // Warping space into cylinder with frequency influence
  p.z = l - 1. + 0.1 * lw * (1.0 + spell_intensity);
  
  // Torsade effect enhanced by frequency
  float torsion_factor = 1.0 + frequency_energy * 3.0;
  p.yz *= rot(iTime * torsion_factor + a * 2.);
  
  // Long cube shape with frequency-based size variation
  float size_variation = 0.25 - 0.1 * lw * (1.0 + spell_intensity * 0.5);
  scene = min(scene, sdBox(p, vec3(10., vec2(size_variation))));
  
  // Long cylinder cutting the box with frequency influence
  float cylinder_size = 0.3 - 0.2 * lw * (1.0 + frequency_energy * 2.0);
  scene = max(scene, -sdCylinder(p.zy, cylinder_size));
  return scene;
}

// Distance function for the chain - ENHANCED WITH PER-CHAIN FREQUENCY
float mapChain(vec3 p) {
  float scene = 1.;
  
  // Number of chains based on complexity setting (limit to 32 for frequency bands)
  int maxChains = min(int(chainComplexity), 32);
  
  // Size of chain
  vec2 size = vec2(0.1, 0.02);
  
  // Generate chains with individual frequency control
  for (int i = 0; i < 32; i++) {
    if (i >= maxChains) break;
    
    // First set of chains with frequency control
    float torus = sdTorus(posChain(p, float(maxChains), i).yxz, size);
    scene = smin(scene, torus, 0.1);
    
    // Second set of chains with rotation offset
    vec3 p2 = p;
    p2.xz *= rot(PI / float(maxChains));
    scene = min(scene, sdTorus(posChain(p2, float(maxChains), i).xyz, size));
  }
  
  return scene;
}

// Position of core stuff
vec3 posCore(vec3 p, float count) {
  // Polar domain repetition
  vec3 m = moda(p.xz, count);
  p.xz = m.xy;
  
  // Linear domain repetition
  float c = 0.2;
  p.x = mod(p.x, c) - c / 2.;
  return p;
}

// Distance field for the core thing in the center - ENHANCED
float mapCore(vec3 p) {
  float scene = 1.;
  
  // Number of torus repeated
  float count = spellCount * 2.0;
  float a = p.x * 2.;
  
  // Calculate average frequency energy for core effects
  float avg_frequency = 0.0;
  for (int i = 0; i < 32; i++) {
    avg_frequency += u_frequency_bands[i];
  }
  avg_frequency /= 32.0;
  
  // Displace space - Storm intensity affects rotation speed with frequency
  float stormFactor = 1.0 + stormIntensity * 2.0 + avg_frequency * 5.0;
  p.xz *= rot(p.y * 6.);
  p.xz *= rot(iTime * stormFactor);
  p.xy *= rot(iTime * 0.5 * stormFactor);
  p.yz *= rot(iTime * 1.5 * stormFactor);
  vec3 p1 = posCore(p, count);
  
  // Size variation based on average frequency
  vec2 size = vec2(0.1, 0.2) * (1.0 + avg_frequency * 3.0);
  
  // Tentacles torus shape
  scene = min(scene, sdTorus(p1.xzy * 1.5, size));
  
  // Sphere used for intersection difference with the toruses
  float sphere_size = 0.6 * (1.0 + avg_frequency * 2.0);
  scene = max(-scene, sdSphere(p, sphere_size));
  return scene;
}

void main() {
  // Raymarch camera
  vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;
  vec3 eye = camera(vec3(uv, -1.5));
  vec3 ray = camera(normalize(vec3(uv, 1.)));
  vec3 pos = eye;
  
  // Dithering
  vec2 dpos = gl_FragCoord.xy / iResolution.xy;
  vec2 seed = dpos + fract(iTime);
  
  float shade = 0.;
  float totalDistance = 0.;
  
  // Calculate which chain we're primarily looking at for color
  float a = atan(pos.z, pos.x);
  int primaryChain = int(mod(a / TAU * chainComplexity + 0.5, 32.0));
  
  for (float i = 0.; i < STEPS; ++i) {
    // Distance from the different shapes with frequency control
    float distSpell = min(mapSpell(pos, primaryChain), mapCore(pos));
    float distChain = mapChain(pos);
    float dist = min(distSpell, distChain);
    
    // Hit volume
    if (dist < BIAS) {
      // Add shade
      shade += 1.;
      
      // Hit non transparent volume
      if (distChain < distSpell) {
        // Set shade and stop iteration
        shade = STEPS - i - 1.;
        break;
      }
    }
    
    // Dithering
    dist = abs(dist) * (0.8 + 0.2 * rand(seed * vec2(i)));
    
    // Minimum step
    dist = max(DIST_MIN, dist);
    
    // Raymarch
    pos += ray * dist;
    totalDistance += dist;
  }
  
  // Color from the normalized steps
  float normalizedShade = shade / (STEPS - 1.);
  
  // Apply color intensity with frequency influence
  float frequency_boost = 1.0 + u_frequency_bands[clamp(primaryChain, 0, 31)] * 2.0;
  vec3 baseColor = vec3(normalizedShade * colorIntensity * frequency_boost);
  
  // Add frequency-based color variation
  float hueShift = float(primaryChain) / 32.0;
  baseColor *= vec3(
    1.0 + sin(hueShift * TAU) * 0.3,
    1.0 + sin(hueShift * TAU + TAU/3.0) * 0.3,
    1.0 + sin(hueShift * TAU + 2.0*TAU/3.0) * 0.3
  );
  
  // Apply fog based on distance
  float fogAmount = 1.0 - exp(-totalDistance * fogDensity * 0.05);
  vec3 fogColor = vec3(0.0);
  vec3 finalColor = mix(baseColor, fogColor, fogAmount);
  
  gl_FragColor = vec4(finalColor, 1.0);
}
`;

// 2. Create the scene component
const ChainSpellComponent: React.FC<{ audioData: AudioData; config: ChainSpellSettings; globalConfig: GlobalSettings }> = ({ audioData, config, globalConfig }) => {
    const meshRef = useRef<THREE.Mesh>(null);
    const { size, viewport, mouse, gl } = useThree();

    // Mouse drag state
    const [isDragging, setIsDragging] = useState(false);
    const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
    const [currentRotation, setCurrentRotation] = useState({ x: 0, y: 0 });
    const [rotation, setRotation] = useState({ x: 0, y: 0 });

    // Zoom state
    const [zoom, setZoom] = useState(1.0);

    // Create shader material with uniforms
    const uniforms = useMemo(() => ({
        iTime: { value: 0 },
        iResolution: { value: new THREE.Vector3() },
        iMouse: { value: new THREE.Vector4() },
        cameraDistance: { value: config.cameraDistance },
        colorIntensity: { value: config.colorIntensity },
        fogDensity: { value: config.fogDensity },
        spellCount: { value: config.spellCount },
        chainComplexity: { value: config.chainComplexity },
        stormIntensity: { value: config.stormIntensity },
        // NEW: Array of 32 frequency bands for individual chain control
        u_frequency_bands: { value: new Array(32).fill(0.0) },
    }), []); // Empty dependency array to avoid recreating uniforms

    // Function to extract 32 frequency bands from audio data
    const extractFrequencyBands = (frequencies: Uint8Array): number[] => {
        const bands = new Array(32);
        const frequencyBinCount = frequencies.length;

        // Create 32 logarithmically distributed frequency bands
        for (let i = 0; i < 32; i++) {
            // Logarithmic distribution for better frequency coverage
            const startBin = Math.floor(Math.pow(i / 32, 2) * frequencyBinCount);
            const endBin = Math.floor(Math.pow((i + 1) / 32, 2) * frequencyBinCount);

            // Calculate average magnitude for this band
            let sum = 0;
            let count = 0;
            for (let bin = startBin; bin < Math.min(endBin, frequencyBinCount); bin++) {
                sum += frequencies[bin] / 255.0; // Normalize to 0-1
                count++;
            }

            // Store the average magnitude for this frequency band
            bands[i] = count > 0 ? sum / count : 0;
        }

        return bands;
    };

    // Handle mouse events
    useEffect(() => {
        const canvas = gl.domElement;

        const handleMouseDown = (e: MouseEvent) => {
            setIsDragging(true);
            setDragStart({
                x: e.clientX,
                y: e.clientY
            });
            setCurrentRotation({ ...rotation });
        };

        const handleMouseMove = (e: MouseEvent) => {
            if (!isDragging) return;

            const deltaX = e.clientX - dragStart.x;
            const deltaY = e.clientY - dragStart.y;

            // Convert pixel movement to rotation (adjust sensitivity as needed)
            const sensitivity = 0.5;
            setRotation({
                x: currentRotation.x + (deltaX * sensitivity),
                y: currentRotation.y + (deltaY * sensitivity)
            });
        };

        const handleMouseUp = () => {
            setIsDragging(false);
        };

        const handleWheel = (e: WheelEvent) => {
            e.preventDefault();
            const delta = e.deltaY * 0.001;
            setZoom(prevZoom => Math.max(0.5, Math.min(3.0, prevZoom + delta)));
        };

        canvas.addEventListener('mousedown', handleMouseDown);
        window.addEventListener('mousemove', handleMouseMove);
        window.addEventListener('mouseup', handleMouseUp);
        canvas.addEventListener('wheel', handleWheel);

        return () => {
            canvas.removeEventListener('mousedown', handleMouseDown);
            window.removeEventListener('mousemove', handleMouseMove);
            window.removeEventListener('mouseup', handleMouseUp);
            canvas.removeEventListener('wheel', handleWheel);
        };
    }, [isDragging, dragStart, currentRotation, rotation, gl.domElement]);

    // Update uniforms each frame
    useFrame((state) => {
        if (!meshRef.current) return;
        const material = meshRef.current.material as THREE.ShaderMaterial;

        // Update time
        material.uniforms.iTime.value = state.clock.elapsedTime * config.animationSpeed;

        // Update resolution
        material.uniforms.iResolution.value.set(size.width, size.height, 1);

        // Update mouse position based on rotation state
        // Convert rotation to normalized coordinates for the shader
        const normalizedX = (rotation.x / size.width) % 1;
        const normalizedY = (rotation.y / size.height) % 1;

        material.uniforms.iMouse.value.set(
            normalizedX * size.width + size.width * 0.5,
            normalizedY * size.height + size.height * 0.5,
            0,
            0
        );

        // Update camera distance with zoom
        material.uniforms.cameraDistance.value = config.cameraDistance * zoom;

        // Update all other uniforms from config
        material.uniforms.colorIntensity.value = config.colorIntensity;
        material.uniforms.fogDensity.value = config.fogDensity;
        material.uniforms.spellCount.value = config.spellCount;
        material.uniforms.chainComplexity.value = config.chainComplexity;
        material.uniforms.stormIntensity.value = config.stormIntensity;

        // Update frequency bands from audio data
        const frequencies = audioData.frequencies;
        if (frequencies) {
            const bands = extractFrequencyBands(frequencies);
            material.uniforms.u_frequency_bands.value = bands;
        }
    });

    // Change cursor on drag
    useEffect(() => {
        document.body.style.cursor = isDragging ? 'grabbing' : 'grab';
        return () => {
            document.body.style.cursor = 'auto';
        };
    }, [isDragging]);

    // Create a fullscreen quad using viewport dimensions
    return (
        <mesh ref={meshRef}>
            <planeGeometry args={[viewport.width, viewport.height]} />
            <shaderMaterial
                vertexShader={vertexShader}
                fragmentShader={fragmentShader}
                uniforms={uniforms}
                depthWrite={false}
                depthTest={false}
            />
        </mesh>
    );
};

// 3. Define the scene configuration
const schema: SceneSettingsSchema = {
    animationSpeed: { type: 'slider', label: 'Animation Speed', min: 0.1, max: 3, step: 0.1 },
    colorIntensity: { type: 'slider', label: 'Color Intensity', min: 0.5, max: 2, step: 0.1 },
    fogDensity: { type: 'slider', label: 'Fog Density', min: 0, max: 2, step: 0.1 },
    cameraDistance: { type: 'slider', label: 'Camera Distance', min: 0.5, max: 3, step: 0.1 },
    spellCount: { type: 'slider', label: 'Spell Count', min: 1, max: 10, step: 1 },
    chainComplexity: { type: 'slider', label: 'Chain Complexity', min: 10, max: 30, step: 1 },
    stormIntensity: { type: 'slider', label: 'Storm Intensity', min: 0, max: 2, step: 0.1 },
};

export const chainSpellScene: SceneDefinition<ChainSpellSettings> = {
    id: 'chainspell',
    name: 'Chain Spell',
    component: ChainSpellComponent,
    settings: {
        default: {
            animationSpeed: 1.0,
            colorIntensity: 1.0,
            fogDensity: 0.5,
            cameraDistance: 1.5,
            spellCount: 5,
            chainComplexity: 21,
            stormIntensity: 0.7,
        },
        schema,
    },
};
