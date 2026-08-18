using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuSceneWiringTests
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

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

    private T FindComponent<T>() where T : Component
    {
        foreach (GameObject root in loadedScene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field.GetValue(target);
    }

    [Test]
    public void MainMenuUI_Exists()
    {
        Assert.IsNotNull(FindComponent<MainMenuUI>());
    }

    [Test]
    public void MainMenuUI_SettingsUI_IsWired()
    {
        MainMenuUI menu = FindComponent<MainMenuUI>();
        Assert.IsNotNull(menu);
        Assert.IsNotNull(GetPrivateField(menu, "settingsUI"));
    }

    [Test]
    public void MainMenuUI_AllMilestones_HasFiveEntries()
    {
        MainMenuUI menu = FindComponent<MainMenuUI>();
        Assert.IsNotNull(menu);
        System.Collections.IList milestones = (System.Collections.IList)GetPrivateField(menu, "allMilestones");
        Assert.AreEqual(5, milestones.Count);
        foreach (object milestone in milestones)
        {
            Assert.IsNotNull(milestone);
        }
    }

    [Test]
    public void SettingsUI_Exists()
    {
        Assert.IsNotNull(FindComponent<SettingsUI>());
    }

    [Test]
    public void AudioManager_Exists()
    {
        Assert.IsNotNull(FindComponent<AudioManager>());
    }

    [Test]
    public void BuildSettings_ContainMainMenuAndSampleScene_InOrder()
    {
        UnityEditor.EditorBuildSettingsScene[] scenes = UnityEditor.EditorBuildSettings.scenes;
        Assert.GreaterOrEqual(scenes.Length, 2);
        StringAssert.Contains("MainMenu.unity", scenes[0].path);
        StringAssert.Contains("SampleScene.unity", scenes[1].path);
    }

    [Test]
    public void MainMenuUI_UIDocument_IsWired()
    {
        MainMenuUI menu = FindComponent<MainMenuUI>();
        Assert.IsNotNull(menu);
        UIDocument document = menu.GetComponent<UIDocument>();
        Assert.IsNotNull(document);
        Assert.IsNotNull(document.visualTreeAsset);
        Assert.IsNotNull(document.panelSettings);
    }

    [Test]
    public void SettingsUI_UIDocument_IsWired()
    {
        SettingsUI settings = FindComponent<SettingsUI>();
        Assert.IsNotNull(settings);
        UIDocument document = settings.GetComponent<UIDocument>();
        Assert.IsNotNull(document);
        Assert.IsNotNull(document.visualTreeAsset);
        Assert.IsNotNull(document.panelSettings);
    }

    [Test]
    public void MainMenuUI_AllMilestones_MatchesSampleSceneRoster()
    {
        Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
        try
        {
            MainMenuUI menu = FindComponent<MainMenuUI>();
            Assert.IsNotNull(menu);
            IList menuMilestones = (IList)GetPrivateField(menu, "allMilestones");
            List<string> menuIds = new List<string>();
            foreach (object m in menuMilestones)
            {
                FieldInfo idField = m.GetType().GetField("id");
                menuIds.Add((string)idField.GetValue(m));
            }

            MilestoneManager milestoneManager = Object.FindFirstObjectByType<MilestoneManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(milestoneManager);
            IList sampleMilestones = (IList)GetPrivateField(milestoneManager, "milestones");
            List<string> sampleIds = new List<string>();
            foreach (object m in sampleMilestones)
            {
                FieldInfo idField = m.GetType().GetField("id");
                sampleIds.Add((string)idField.GetValue(m));
            }

            CollectionAssert.AreEquivalent(sampleIds, menuIds);
        }
        finally
        {
            EditorSceneManager.CloseScene(sampleScene, true);
        }
    }
}
