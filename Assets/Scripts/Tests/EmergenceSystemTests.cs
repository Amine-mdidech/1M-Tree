using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EmergenceSystemTests
{
    private GameObject go;
    private EmergenceSystem system;
    private SpeciesDefinition herbivore;
    private SpeciesDefinition predator;
    private GameObject economyManagerGO;
    private EconomyManager economyManager;

    [SetUp]
    public void SetUp()
    {
        EcosystemState.ResetAll();

        herbivore = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivore.id = "test_herbivore";
        herbivore.category = SpeciesDefinition.SpeciesCategory.Herbivore;

        predator = ScriptableObject.CreateInstance<SpeciesDefinition>();
        predator.id = "test_predator";
        predator.category = SpeciesDefinition.SpeciesCategory.Predator;

        go = new GameObject("TestEmergenceSystem");
        system = go.AddComponent<EmergenceSystem>();

        economyManagerGO = new GameObject("TestEconomyManager");
        economyManager = economyManagerGO.AddComponent<EconomyManager>();
        SetPrivateField(system, "economyManager", economyManager);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (CreatureVisual creature in new List<CreatureVisual>(CreatureVisual.ActiveCreatures))
        {
            if (creature != null)
            {
                Object.DestroyImmediate(creature.gameObject);
            }
        }

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(herbivore);
        Object.DestroyImmediate(predator);
        Object.DestroyImmediate(economyManagerGO);
    }

    [Test]
    public void AddUnlockedSpecies_HerbivoreGoesToNonPredatorList()
    {
        system.AddUnlockedSpecies(herbivore);

        Assert.IsTrue(system.IsSpeciesUnlocked("test_herbivore"));
        Assert.AreEqual(1, system.UnlockedSpeciesCount);
    }

    [Test]
    public void AddUnlockedSpecies_PredatorGoesToPredatorList()
    {
        system.AddUnlockedSpecies(predator);

        Assert.IsTrue(system.IsSpeciesUnlocked("test_predator"));
        Assert.IsFalse(system.HasAnyPredatorEmerged()); // unlocked, but population is still 0
    }

    [Test]
    public void AddUnlockedSpecies_Duplicate_DoesNotDoubleCount()
    {
        system.AddUnlockedSpecies(herbivore);
        system.AddUnlockedSpecies(herbivore);

        Assert.AreEqual(1, system.UnlockedSpeciesCount);
    }

    [Test]
    public void GetUnlockedSpeciesIds_ReturnsAllUnlockedAcrossCategories()
    {
        system.AddUnlockedSpecies(herbivore);
        system.AddUnlockedSpecies(predator);

        CollectionAssert.AreEquivalent(new[] { "test_herbivore", "test_predator" }, system.GetUnlockedSpeciesIds());
    }

    [Test]
    public void HasAnyPredatorEmerged_TrueOncePopulationIsPositive()
    {
        system.AddUnlockedSpecies(predator);
        EcosystemState.AddSpeciesIndividual("test_predator");

        Assert.IsTrue(system.HasAnyPredatorEmerged());
    }

    [Test]
    public void SpawnMissingCreatureVisuals_SpawnsOneVisualPerRestoredPopulation()
    {
        system.AddUnlockedSpecies(herbivore);
        EcosystemState.SetSpeciesPopulation("test_herbivore", 3);

        system.SpawnMissingCreatureVisuals();

        Assert.AreEqual(3, CountActiveCreaturesForSpecies("test_herbivore"));
    }

    [Test]
    public void SpawnMissingCreatureVisuals_CalledTwice_DoesNotDuplicateVisuals()
    {
        system.AddUnlockedSpecies(herbivore);
        EcosystemState.SetSpeciesPopulation("test_herbivore", 2);

        system.SpawnMissingCreatureVisuals();
        system.SpawnMissingCreatureVisuals();

        Assert.AreEqual(2, CountActiveCreaturesForSpecies("test_herbivore"));
    }

    [Test]
    public void SpawnMissingCreatureVisuals_ZeroPopulation_SpawnsNothing()
    {
        system.AddUnlockedSpecies(herbivore);

        system.SpawnMissingCreatureVisuals();

        Assert.AreEqual(0, CountActiveCreaturesForSpecies("test_herbivore"));
    }

    [Test]
    public void SpawnMissingCreatureVisuals_CoversPredatorsToo()
    {
        system.AddUnlockedSpecies(predator);
        EcosystemState.SetSpeciesPopulation("test_predator", 1);

        system.SpawnMissingCreatureVisuals();

        Assert.AreEqual(1, CountActiveCreaturesForSpecies("test_predator"));
    }

    [Test]
    public void SpawnMissingCreatureVisuals_PreviouslyCachedNullFrames_SelfHealsWithValidSprite()
    {
        // Reproduces a real bug: if sprite generation ever returns a null frame for a species
        // (transient failure, e.g. mid domain-reload), the old code cached that null result
        // forever via the static creatureFramesBySpeciesId dictionary, permanently rendering
        // every future individual of that species invisible (SpriteRenderer.sprite stays null)
        // until a full Editor restart cleared the static cache.
        SeedBadFramesCache("test_herbivore");

        system.AddUnlockedSpecies(herbivore);
        EcosystemState.SetSpeciesPopulation("test_herbivore", 1);

        system.SpawnMissingCreatureVisuals();

        CreatureVisual creature = null;
        foreach (CreatureVisual candidate in CreatureVisual.ActiveCreatures)
        {
            if (candidate.SpeciesId == "test_herbivore")
            {
                creature = candidate;
                break;
            }
        }

        Assert.IsNotNull(creature, "expected a spawned creature for test_herbivore");
        SpriteRenderer spriteRenderer = creature.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(spriteRenderer.sprite, "cached null frames should have been rebuilt, not reused");
    }

    private static void SeedBadFramesCache(string speciesId)
    {
        FieldInfo field = typeof(EmergenceSystem).GetField("creatureFramesBySpeciesId", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = (Dictionary<string, Sprite[]>)field.GetValue(null);
        cache[speciesId] = new Sprite[] { null, null };
    }

    [Test]
    public void CanRelease_Herbivore_BlockedByPopulationCap()
    {
        herbivore.treesPerIndividualCap = 10;
        EcosystemState.RegisterTreePlanted("oak"); // 1 tree, capacity = 1/10 = 0

        Assert.IsFalse(system.CanRelease(herbivore));
    }

    [Test]
    public void CanRelease_Herbivore_BlockedByMissingRequiredVegetation()
    {
        herbivore.requiredVegetationIds = new[] { "pine" };
        // no vegetation registered

        Assert.IsFalse(system.CanRelease(herbivore));
    }

    [Test]
    public void CanRelease_Herbivore_SatisfiedCriteria_ReturnsTrue()
    {
        herbivore.treesPerIndividualCap = 1;
        EcosystemState.RegisterTreePlanted("oak");

        Assert.IsTrue(system.CanRelease(herbivore));
    }

    [Test]
    public void TryReleaseSpecies_Herbivore_Succeeds_IncrementsPopulation()
    {
        herbivore.treesPerIndividualCap = 1;
        EcosystemState.RegisterTreePlanted("oak");

        bool released = system.TryReleaseSpecies(herbivore);

        Assert.IsTrue(released);
        Assert.AreEqual(1, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void TryReleaseSpecies_UnmetCriteria_ReturnsFalseAndDoesNotChangePopulation()
    {
        herbivore.treesPerIndividualCap = 10;
        EcosystemState.RegisterTreePlanted("oak"); // capacity = 1/10 = 0

        bool released = system.TryReleaseSpecies(herbivore);

        Assert.IsFalse(released);
        Assert.AreEqual(0, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void CanRelease_Predator_BlockedByPreyThreshold()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 1;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 3); // below threshold

        Assert.IsFalse(system.CanRelease(predator));
    }

    [Test]
    public void CanRelease_Predator_BlockedByPreyToPredatorRatio()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 10;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 5); // meets threshold, capacity = 5/10 = 0

        Assert.IsFalse(system.CanRelease(predator));
    }

    [Test]
    public void CanRelease_Predator_SatisfiedCriteria_ReturnsTrue()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 1;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 5); // capacity = 5/1 = 5, 0 < 5

        Assert.IsTrue(system.CanRelease(predator));
    }

    [Test]
    public void TryReleaseSpecies_Predator_Succeeds_IncrementsPopulation()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 1;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 5);

        bool released = system.TryReleaseSpecies(predator);

        Assert.IsTrue(released);
        Assert.AreEqual(1, EcosystemState.GetSpeciesPopulation("test_predator"));
    }

    [Test]
    public void TryReleaseSpeciesAt_Succeeds_IncrementsPopulationAtGivenPosition()
    {
        herbivore.treesPerIndividualCap = 1;
        EcosystemState.RegisterTreePlanted("oak");
        Vector3 targetPosition = new Vector3(3f, 4f, 0f);

        bool released = system.TryReleaseSpeciesAt(herbivore, targetPosition);

        Assert.IsTrue(released);
        Assert.AreEqual(1, EcosystemState.GetSpeciesPopulation("test_herbivore"));

        CreatureVisual creature = null;
        foreach (CreatureVisual candidate in CreatureVisual.ActiveCreatures)
        {
            if (candidate.SpeciesId == "test_herbivore")
            {
                creature = candidate;
                break;
            }
        }

        Assert.IsNotNull(creature, "expected a spawned creature for test_herbivore");
        Assert.AreEqual(targetPosition, creature.transform.position);
    }

    [Test]
    public void TryReleaseSpeciesAt_UnmetCriteria_ReturnsFalseAndDoesNotChangePopulation()
    {
        herbivore.treesPerIndividualCap = 10;
        EcosystemState.RegisterTreePlanted("oak"); // capacity = 1/10 = 0

        bool released = system.TryReleaseSpeciesAt(herbivore, Vector3.zero);

        Assert.IsFalse(released);
        Assert.AreEqual(0, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void TryReleaseSpeciesAt_NullSpecies_ReturnsFalse()
    {
        Assert.IsFalse(system.TryReleaseSpeciesAt(null, Vector3.zero));
    }

    [Test]
    public void TryReleaseSpeciesAt_InsufficientFunds_ReturnsFalseAndDoesNotChangePopulation()
    {
        herbivore.treesPerIndividualCap = 1;
        herbivore.releaseCost = 1000;
        EcosystemState.RegisterTreePlanted("oak");

        bool released = system.TryReleaseSpeciesAt(herbivore, Vector3.zero);

        Assert.IsFalse(released);
        Assert.AreEqual(0, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void TryReleaseSpeciesAt_SufficientFunds_SpendsCost()
    {
        herbivore.treesPerIndividualCap = 1;
        herbivore.releaseCost = 50;
        EcosystemState.RegisterTreePlanted("oak");
        int fundsBefore = economyManager.Funds;

        bool released = system.TryReleaseSpeciesAt(herbivore, Vector3.zero);

        Assert.IsTrue(released);
        Assert.AreEqual(fundsBefore - 50, economyManager.Funds);
    }

    [Test]
    public void CurrentReleasableSpecies_Herbivore_DefaultsToFirstUnlocked()
    {
        system.AddUnlockedSpecies(herbivore);

        Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));
    }

    [Test]
    public void CurrentReleasableSpecies_Predator_DefaultsToFirstUnlocked()
    {
        system.AddUnlockedSpecies(predator);

        Assert.AreEqual(predator, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Predator));
    }

    [Test]
    public void CurrentReleasableSpecies_EmptyCategory_ReturnsNull()
    {
        Assert.IsNull(system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));
    }

    [Test]
    public void CycleReleasableSpecies_Herbivore_WrapsAcrossTwoSpecies()
    {
        SpeciesDefinition herbivore2 = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivore2.id = "test_herbivore2";
        herbivore2.category = SpeciesDefinition.SpeciesCategory.Herbivore;

        system.AddUnlockedSpecies(herbivore);
        system.AddUnlockedSpecies(herbivore2);

        system.CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore);
        Assert.AreEqual(herbivore2, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));

        system.CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore);
        Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));

        Object.DestroyImmediate(herbivore2);
    }

    [Test]
    public void CycleReleasableSpecies_PredatorIndexIndependentOfHerbivoreIndex()
    {
        SpeciesDefinition predator2 = ScriptableObject.CreateInstance<SpeciesDefinition>();
        predator2.id = "test_predator2";
        predator2.category = SpeciesDefinition.SpeciesCategory.Predator;

        system.AddUnlockedSpecies(herbivore);
        system.AddUnlockedSpecies(predator);
        system.AddUnlockedSpecies(predator2);

        system.CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory.Predator);

        Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));
        Assert.AreEqual(predator2, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Predator));

        Object.DestroyImmediate(predator2);
    }

    [Test]
    public void GetSpeciesIconSprite_ReturnsNonNullSprite()
    {
        Sprite icon = EmergenceSystem.GetSpeciesIconSprite(herbivore);

        Assert.IsNotNull(icon);
    }

    [Test]
    public void GetSpeciesIconSprite_NullSpecies_ReturnsNull()
    {
        Assert.IsNull(EmergenceSystem.GetSpeciesIconSprite(null));
    }

    private static int CountActiveCreaturesForSpecies(string speciesId)
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
}
