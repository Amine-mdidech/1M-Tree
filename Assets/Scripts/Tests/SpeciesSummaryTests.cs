using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SpeciesSummaryTests
{
    private VegetationDefinition oak;
    private VegetationDefinition pine;
    private SpeciesDefinition herbivoreWithCap;
    private SpeciesDefinition herbivoreWithRequiredVegetation;
    private SpeciesDefinition predator;

    [SetUp]
    public void SetUp()
    {
        EcosystemState.ResetAll();

        oak = ScriptableObject.CreateInstance<VegetationDefinition>();
        oak.id = "oak";
        oak.displayName = "Oak";
        oak.growthStages = new[] { new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 1 } };

        pine = ScriptableObject.CreateInstance<VegetationDefinition>();
        pine.id = "pine";
        pine.displayName = "Pine";
        pine.growthStages = new[] { new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 1 } };

        herbivoreWithCap = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivoreWithCap.id = "test_herbivore_cap";
        herbivoreWithCap.displayName = "Gazelle";
        herbivoreWithCap.category = SpeciesDefinition.SpeciesCategory.Herbivore;
        herbivoreWithCap.treesPerIndividualCap = 20;

        herbivoreWithRequiredVegetation = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivoreWithRequiredVegetation.id = "test_herbivore_veg";
        herbivoreWithRequiredVegetation.displayName = "Squirrel";
        herbivoreWithRequiredVegetation.category = SpeciesDefinition.SpeciesCategory.Herbivore;
        herbivoreWithRequiredVegetation.requiredVegetationIds = new[] { "pine" };

        predator = ScriptableObject.CreateInstance<SpeciesDefinition>();
        predator.id = "test_predator";
        predator.displayName = "Wolf";
        predator.category = SpeciesDefinition.SpeciesCategory.Predator;
        predator.preySpeciesIds = new[] { "test_herbivore_cap" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 2;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(oak);
        Object.DestroyImmediate(pine);
        Object.DestroyImmediate(herbivoreWithCap);
        Object.DestroyImmediate(herbivoreWithRequiredVegetation);
        Object.DestroyImmediate(predator);

        foreach (PlantedTree tree in Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(tree.gameObject);
        }
    }

    private PlantedTree CreateTestTree(VegetationDefinition vegetation)
    {
        GameObject go = new GameObject("TestTree_" + vegetation.id);
        go.AddComponent<SpriteRenderer>();
        PlantedTree tree = go.AddComponent<PlantedTree>();
        tree.Initialize(vegetation);
        return tree;
    }

    [Test]
    public void GroupTreeCountsByVegetationId_GroupsByType_ExcludesZeroCountTypes()
    {
        List<PlantedTree> trees = new List<PlantedTree>
        {
            CreateTestTree(oak),
            CreateTestTree(oak),
            CreateTestTree(pine)
        };

        Dictionary<string, int> counts = SpeciesSummaryUI.GroupTreeCountsByVegetationId(trees);

        Assert.AreEqual(2, counts.Count);
        Assert.AreEqual(2, counts["oak"]);
        Assert.AreEqual(1, counts["pine"]);
    }

    [Test]
    public void GroupTreeCountsByVegetationId_EmptyList_ReturnsEmptyDictionary()
    {
        Dictionary<string, int> counts = SpeciesSummaryUI.GroupTreeCountsByVegetationId(new List<PlantedTree>());

        Assert.AreEqual(0, counts.Count);
    }

    [Test]
    public void FilterPresentSpecies_OnlyIncludesPositivePopulationOfMatchingCategory()
    {
        EcosystemState.SetSpeciesPopulation("test_herbivore_cap", 3);
        EcosystemState.SetSpeciesPopulation("test_herbivore_veg", 0);
        EcosystemState.SetSpeciesPopulation("test_predator", 1);

        List<SpeciesDefinition> all = new List<SpeciesDefinition> { herbivoreWithCap, herbivoreWithRequiredVegetation, predator };

        List<SpeciesDefinition> herbivores = SpeciesSummaryUI.FilterPresentSpecies(all, SpeciesDefinition.SpeciesCategory.Herbivore);
        List<SpeciesDefinition> predators = SpeciesSummaryUI.FilterPresentSpecies(all, SpeciesDefinition.SpeciesCategory.Predator);

        CollectionAssert.AreEquivalent(new[] { herbivoreWithCap }, herbivores);
        CollectionAssert.AreEquivalent(new[] { predator }, predators);
    }

    [Test]
    public void BuildCriteriaText_Predator_IncludesThresholdAndCurrentPrey()
    {
        EcosystemState.SetSpeciesPopulation("test_herbivore_cap", 3);

        string text = SpeciesSummaryUI.BuildCriteriaText(predator, new Dictionary<string, string>());

        Assert.AreEqual("Needs 5+ prey present (currently 3). Capacity: 1 per 2 prey.", text);
    }

    [Test]
    public void BuildCriteriaText_HerbivoreWithTreeCap_IncludesCurrentTreeCount()
    {
        EcosystemState.RegisterTreePlanted("oak");
        EcosystemState.RegisterTreePlanted("oak");

        string text = SpeciesSummaryUI.BuildCriteriaText(herbivoreWithCap, new Dictionary<string, string>());

        Assert.AreEqual("Needs 20 trees planted per individual (currently 2 trees).", text);
    }

    [Test]
    public void BuildCriteriaText_HerbivoreWithRequiredVegetation_AppendsRequiresList()
    {
        Dictionary<string, string> vegetationNames = new Dictionary<string, string> { { "pine", "Pine" } };

        string text = SpeciesSummaryUI.BuildCriteriaText(herbivoreWithRequiredVegetation, vegetationNames);

        Assert.AreEqual("No tree requirement. Requires: Pine.", text);
    }
}
