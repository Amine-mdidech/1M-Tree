using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRosterWiringTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private Scene loadedScene;

    [SetUp]
    public void SetUp()
    {
        loadedScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.CloseScene(loadedScene, true);
    }

    private static T FindComponent<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field.GetValue(target);
    }

    private static List<string> GetIds(object target, string fieldName)
    {
        IEnumerable list = (IEnumerable)GetPrivateField(target, fieldName);
        List<string> ids = new List<string>();
        foreach (object item in list)
        {
            FieldInfo idField = item.GetType().GetField("id");
            ids.Add((string)idField.GetValue(item));
        }

        return ids;
    }

    [Test]
    public void TreePlanter_PlantableVegetation_MatchesRoster()
    {
        TreePlanter treePlanter = FindComponent<TreePlanter>();
        Assert.IsNotNull(treePlanter);
        CollectionAssert.AreEquivalent(new[] { "oak", "pine" }, GetIds(treePlanter, "plantableVegetation"));
    }

    [Test]
    public void TreePlanter_UnlockedVegetation_MatchesRoster()
    {
        TreePlanter treePlanter = FindComponent<TreePlanter>();
        Assert.IsNotNull(treePlanter);
        CollectionAssert.AreEquivalent(new[] { "oak" }, GetIds(treePlanter, "unlockedVegetation"));
    }

    [Test]
    public void EmergenceSystem_UnlockedNonPredatorSpecies_MatchesRoster()
    {
        EmergenceSystem emergenceSystem = FindComponent<EmergenceSystem>();
        Assert.IsNotNull(emergenceSystem);
        CollectionAssert.AreEquivalent(new[] { "gazelle" }, GetIds(emergenceSystem, "unlockedNonPredatorSpecies"));
    }

    [Test]
    public void EmergenceSystem_UnlockedPredatorSpecies_MatchesRoster()
    {
        EmergenceSystem emergenceSystem = FindComponent<EmergenceSystem>();
        Assert.IsNotNull(emergenceSystem);
        CollectionAssert.AreEquivalent(new[] { "wolf" }, GetIds(emergenceSystem, "unlockedPredatorSpecies"));
    }

    [Test]
    public void ResearchManager_AvailableResearchProjects_MatchesRoster()
    {
        ResearchManager researchManager = FindComponent<ResearchManager>();
        Assert.IsNotNull(researchManager);
        CollectionAssert.AreEquivalent(
            new[] { "pine_research", "squirrel_research", "fox_research" },
            GetIds(researchManager, "availableResearchProjects"));
    }

    [Test]
    public void ResearchManager_UnlockableSpecies_MatchesRoster()
    {
        ResearchManager researchManager = FindComponent<ResearchManager>();
        Assert.IsNotNull(researchManager);
        CollectionAssert.AreEquivalent(new[] { "squirrel", "fox" }, GetIds(researchManager, "unlockableSpecies"));
    }

    [Test]
    public void CodexUI_AllSpecies_MatchesRoster()
    {
        CodexUI codexUI = FindComponent<CodexUI>();
        Assert.IsNotNull(codexUI);
        CollectionAssert.AreEquivalent(new[] { "gazelle", "squirrel", "wolf", "fox" }, GetIds(codexUI, "allSpecies"));
    }

    [Test]
    public void CodexUI_AllVegetation_MatchesRoster()
    {
        CodexUI codexUI = FindComponent<CodexUI>();
        Assert.IsNotNull(codexUI);
        CollectionAssert.AreEquivalent(new[] { "oak", "pine" }, GetIds(codexUI, "allVegetation"));
    }

    [Test]
    public void DemoUI_AllSpecies_MatchesRoster()
    {
        DemoUI demoUI = FindComponent<DemoUI>();
        Assert.IsNotNull(demoUI);
        CollectionAssert.AreEquivalent(new[] { "gazelle", "squirrel", "wolf", "fox" }, GetIds(demoUI, "allSpecies"));
    }

    [Test]
    public void DemoUI_AllVegetation_MatchesRoster()
    {
        DemoUI demoUI = FindComponent<DemoUI>();
        Assert.IsNotNull(demoUI);
        CollectionAssert.AreEquivalent(new[] { "oak", "pine" }, GetIds(demoUI, "allVegetation"));
    }

    [Test]
    public void ProfileUI_AllSpecies_MatchesRoster()
    {
        ProfileUI profileUI = FindComponent<ProfileUI>();
        Assert.IsNotNull(profileUI);
        CollectionAssert.AreEquivalent(new[] { "gazelle", "squirrel", "wolf", "fox" }, GetIds(profileUI, "allSpecies"));
    }

    [Test]
    public void ProfileUI_AllVegetation_MatchesRoster()
    {
        ProfileUI profileUI = FindComponent<ProfileUI>();
        Assert.IsNotNull(profileUI);
        CollectionAssert.AreEquivalent(new[] { "oak", "pine" }, GetIds(profileUI, "allVegetation"));
    }

    [Test]
    public void SaveManager_AllSpecies_MatchesRoster()
    {
        SaveManager saveManager = FindComponent<SaveManager>();
        Assert.IsNotNull(saveManager);
        CollectionAssert.AreEquivalent(new[] { "gazelle", "squirrel", "wolf", "fox" }, GetIds(saveManager, "allSpecies"));
    }

    [Test]
    public void CameraPanController_EmergenceSystem_IsWired()
    {
        CameraPanController cameraPanController = FindComponent<CameraPanController>();
        Assert.IsNotNull(cameraPanController);
        Assert.IsNotNull(GetPrivateField(cameraPanController, "emergenceSystem"));
    }

    [Test]
    public void HudUI_PickerIconSprites_AreWired()
    {
        HudUI hud = FindComponent<HudUI>();
        Assert.IsNotNull(hud);
        Assert.IsNotNull(GetPrivateField(hud, "plantIconSprite"));
        Assert.IsNotNull(GetPrivateField(hud, "herbivoreIconSprite"));
        Assert.IsNotNull(GetPrivateField(hud, "predatorIconSprite"));
    }
}
