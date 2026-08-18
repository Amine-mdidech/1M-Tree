using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
    public void MainMenuUI_BackgroundSprite_IsWired()
    {
        MainMenuUI menu = FindComponent<MainMenuUI>();
        Assert.IsNotNull(menu);
        Assert.IsNotNull(GetPrivateField(menu, "backgroundSprite"));
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
}
