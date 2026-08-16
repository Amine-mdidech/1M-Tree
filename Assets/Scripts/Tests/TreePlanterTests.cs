using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TreePlanterTests
{
    private GameObject planterGO;
    private TreePlanter planter;
    private GameObject economyManagerGO;
    private EconomyManager economyManager;
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

        economyManagerGO = new GameObject("TestEconomyManager");
        economyManager = economyManagerGO.AddComponent<EconomyManager>();

        oak.plantCost = 10;
        pine.plantCost = 10;

        SetPrivateField(planter, "unlockedVegetation", new List<VegetationDefinition> { oak });
        SetPrivateField(planter, "plantableVegetation", new List<VegetationDefinition> { oak, pine });
        SetPrivateField(planter, "economyManager", economyManager);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (PlantedTree spawnedTree in Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(spawnedTree.gameObject);
        }

        Object.DestroyImmediate(planterGO);
        Object.DestroyImmediate(economyManagerGO);
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
    public void SelectVegetation_SetsCurrentVegetationToMatch()
    {
        planter.AddUnlockedVegetation(pine);

        planter.SelectVegetation(pine);

        Assert.AreEqual("pine", planter.CurrentVegetation.id);
    }

    [Test]
    public void SelectVegetation_NotUnlocked_LeavesCurrentVegetationUnchanged()
    {
        planter.SelectVegetation(pine); // pine not yet unlocked

        Assert.AreEqual("oak", planter.CurrentVegetation.id);
    }

    [Test]
    public void PlantTree_SpendsFundsAndSpawnsATree()
    {
        int treesBefore = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        int fundsBefore = economyManager.Funds;

        planter.PlantTree(Vector3.zero);

        int treesAfter = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(treesBefore + 1, treesAfter);
        Assert.AreEqual(fundsBefore - oak.plantCost, economyManager.Funds);
    }

    [Test]
    public void PlantTree_StopsWhenFundsRunOut()
    {
        // EconomyManager starts with 300 Funds and oak costs 10, so exactly 30 plants are
        // affordable before funds run out. Spaced well past the minimum tree-to-tree distance so
        // this loop exercises running out of funds specifically, not the spacing rule.
        for (int i = 0; i < 30; i++)
        {
            planter.PlantTree(new Vector3(i * 10f, 0f, 0f));
        }

        int treesAtCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(0, economyManager.Funds);

        planter.PlantTree(new Vector3(1000f, 0f, 0f));

        int treesAfterOverCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(treesAtCap, treesAfterOverCap);
    }

    [Test]
    public void PlantTree_TooCloseToExistingTree_IsRejected()
    {
        planter.PlantTree(Vector3.zero);

        planter.PlantTree(new Vector3(0.1f, 0f, 0f));

        int treeCount = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(1, treeCount);
    }

    [Test]
    public void PlantTree_FarEnoughFromExistingTree_Succeeds()
    {
        planter.PlantTree(Vector3.zero);

        planter.PlantTree(new Vector3(10f, 0f, 0f));

        int treeCount = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
        Assert.AreEqual(2, treeCount);
    }

    [Test]
    public void CanPlantAt_TooCloseToExistingTree_ReturnsFalse()
    {
        planter.PlantTree(Vector3.zero);

        Assert.IsFalse(planter.CanPlantAt(new Vector3(0.1f, 0f, 0f)));
    }

    [Test]
    public void CanPlantAt_FarFromExistingTree_ReturnsTrue()
    {
        planter.PlantTree(Vector3.zero);

        Assert.IsTrue(planter.CanPlantAt(new Vector3(10f, 0f, 0f)));
    }

    [Test]
    public void CanPlantAt_NoTreesPlantedYet_ReturnsTrue()
    {
        Assert.IsTrue(planter.CanPlantAt(Vector3.zero));
    }

    [Test]
    public void CanMoveTreeTo_OwnCurrentPosition_ReturnsTrue()
    {
        planter.PlantTree(Vector3.zero);
        PlantedTree tree = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude)[0];

        // The tree is excluded from its own spacing check, so "moving" it onto the spot it
        // already occupies must not be rejected as "too close to an existing tree".
        Assert.IsTrue(planter.CanMoveTreeTo(tree, Vector3.zero));
    }

    [Test]
    public void CanMoveTreeTo_TooCloseToAnotherTree_ReturnsFalse()
    {
        planter.PlantTree(Vector3.zero);
        planter.PlantTree(new Vector3(10f, 0f, 0f));
        PlantedTree treeToMove = FindTreeNearestTo(new Vector3(10f, 0f, 0f));

        Assert.IsFalse(planter.CanMoveTreeTo(treeToMove, new Vector3(0.1f, 0f, 0f)));
    }

    [Test]
    public void CanMoveTreeTo_FarFromOtherTrees_ReturnsTrue()
    {
        planter.PlantTree(Vector3.zero);
        PlantedTree tree = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude)[0];

        Assert.IsTrue(planter.CanMoveTreeTo(tree, new Vector3(20f, 0f, 0f)));
    }

    [Test]
    public void MoveTree_ValidDestination_RelocatesTreeAndDoesNotSpendFunds()
    {
        planter.PlantTree(Vector3.zero);
        PlantedTree tree = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude)[0];
        int fundsBefore = economyManager.Funds;

        bool moved = planter.MoveTree(tree, new Vector3(20f, 0f, 0f));

        Assert.IsTrue(moved);
        Assert.AreEqual(new Vector3(20f, 0f, 0f), tree.transform.position);
        Assert.AreEqual(fundsBefore, economyManager.Funds);
    }

    [Test]
    public void MoveTree_InvalidDestination_LeavesTreeInPlace()
    {
        planter.PlantTree(Vector3.zero);
        planter.PlantTree(new Vector3(10f, 0f, 0f));
        PlantedTree treeToMove = FindTreeNearestTo(new Vector3(10f, 0f, 0f));

        bool moved = planter.MoveTree(treeToMove, new Vector3(0.1f, 0f, 0f));

        Assert.IsFalse(moved);
        Assert.AreEqual(new Vector3(10f, 0f, 0f), treeToMove.transform.position);
    }

    private static PlantedTree FindTreeNearestTo(Vector3 position)
    {
        PlantedTree closest = null;
        float closestDistance = float.MaxValue;

        foreach (PlantedTree tree in Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude))
        {
            float distance = Vector3.Distance(tree.transform.position, position);
            if (distance < closestDistance)
            {
                closest = tree;
                closestDistance = distance;
            }
        }

        return closest;
    }
}
