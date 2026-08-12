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

    private const float DefaultTickIntervalSeconds = 60f;
    private const int TreeCountDivisor = 50;
    private const float VegetationDiversityStep = 0.1f;
    private const float NonPredatorChanceCeiling = 0.8f;
    private const float PredatorChanceCeiling = 0.5f;
    private const float PredatorPreyRatioCeiling = 2f;

    [SerializeField] private List<SpeciesDefinition> unlockedNonPredatorSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<SpeciesDefinition> unlockedPredatorSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<SpeciesRuntimeState> populations = new List<SpeciesRuntimeState>();
    [SerializeField] private float tickIntervalSeconds = DefaultTickIntervalSeconds;
    [SerializeField] private float creatureWanderRadius = 2f;

    // Index 0 / 1 = the two walk-cycle frames for that species, swapped by CreatureVisual while moving.
    private static readonly Dictionary<string, Sprite[]> creatureFramesBySpeciesId =
        new Dictionary<string, Sprite[]>();

    private float tickTimer;

    public int UnlockedSpeciesCount => unlockedNonPredatorSpecies.Count + unlockedPredatorSpecies.Count;

    // How many emergence ticks have actually run, regardless of whether any of them rolled a
    // spawn. Lets tests/debugging verify catch-up ticks fired without depending on Random.value.
    public int EmergenceTickCount { get; private set; }

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

    private void Update()
    {
        AdvanceTicks(Time.deltaTime);
    }

    // Routes both live per-frame ticking and offline/short-session catch-up (see SaveManager,
    // which calls this once on load with the real-world seconds elapsed since the app last
    // closed) through the same accounting, so partial progress toward the next tick is never
    // silently discarded across a session boundary the way a session-local, reset-on-launch
    // timer would discard it.
    public void AdvanceTicks(float elapsedSeconds)
    {
        int ticksToRun = ConsumeTicks(elapsedSeconds);
        for (int i = 0; i < ticksToRun; i++)
        {
            RunEmergenceTick();
        }
    }

    private int ConsumeTicks(float elapsedSeconds)
    {
        // A zero or negative interval (e.g. an Inspector value dragged past zero) would otherwise
        // spin the loop below forever: tickTimer -= tickIntervalSeconds never shrinks tickTimer
        // when tickIntervalSeconds <= 0, so the condition never goes false. That hangs the whole
        // Editor, since this runs on the main thread every frame.
        if (tickIntervalSeconds <= 0f)
        {
            return 0;
        }

        tickTimer += elapsedSeconds;

        int ticksToRun = 0;
        while (tickTimer >= tickIntervalSeconds)
        {
            tickTimer -= tickIntervalSeconds;
            ticksToRun++;
        }

        return ticksToRun;
    }

    private void RunEmergenceTick()
    {
        EmergenceTickCount++;

        int currentTreeCount = EcosystemState.TotalTreeCount;
        int distinctVegetationTypesPlanted = EcosystemState.DistinctVegetationTypeCount;
        float vegetationDiversityMultiplier = 1f + (distinctVegetationTypesPlanted - 1) * VegetationDiversityStep;

        IReadOnlyList<PlantedTree> treesThisTick = PlantedTree.ActiveTrees;

        foreach (SpeciesDefinition species in unlockedNonPredatorSpecies)
        {
            if (species == null || species.category == SpeciesDefinition.SpeciesCategory.Predator)
            {
                continue;
            }

            if (!IsVegetationSatisfied(species))
            {
                continue;
            }

            if (species.treesPerIndividualCap > 0)
            {
                int populationCapacity = currentTreeCount / species.treesPerIndividualCap;
                if (EcosystemState.GetSpeciesPopulation(species.id) >= populationCapacity)
                {
                    continue;
                }
            }

            float emergenceChance = species.baseEmergenceWeight * (currentTreeCount / (float)TreeCountDivisor) * vegetationDiversityMultiplier;
            emergenceChance = Mathf.Clamp(emergenceChance, 0f, NonPredatorChanceCeiling);

            if (Random.value < emergenceChance)
            {
                SpawnIndividual(species, treesThisTick);
            }
        }

        RunPredatorEmergence(treesThisTick);
    }

    private void RunPredatorEmergence(IReadOnlyList<PlantedTree> treesThisTick)
    {
        foreach (SpeciesDefinition predator in unlockedPredatorSpecies)
        {
            if (predator == null || predator.category != SpeciesDefinition.SpeciesCategory.Predator)
            {
                continue;
            }

            int totalPrey = 0;
            if (predator.preySpeciesIds != null)
            {
                foreach (string preyId in predator.preySpeciesIds)
                {
                    totalPrey += EcosystemState.GetSpeciesPopulation(preyId);
                }
            }

            int predatorCapacity = Mathf.FloorToInt(totalPrey / (float)predator.preyToPredatorRatio);
            int currentPredatorPopulation = EcosystemState.GetSpeciesPopulation(predator.id);

            float emergenceChance = 0f;
            if (totalPrey >= predator.preyThreshold && currentPredatorPopulation < predatorCapacity)
            {
                emergenceChance = predator.baseEmergenceWeight * Mathf.Min(totalPrey / (float)predator.preyThreshold, PredatorPreyRatioCeiling);
                emergenceChance = Mathf.Clamp(emergenceChance, 0f, PredatorChanceCeiling);
            }

            if (emergenceChance > 0f && Random.value < emergenceChance)
            {
                SpawnIndividual(predator, treesThisTick);
            }
        }
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

    private void SpawnIndividual(SpeciesDefinition species, IReadOnlyList<PlantedTree> availableTrees)
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

        SpawnCreatureVisual(species, availableTrees);
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

    private void SpawnCreatureVisual(SpeciesDefinition species, IReadOnlyList<PlantedTree> availableTrees, bool playSpawnEffects = true)
    {
        Vector3 spawnPosition = GetRandomTreePosition(availableTrees);

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
                SpawnCreatureVisual(species, availableTrees, playSpawnEffects: false);
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
            case "rabbit":
                return new Color(0.78f, 0.68f, 0.55f);
            case "hare":
                return new Color(0.68f, 0.52f, 0.32f);
            case "goat":
                return new Color(0.85f, 0.83f, 0.78f);
            case "antelope":
                return new Color(0.75f, 0.5f, 0.3f);
            case "deer":
                return new Color(0.6f, 0.4f, 0.25f);
            case "zebra":
                return new Color(0.88f, 0.86f, 0.82f);
            case "boar":
                return new Color(0.4f, 0.32f, 0.28f);
            case "buffalo":
                return new Color(0.32f, 0.28f, 0.24f);
            case "moose":
                return new Color(0.35f, 0.28f, 0.2f);
            case "giraffe":
                return new Color(0.85f, 0.65f, 0.3f);
            case "elephant":
                return new Color(0.55f, 0.55f, 0.58f);
            case "lion":
                return new Color(0.75f, 0.58f, 0.28f);
            case "fox":
                return new Color(0.78f, 0.4f, 0.2f);
            case "eagle":
                return new Color(0.5f, 0.4f, 0.3f);
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
            case "elephant":
                return baseScale * 1.6f;
            case "giraffe":
                return baseScale * 1.3f;
            case "rabbit":
            case "hare":
                return baseScale * 0.6f;
            case "fox":
                return baseScale * 0.85f;
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

    private static bool IsValidFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 && frames[0] != null;
    }

    private static Sprite[] BuildCreatureFrames(SpeciesDefinition species)
    {
        if (species.id == "eagle")
        {
            Color birdColor = GetSpeciesColor(species);
            return new[]
            {
                PixelArtUtility.CreateBirdSprite(64, birdColor, 0),
                PixelArtUtility.CreateBirdSprite(64, birdColor, 1)
            };
        }

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
            case "rabbit":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Long, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Tuft };
            case "hare":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Long, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Tuft };
            case "goat":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.CurvedHorns, tailStyle = PixelArtUtility.AnimalTailStyle.Nub };
            case "antelope":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.ThinHorns, tailStyle = PixelArtUtility.AnimalTailStyle.Nub };
            case "deer":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.Antlers, tailStyle = PixelArtUtility.AnimalTailStyle.Nub, pattern = PixelArtUtility.AnimalPattern.Spots, patternColor = Color.white };
            case "zebra":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Long, pattern = PixelArtUtility.AnimalPattern.Stripes, patternColor = new Color(0.12f, 0.1f, 0.08f) };
            case "boar":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.Tusks, tailStyle = PixelArtUtility.AnimalTailStyle.Nub };
            case "buffalo":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.CurvedHorns, tailStyle = PixelArtUtility.AnimalTailStyle.Long };
            case "moose":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.Antlers, tailStyle = PixelArtUtility.AnimalTailStyle.Nub };
            case "giraffe":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.ThinHorns, tailStyle = PixelArtUtility.AnimalTailStyle.Long, pattern = PixelArtUtility.AnimalPattern.Spots, patternColor = new Color(0.55f, 0.35f, 0.15f) };
            case "elephant":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Flappy, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.Tusks, tailStyle = PixelArtUtility.AnimalTailStyle.Long };
            case "lion":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Tuft, hasMane = true, hasFangs = true };
            case "fox":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy, hasFangs = true };
            default:
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Nub, hasFangs = species.category == SpeciesDefinition.SpeciesCategory.Predator };
        }
    }

    [ContextMenu("Debug: Force Emergence Tick")]
    public void ForceEmergenceTick()
    {
        RunEmergenceTick();
    }
}
