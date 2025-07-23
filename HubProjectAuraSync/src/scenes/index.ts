import { bars2DScene } from './Bars2D';
import { constellationScene } from './ConstellationVivante';
import { pulsarGridScene } from './PulsarGrid';
import { tunnelSDFScene } from './TunnelSDF';
import {improvedHarmonicGridScene} from './HarmonicGrid';
import {advancedTunnelScene} from "./SimpleScene.tsx";

export const scenes = [bars2DScene, constellationScene, pulsarGridScene, tunnelSDFScene, improvedHarmonicGridScene, advancedTunnelScene];

export const scenesById = Object.fromEntries(scenes.map(scene => [scene.id, scene]));