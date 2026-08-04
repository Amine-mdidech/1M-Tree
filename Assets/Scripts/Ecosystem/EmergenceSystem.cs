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

    private static readonly Dictionary<SpeciesDefinition.SpeciesCategory, Sprite> creatureSpritesByCategory =
        new Dictionary<SpeciesDefinition.SpeciesCategory, Sprite>();

    private float tickTimer;

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

    private void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickIntervalSeconds)
        {
            tickTimer -= tickIntervalSeconds;
            RunEmergenceTick();
        }
    }

    private void RunEmergenceTick()
    {
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

    private void SpawnCreatureVisual(SpeciesDefinition species, IReadOnlyList<PlantedTree> availableTrees)
    {
        Vector3 spawnPosition = GetRandomTreePosition(availableTrees);

        GameObject go = new GameObject($"Creature_{species.id}");
        go.transform.position = spawnPosition;

        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetCreatureSprite(species.category);
        spriteRenderer.sortingOrder = 1;

        float scale = GetCategoryScale(species.category);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        CreatureVisual visual = go.AddComponent<CreatureVisual>();
        visual.Initialize(species, spawnPosition, creatureWanderRadius);

        VfxUtility.SpawnBurst(spawnPosition, GetCategoryColor(species.category), 5, 1.1f, 0.35f);
    }

    private static Vector3 GetRandomTreePosition(IReadOnlyList<PlantedTree> availableTrees)
    {
        if (availableTrees == null || availableTrees.Count == 0)
        {
            return Vector3.zero;
        }

        return availableTrees[Random.Range(0, availableTrees.Count)].transform.position;
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

    private static Sprite GetCreatureSprite(SpeciesDefinition.SpeciesCategory category)
    {
        if (creatureSpritesByCategory.TryGetValue(category, out Sprite cached))
        {
            return cached;
        }

        Sprite sprite = category == SpeciesDefinition.SpeciesCategory.Predator
            ? PixelArtUtility.CreatePredatorSprite(64, GetCategoryColor(category))
            : PixelArtUtility.CreateHerbivoreSprite(64, GetCategoryColor(category));
        creatureSpritesByCategory[category] = sprite;
        return sprite;
    }

    [ContextMenu("Debug: Force Emergence Tick")]
    public void ForceEmergenceTick()
    {
        RunEmergenceTick();
    }
}
