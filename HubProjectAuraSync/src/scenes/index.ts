import { bars2DScene } from './Bars2D';
import { constellationScene } from './ConstellationVivante';
import { pulsarGridScene } from './PulsarGrid';
import { tunnelSDFScene } from './TunnelSDF';
import { harmonicGridScene } from './HarmonicGrid';

export const scenes = [bars2DScene, constellationScene, pulsarGridScene, tunnelSDFScene, harmonicGridScene];

export const scenesById = Object.fromEntries(scenes.map(scene => [scene.id, scene]));