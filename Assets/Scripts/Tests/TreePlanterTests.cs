using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TreePlanterTests
{
    private GameObject planterGO;
    private TreePlanter planter;
    private GameObject dailyGrantGO;
    private DailyGrant dailyGrant;
    private VegetationDefinition oak;
    private VegetationDefinition pine;

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

        planterGO = new GameObject("TestTreePlanter");
        planter = planterGO.AddComponent<TreePlanter>();

        dailyGrantGO = new GameObject("TestDailyGrant");
        dailyGrant = dailyGrantGO.AddComponent<DailyGrant>();

        SetPrivateField(planter, "unlockedVegetation", new List<VegetationDefinition> { oak });
        SetPrivateField(planter, "plantableVegetation", new List<VegetationDefinition> { oak, pine });
        SetPrivateField(planter, "dailyGrant", dailyGrant);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (PlantedTree spawnedTree in Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(spawnedTree.gameObject);
        }

        Object.DestroyImmediate(planterGO);
        Object.DestroyImmediate(dailyGrantGO);
        Object.DestroyImmediate(oak);
        Object.DestroyImmediate(pine);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    [Test]
    public void IsVegetationUnlocked_TrueForStartingVegetationOnly()
    {
        Assert.IsTrue(planter.IsVegetationUnlocked("oak"));
        Assert.IsFalse(planter.IsVegetationUnlocked("pine"));
    }

    [Test]
    public void AddUnlockedVegetation_MakesItAvailable()
    {
        planter.AddUnlockedVegetation(pine);

        Assert.IsTrue(planter.IsVegetationUnlocked("pine"));
        CollectionAssert.AreEquivalent(new[] { "oak", "pine" }, planter.GetUnlockedVegetationIds());
    }

    [Test]
    public void AddUnlockedVegetation_Duplicate_DoesNotAddTwice()
    {
        planter.AddUnlockedVegetation(oak);

        Assert.AreEqual(1, planter.GetUnlockedVegetationIds().Count);
    }

    [Test]
    public void CycleVegetation_WrapsAroundUnlockedList()
    {
        planter.AddUnlockedVegetation(pine);

        Assert.AreEqual("oak", planter.CurrentVegetation.id);
        planter.CycleVegetation();
        Assert.AreEqual("pine", planter.CurrentVegetation.id);
        planter.CycleVegetation();
        Assert.AreEqual("oak", planter.CurrentVegetation.id);
    }

    [Test]
    public void PlantTree_ConsumesDailyAllowanceAndSpawnsATree()
    {
        int treesBefore = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;

        planter.PlantTree(Vector3.zero);

        int treesAfter = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(treesBefore + 1, treesAfter);
        Assert.AreEqual(1, dailyGrant.TreesPlantedToday);
    }

    [Test]
    public void PlantTree_StopsAtDailyCap()
    {
        for (int i = 0; i < 30; i++)
        {
            planter.PlantTree(new Vector3(i, 0f, 0f));
        }

        int treesAtCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;

        planter.PlantTree(new Vector3(100f, 0f, 0f));

        int treesAfterOverCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(treesAtCap, treesAfterOverCap);
    }
}
