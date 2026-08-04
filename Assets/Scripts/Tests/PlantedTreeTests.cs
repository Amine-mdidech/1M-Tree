using NUnit.Framework;
using UnityEngine;

public class PlantedTreeTests
{
    private GameObject go;
    private PlantedTree tree;
    private VegetationDefinition vegetation;

    [SetUp]
    public void SetUp()
    {
        vegetation = ScriptableObject.CreateInstance<VegetationDefinition>();
        vegetation.id = "test_tree";
        vegetation.displayName = "Test Tree";
        vegetation.growthStages = new[]
        {
            new VegetationDefinition.GrowthStage { stageName = "Seedling", durationSeconds = 10 },
            new VegetationDefinition.GrowthStage { stageName = "Sapling", durationSeconds = 20 },
            new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 30 }
        };

        go = new GameObject("TestTree");
        tree = go.AddComponent<PlantedTree>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(vegetation);
    }

    [Test]
    public void Initialize_StartsAtFirstStageAndNotMature()
    {
        tree.Initialize(vegetation);

        Assert.AreEqual(0, tree.CurrentStageIndex);
        Assert.AreEqual("Seedling", tree.CurrentStageName);
        Assert.IsFalse(tree.IsMature);
    }

    [Test]
    public void AdvanceGrowth_CrossingStageDuration_AdvancesStage()
    {
        tree.Initialize(vegetation);

        tree.AdvanceGrowth(15f); // 10s seedling duration + 5s into sapling

        Assert.AreEqual(1, tree.CurrentStageIndex);
        Assert.AreEqual("Sapling", tree.CurrentStageName);
        Assert.AreEqual(5f, tree.ElapsedSecondsInStage, 0.001f);
    }

    [Test]
    public void AdvanceGrowth_PastAllStages_BecomesMatureAndStopsAdvancing()
    {
        tree.Initialize(vegetation);

        tree.AdvanceGrowth(1000f);

        Assert.IsTrue(tree.IsMature);
        Assert.AreEqual(2, tree.CurrentStageIndex);
    }

    [Test]
    public void GetRemainingGrowthSeconds_SumsAcrossRemainingStages()
    {
        tree.Initialize(vegetation);
        tree.AdvanceGrowth(4f); // 4s into the 10s seedling stage

        float remaining = tree.GetRemainingGrowthSeconds();

        // (10 - 4) seedling remainder + 20 sapling + 30 mature = 56
        Assert.AreEqual(56f, remaining, 0.001f);
    }

    [Test]
    public void GetRemainingGrowthSeconds_WhenMature_IsZero()
    {
        tree.Initialize(vegetation);
        tree.AdvanceGrowth(1000f);

        Assert.AreEqual(0f, tree.GetRemainingGrowthSeconds());
    }

    [Test]
    public void CompleteGrowthInstantly_MakesTreeImmediatelyMature()
    {
        tree.Initialize(vegetation);

        tree.CompleteGrowthInstantly();

        Assert.IsTrue(tree.IsMature);
        Assert.AreEqual(0f, tree.GetRemainingGrowthSeconds());
    }
}
