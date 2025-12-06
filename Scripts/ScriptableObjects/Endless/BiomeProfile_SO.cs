using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects.Endless
{
    /// <summary>
    /// Profile de biome définissant les assets visuels et audio à appliquer lors d'une transition de monde.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeProfile_New", menuName = "GameData/Endless/Biome Profile")]
    public class BiomeProfile_SO : ScriptableObject
    {
        [Header("Identification")]
        public string BiomeName;

        [Header("Mood & Atmosphere")]
        public VisualMoodData MoodData;

        [Header("Tiles & Decorations")]
        public GameObject GroundTilePrefab;
        public GameObject ObstacleTilePrefab;
        public List<GameObject> DecorationPrefabs = new List<GameObject>();

        [Header("Audio")]
        [Tooltip("Optionnel : musique ou ambiance spécifique à ce biome.")]
        public AudioClip BiomeAmbientMusic;
    }
}