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
}
