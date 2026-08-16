using System.Collections.Generic;
using UnityEngine;

public class EmergenceSystem : MonoBehaviour
{
    [System.Serializable]
    public class SpeciesRuntimeState
    {
        public SpeciesDefinition species;
        public int population;
    }

    [SerializeField] private List<SpeciesDefinition> unlockedNonPredatorSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<SpeciesDefinition> unlockedPredatorSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<SpeciesRuntimeState> populations = new List<SpeciesRuntimeState>();
    [SerializeField] private float creatureWanderRadius = 2f;
    [SerializeField] private int currentHerbivoreIndex;
    [SerializeField] private int currentPredatorIndex;
    [SerializeField] private EconomyManager economyManager;

    // Index 0 / 1 = the two walk-cycle frames for that species, swapped by CreatureVisual while moving.
    private static readonly Dictionary<string, Sprite[]> creatureFramesBySpeciesId =
        new Dictionary<string, Sprite[]>();

    public int UnlockedSpeciesCount => unlockedNonPredatorSpecies.Count + unlockedPredatorSpecies.Count;

    public bool HasAnyPredatorEmerged()
    {
        foreach (SpeciesDefinition predator in unlockedPredatorSpecies)
        {
            if (predator != null && EcosystemState.GetSpeciesPopulation(predator.id) > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSpeciesUnlocked(string speciesId)
    {
        foreach (SpeciesDefinition species in unlockedNonPredatorSpecies)
        {
            if (species != null && species.id == speciesId)
            {
                return true;
            }
        }

        foreach (SpeciesDefinition species in unlockedPredatorSpecies)
        {
            if (species != null && species.id == speciesId)
            {
                return true;
            }
        }

        return false;
    }

    public void AddUnlockedSpecies(SpeciesDefinition species)
    {
        if (species == null || IsSpeciesUnlocked(species.id))
        {
            return;
        }

        if (species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            unlockedPredatorSpecies.Add(species);
        }
        else
        {
            unlockedNonPredatorSpecies.Add(species);
        }
    }

    public List<string> GetUnlockedSpeciesIds()
    {
        List<string> ids = new List<string>();

        foreach (SpeciesDefinition species in unlockedNonPredatorSpecies)
        {
            if (species != null)
            {
                ids.Add(species.id);
            }
        }

        foreach (SpeciesDefinition species in unlockedPredatorSpecies)
        {
            if (species != null)
            {
                ids.Add(species.id);
            }
        }

        return ids;
    }

    // Deterministic replacement for the old per-tick probability roll: given the same criteria
    // (prey population for predators, tree-count-derived capacity for herbivores), this either
    // is or isn't currently satisfied - no randomness. baseEmergenceWeight is intentionally not
    // read here; it only ever fed the removed probability roll.
    public bool CanRelease(SpeciesDefinition species)
    {
        if (species == null)
        {
            return false;
        }

        if (species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            int totalPrey = 0;
            if (species.preySpeciesIds != null)
            {
                foreach (string preyId in species.preySpeciesIds)
                {
                    totalPrey += EcosystemState.GetSpeciesPopulation(preyId);
                }
            }

            int predatorCapacity = Mathf.FloorToInt(totalPrey / (float)species.preyToPredatorRatio);
            int currentPredatorPopulation = EcosystemState.GetSpeciesPopulation(species.id);

            return totalPrey >= species.preyThreshold && currentPredatorPopulation < predatorCapacity;
        }

        if (!IsVegetationSatisfied(species))
        {
            return false;
        }

        if (species.treesPerIndividualCap > 0)
        {
            int populationCapacity = EcosystemState.TotalTreeCount / species.treesPerIndividualCap;
            return EcosystemState.GetSpeciesPopulation(species.id) < populationCapacity;
        }

        return true;
    }

    // Player-triggered release: succeeds or fails immediately based on CanRelease, with the same
    // feedback tree-planting already gives (sound + VFX + XP). The VFX burst comes from
    // SpawnIndividual's call into SpawnCreatureVisual (playSpawnEffects defaults to true) - don't
    // add a second one here.
    public bool TryReleaseSpecies(SpeciesDefinition species)
    {
        if (!CanRelease(species))
        {
            return false;
        }

        SpawnIndividual(species, GetRandomTreePosition(PlantedTree.ActiveTrees));
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        return true;
    }

    // Position-aware counterpart of TryReleaseSpecies for the HUD's tap-to-place picker: releases
    // at the tapped world position instead of a random tree, with the same land/river rejection
    // feedback PlantTree gives. Deliberately skips the Lab-exclusion-zone and tree-spacing checks
    // PlantTree has - neither applies to animals.
    public bool TryReleaseSpeciesAt(SpeciesDefinition species, Vector3 position)
    {
        if (species == null)
        {
            return false;
        }

        if (LandManager.Instance != null && !LandManager.Instance.IsWithinOwnedLand(position))
        {
            Debug.Log("Can't release here — outside your owned land.");
            AudioManager.Instance?.PlayError();
            HudUI.Instance?.ShowLandLockedPopup();
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsBlocked(position))
        {
            Debug.Log("Can't release here — that's the river.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsWithinSandBank(position))
        {
            Debug.Log("Can't release here — that's the sand bank.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        if (!CanRelease(species))
        {
            Debug.Log($"Can't release {species.displayName} yet — criteria not met.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        if (economyManager != null && !economyManager.TrySpend(species.releaseCost))
        {
            Debug.Log($"Can't release {species.displayName} yet — not enough funds (need {species.releaseCost}, have {economyManager.Funds}).");
            AudioManager.Instance?.PlayError();
            return false;
        }

        SpawnIndividual(species, position);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        return true;
    }

    // Mirrors TreePlanter.CurrentVegetation/CycleVegetation for animal species, split per category
    // (Predator vs. everything else) so switching category and back restores the prior pick there,
    // rather than always resetting to index 0. Any non-Predator category maps to the non-predator
    // list, matching AddUnlockedSpecies's existing category split.
    public SpeciesDefinition CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory category)
    {
        List<SpeciesDefinition> list = ListForCategory(category);
        if (list.Count == 0)
        {
            return null;
        }

        int index = IndexForCategory(category);
        return list[index % list.Count];
    }

    public void CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory category)
    {
        List<SpeciesDefinition> list = ListForCategory(category);
        if (list.Count == 0)
        {
            return;
        }

        if (category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            currentPredatorIndex = (currentPredatorIndex + 1) % list.Count;
        }
        else
        {
            currentHerbivoreIndex = (currentHerbivoreIndex + 1) % list.Count;
        }
    }

    private List<SpeciesDefinition> ListForCategory(SpeciesDefinition.SpeciesCategory category)
    {
        return category == SpeciesDefinition.SpeciesCategory.Predator
            ? unlockedPredatorSpecies
            : unlockedNonPredatorSpecies;
    }

    private int IndexForCategory(SpeciesDefinition.SpeciesCategory category)
    {
        return category == SpeciesDefinition.SpeciesCategory.Predator
            ? currentPredatorIndex
            : currentHerbivoreIndex;
    }

    private static bool IsVegetationSatisfied(SpeciesDefinition species)
    {
        if (species.requiredVegetationIds == null || species.requiredVegetationIds.Length == 0)
        {
            return true;
        }

        foreach (string requiredId in species.requiredVegetationIds)
        {
            if (!EcosystemState.HasVegetationType(requiredId))
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnIndividual(SpeciesDefinition species, Vector3 position)
    {
        EcosystemState.AddSpeciesIndividual(species.id);
        int newPopulation = EcosystemState.GetSpeciesPopulation(species.id);

        SpeciesRuntimeState runtimeState = FindRuntimeState(species);
        if (runtimeState == null)
        {
            runtimeState = new SpeciesRuntimeState { species = species };
            populations.Add(runtimeState);
        }

        runtimeState.population = newPopulation;

        Debug.Log($"{species.displayName} emerged! Population is now {newPopulation}.");

        SpawnCreatureVisual(species, position);
    }

    private SpeciesRuntimeState FindRuntimeState(SpeciesDefinition species)
    {
        foreach (SpeciesRuntimeState state in populations)
        {
            if (state.species == species)
            {
                return state;
            }
        }

        return null;
    }

    private void SpawnCreatureVisual(SpeciesDefinition species, Vector3 spawnPosition, bool playSpawnEffects = true)
    {
        GameObject go = new GameObject($"Creature_{species.id}");
        go.transform.position = spawnPosition;

        Sprite[] frames = GetCreatureFrames(species);

        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingOrder = 1;

        float scale = GetSpeciesScale(species);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        CreatureVisual visual = go.AddComponent<CreatureVisual>();
        visual.Initialize(species, spawnPosition, creatureWanderRadius, frames);

        if (playSpawnEffects)
        {
            VfxUtility.SpawnBurst(spawnPosition, GetSpeciesColor(species), 5, 1.1f, 0.35f);
        }
    }

    // SaveManager restores EcosystemState's population *numbers* on load, but never the
    // CreatureVisual GameObjects those numbers represent (a fresh scene has none). Without this,
    // a species already at its population cap can never pass RunEmergenceTick's cap check again
    // (currentPopulation >= populationCapacity), so it would silently stay invisible forever after
    // a reload despite still counting toward its population. Called once after SaveManager finishes
    // restoring trees and population counts; no spawn VFX/sound, since these aren't new arrivals.
    public void SpawnMissingCreatureVisuals()
    {
        IReadOnlyList<PlantedTree> availableTrees = PlantedTree.ActiveTrees;

        SpawnMissingForList(unlockedNonPredatorSpecies, availableTrees);
        SpawnMissingForList(unlockedPredatorSpecies, availableTrees);
    }

    private void SpawnMissingForList(List<SpeciesDefinition> speciesList, IReadOnlyList<PlantedTree> availableTrees)
    {
        foreach (SpeciesDefinition species in speciesList)
        {
            if (species == null)
            {
                continue;
            }

            int targetPopulation = EcosystemState.GetSpeciesPopulation(species.id);
            int existingVisualCount = CountActiveCreatures(species.id);

            for (int i = existingVisualCount; i < targetPopulation; i++)
            {
                SpawnCreatureVisual(species, GetRandomTreePosition(availableTrees), playSpawnEffects: false);
            }
        }
    }

    private static int CountActiveCreatures(string speciesId)
    {
        int count = 0;
        foreach (CreatureVisual creature in CreatureVisual.ActiveCreatures)
        {
            if (creature.SpeciesId == speciesId)
            {
                count++;
            }
        }

        return count;
    }

    private static Vector3 GetRandomTreePosition(IReadOnlyList<PlantedTree> availableTrees)
    {
        if (availableTrees == null || availableTrees.Count == 0)
        {
            return Vector3.zero;
        }

        // Retry a bounded number of times rather than indexing once: ActiveTrees can transiently
        // hold a destroyed entry (Destroy()/DestroyImmediate() don't always invoke OnDestroy
        // synchronously), and a random pick landing on one would otherwise throw and abort the
        // whole emergence tick.
        const int maxAttempts = 8;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            PlantedTree candidate = availableTrees[Random.Range(0, availableTrees.Count)];
            if (candidate != null)
            {
                return candidate.transform.position;
            }
        }

        return Vector3.zero;
    }

    private static Color GetSpeciesColor(SpeciesDefinition species)
    {
        switch (species.id)
        {
            case "gazelle":
                return new Color(0.82f, 0.62f, 0.35f);
            case "wolf":
                return new Color(0.55f, 0.56f, 0.58f);
            case "fox":
                return new Color(0.78f, 0.4f, 0.2f);
            case "squirrel":
                return new Color(0.62f, 0.38f, 0.2f);
            default:
                return GetCategoryColor(species.category);
        }
    }

    private static Color GetCategoryColor(SpeciesDefinition.SpeciesCategory category)
    {
        switch (category)
        {
            case SpeciesDefinition.SpeciesCategory.Insect:
                return new Color(1f, 0.85f, 0.05f);
            case SpeciesDefinition.SpeciesCategory.Herbivore:
                return new Color(0.85f, 0.58f, 0.22f);
            case SpeciesDefinition.SpeciesCategory.Predator:
                return new Color(0.72f, 0.08f, 0.08f);
            default:
                return Color.white;
        }
    }

    private static float GetCategoryScale(SpeciesDefinition.SpeciesCategory category)
    {
        switch (category)
        {
            case SpeciesDefinition.SpeciesCategory.Insect:
                return 0.35f;
            case SpeciesDefinition.SpeciesCategory.Herbivore:
                return 0.7f;
            case SpeciesDefinition.SpeciesCategory.Predator:
                return 0.875f;
            default:
                return 0.525f;
        }
    }

    // Per-species size multiplier on top of the category baseline, for the handful of species
    // whose real-world size difference is too big to ignore (an elephant next to a rabbit).
    private static float GetSpeciesScale(SpeciesDefinition species)
    {
        float baseScale = GetCategoryScale(species.category);
        switch (species.id)
        {
            case "fox":
                return baseScale * 0.85f;
            case "squirrel":
                return baseScale * 0.6f;
            default:
                return baseScale;
        }
    }

    private static Sprite[] GetCreatureFrames(SpeciesDefinition species)
    {
        if (creatureFramesBySpeciesId.TryGetValue(species.id, out Sprite[] cached) && IsValidFrames(cached))
        {
            return cached;
        }

        Sprite[] frames = BuildCreatureFrames(species);

        // Only cache a genuinely usable result. If sprite generation ever returns a null frame
        // (transient failure), caching it anyway would permanently render every future individual
        // of this species invisible - the static dictionary otherwise never gets a chance to
        // self-heal until a full domain reload clears it.
        if (IsValidFrames(frames))
        {
            creatureFramesBySpeciesId[species.id] = frames;
        }

        return frames;
    }

    // Exposes the same procedurally-generated creature sprite CreatureVisual renders in the world,
    // for use as a small icon elsewhere (e.g. the species summary panel) without needing a
    // duplicate, dedicated icon asset per species.
    public static Sprite GetSpeciesIconSprite(SpeciesDefinition species)
    {
        if (species == null)
        {
            return null;
        }

        return GetCreatureFrames(species)[0];
    }

    private static bool IsValidFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 && frames[0] != null;
    }

    private static Sprite[] BuildCreatureFrames(SpeciesDefinition species)
    {
        PixelArtUtility.QuadrupedTraits traits = GetQuadrupedTraits(species);
        return new[]
        {
            PixelArtUtility.CreateQuadrupedSprite(64, traits, 0),
            PixelArtUtility.CreateQuadrupedSprite(64, traits, 1)
        };
    }

    // Every species gets its own trait combination so it's identifiable at a glance rather than
    // sharing one of two generic category silhouettes. Falls back to a neutral combination (still
    // correctly colored/shaped for its category) for any future species added without an entry here.
    private static PixelArtUtility.QuadrupedTraits GetQuadrupedTraits(SpeciesDefinition species)
    {
        Color color = GetSpeciesColor(species);

        switch (species.id)
        {
            case "gazelle":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.ThinHorns, tailStyle = PixelArtUtility.AnimalTailStyle.Nub };
            case "wolf":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy, hasFangs = true };
            case "fox":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy, hasFangs = true };
            case "squirrel":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy };
            default:
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Nub, hasFangs = species.category == SpeciesDefinition.SpeciesCategory.Predator };
        }
    }
}
