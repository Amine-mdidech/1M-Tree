using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ResearchManagerTests
{
    private GameObject researchGO;
    private ResearchManager researchManager;
    private GameObject emergenceGO;
    private EmergenceSystem emergenceSystem;
    private GameObject planterGO;
    private TreePlanter treePlanter;

    private VegetationDefinition wildflower;
    private VegetationDefinition birch;
    private ResearchProjectDefinition wildflowerResearch;
    private ResearchProjectDefinition birchResearch;

    [SetUp]
    public void SetUp()
    {
        wildflower = ScriptableObject.CreateInstance<VegetationDefinition>();
        wildflower.id = "wildflower";
        wildflower.growthStages = new[] { new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 1 } };

        birch = ScriptableObject.CreateInstance<VegetationDefinition>();
        birch.id = "birch";
        birch.growthStages = new[] { new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 1 } };

        wildflowerResearch = ScriptableObject.CreateInstance<ResearchProjectDefinition>();
        wildflowerResearch.id = "wildflower_research";
        wildflowerResearch.unlocksVegetationId = "wildflower";
        wildflowerResearch.baseDurationSeconds = 100;
        wildflowerResearch.prerequisiteProjectIds = new string[0];

        birchResearch = ScriptableObject.CreateInstance<ResearchProjectDefinition>();
        birchResearch.id = "birch_research";
        birchResearch.unlocksVegetationId = "birch";
        birchResearch.baseDurationSeconds = 100;
        birchResearch.prerequisiteProjectIds = new[] { "wildflower_research" };

        emergenceGO = new GameObject("TestEmergenceSystem");
        emergenceSystem = emergenceGO.AddComponent<EmergenceSystem>();

        planterGO = new GameObject("TestTreePlanter");
        treePlanter = planterGO.AddComponent<TreePlanter>();
        SetPrivateField(treePlanter, "unlockedVegetation", new List<VegetationDefinition>());
        SetPrivateField(treePlanter, "plantableVegetation", new List<VegetationDefinition> { wildflower, birch });

        researchGO = new GameObject("TestResearchManager");
        researchManager = researchGO.AddComponent<ResearchManager>();
        SetPrivateField(researchManager, "availableResearchProjects", new List<ResearchProjectDefinition> { wildflowerResearch, birchResearch });
        SetPrivateField(researchManager, "emergenceSystem", emergenceSystem);
        SetPrivateField(researchManager, "treePlanter", treePlanter);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(researchGO);
        Object.DestroyImmediate(emergenceGO);
        Object.DestroyImmediate(planterGO);
        Object.DestroyImmediate(wildflower);
        Object.DestroyImmediate(birch);
        Object.DestroyImmediate(wildflowerResearch);
        Object.DestroyImmediate(birchResearch);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    [Test]
    public void CanStartResearch_NoPrerequisites_IsTrue()
    {
        Assert.IsTrue(researchManager.CanStartResearch(wildflowerResearch));
    }

    [Test]
    public void CanStartResearch_PrerequisiteNotMet_IsFalse()
    {
        Assert.IsFalse(researchManager.CanStartResearch(birchResearch));
    }

    [Test]
    public void CanStartResearch_PrerequisiteMet_IsTrue()
    {
        treePlanter.AddUnlockedVegetation(wildflower);

        Assert.IsTrue(researchManager.CanStartResearch(birchResearch));
    }

    [Test]
    public void CanStartResearch_AlreadyUnlocked_IsFalse()
    {
        treePlanter.AddUnlockedVegetation(wildflower);

        Assert.IsFalse(researchManager.CanStartResearch(wildflowerResearch));
    }

    [Test]
    public void CanStartResearch_WhileResearching_IsFalseForEverything()
    {
        researchManager.StartResearch(wildflowerResearch);

        Assert.IsFalse(researchManager.CanStartResearch(wildflowerResearch));
        Assert.IsTrue(researchManager.IsResearching);
    }

    [Test]
    public void StartResearch_ThenInstantComplete_UnlocksVegetation()
    {
        researchManager.StartResearch(wildflowerResearch);

        researchManager.DebugInstantComplete();

        Assert.IsTrue(treePlanter.IsVegetationUnlocked("wildflower"));
        Assert.IsFalse(researchManager.IsResearching);
    }

    [Test]
    public void CompletingPrerequisite_UnlocksDependentProject()
    {
        Assert.IsFalse(researchManager.CanStartResearch(birchResearch));

        researchManager.StartResearch(wildflowerResearch);
        researchManager.DebugInstantComplete();

        Assert.IsTrue(researchManager.CanStartResearch(birchResearch));
    }
}
