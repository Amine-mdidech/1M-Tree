using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuSceneWiringTests
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

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
}
