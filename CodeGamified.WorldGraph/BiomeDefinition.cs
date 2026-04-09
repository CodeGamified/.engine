// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System.Collections.Generic;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Abstract biome definition — games provide concrete implementations with
    /// domain-specific resource abundances and material palettes.
    ///
    ///   class PirateBiome : BiomeDefinition  →  WoodAbundance, "adobe" walls
    ///   class SpaceBiome  : BiomeDefinition  →  OreAbundance, "regolith" terrain
    ///
    /// TypeId maps to the game's biome enum cast to int.
    /// </summary>
    public abstract class BiomeDefinition
    {
        /// <summary>Biome type ID (game's enum cast to int).</summary>
        public int TypeId { get; }

        protected BiomeDefinition(int typeId) => TypeId = typeId;

        /// <summary>Get resource abundance multiplier. 1.0 = normal.</summary>
        public abstract float GetAbundance(string resourceId);

        /// <summary>Get material key for a rendering slot (e.g. "wall_above", "terrain").</summary>
        public abstract string GetMaterial(string slot);

        /// <summary>Find resources below a scarcity threshold.</summary>
        public string[] GetScarceResources(string[] resources, float threshold = 0.6f)
        {
            var list = new List<string>();
            foreach (var r in resources)
                if (GetAbundance(r) < threshold) list.Add(r);
            return list.ToArray();
        }

        /// <summary>Find resources above a richness threshold.</summary>
        public string[] GetRichResources(string[] resources, float threshold = 1.2f)
        {
            var list = new List<string>();
            foreach (var r in resources)
                if (GetAbundance(r) > threshold) list.Add(r);
            return list.ToArray();
        }
    }
}
