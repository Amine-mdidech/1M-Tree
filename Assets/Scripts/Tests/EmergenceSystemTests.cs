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
    public void AdvanceTicks_ElapsedTimeLessThanOneInterval_DoesNotRunTick()
    {
        system.AdvanceTicks(30f); // default tick interval is 60s

        Assert.AreEqual(0, system.EmergenceTickCount);
    }

    [Test]
    public void AdvanceTicks_ElapsedTimeCoversMultipleIntervals_RunsThatManyTicksAtOnce()
    {
        system.AdvanceTicks(150f); // 150 / 60 = 2 full ticks, 30s remainder

        Assert.AreEqual(2, system.EmergenceTickCount);
    }

    [Test]
    public void AdvanceTicks_RemainderFromAnEarlierCallCarriesOverInsteadOfBeingLost()
    {
        // Simulates two separate short sessions (e.g. app closed and reopened between them):
        // neither call alone reaches a full interval, but together they should.
        system.AdvanceTicks(45f);
        system.AdvanceTicks(20f); // 45 + 20 = 65s >= 60s

        Assert.AreEqual(1, system.EmergenceTickCount);
    }

    [Test]
    public void AdvanceTicks_ZeroTickInterval_DoesNotHangAndRunsNoTicks()
    {
        // A tick interval of 0 (e.g. an Inspector edit dragged past zero) must not spin forever:
        // tickTimer -= 0 never shrinks, so a naive while-loop would hang the whole Editor.
        SetTickIntervalSeconds(0f);

        system.AdvanceTicks(1000f);

        Assert.AreEqual(0, system.EmergenceTickCount);
    }

    [Test]
    public void AdvanceTicks_NegativeTickInterval_DoesNotHangAndRunsNoTicks()
    {
        // A negative interval makes tickTimer -= tickIntervalSeconds grow every iteration - even
        // more catastrophic than zero if unguarded.
        SetTickIntervalSeconds(-1f);

        system.AdvanceTicks(1000f);

        Assert.AreEqual(0, system.EmergenceTickCount);
    }

    private void SetTickIntervalSeconds(float value)
    {
        FieldInfo field = typeof(EmergenceSystem).GetField("tickIntervalSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(system, value);
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
