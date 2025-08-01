import { bars2DScene } from './Bars2D';
import { constellationScene } from './ConstellationVivante';
import { improvedHarmonicGridScene } from './HarmonicGrid';
import { harmonicGridV2Scene } from './HarmonicGridV2';
import { harmonicGridV3Scene } from './HarmonicGridV3';
import { chainSpellScene } from './ChainSpellRender';

export const scenes = [
    bars2DScene,
    constellationScene,
    improvedHarmonicGridScene,
    harmonicGridV2Scene,
    harmonicGridV3Scene,
    chainSpellScene,
];

export const scenesById = Object.fromEntries(scenes.map(scene => [scene.id, scene]));