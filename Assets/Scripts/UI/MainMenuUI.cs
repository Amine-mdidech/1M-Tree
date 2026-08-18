using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenuUI : MonoBehaviour
{
    private const string GameplaySceneName = "SampleScene";

    [SerializeField] private SettingsUI settingsUI;
    // fal.ai-generated hero art (see docs/art-design-document.md) used as the title screen's
    // backdrop so the menu reads as this game's world, not a generic button stack.
    [SerializeField] private Sprite backgroundSprite;

    private UIDocument uiDocument;
    private VisualElement menuRoot;
    private VisualElement menuFrame;
    private VisualElement confirmRoot;
    private VisualElement confirmFrame;
    private Button playButton;
    private Button newGameButton;

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        menuRoot = root.Q<VisualElement>("menu-root");
        if (backgroundSprite != null)
        {
            menuRoot.style.backgroundImage = new StyleBackground(backgroundSprite);
        }

        menuFrame = root.Q<VisualElement>("menu-frame");
        menuFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.62f, 0.47f, 0.28f)));

        confirmRoot = root.Q<VisualElement>("confirm-root");
        confirmFrame = root.Q<VisualElement>("confirm-frame");
        // Direct hide (not PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash.
        confirmRoot.style.display = DisplayStyle.None;

        playButton = root.Q<Button>("play-button");
        newGameButton = root.Q<Button>("new-game-button");

        playButton.clicked += OnPlayButtonClicked;
        newGameButton.clicked += OnNewGameButtonClicked;
        root.Q<Button>("confirm-yes-button").clicked += OnConfirmYesButtonClicked;
        root.Q<Button>("confirm-no-button").clicked += OnConfirmNoButtonClicked;
        root.Q<Button>("settings-button").clicked += OnSettingsButtonClicked;
        root.Q<Button>("quit-button").clicked += OnQuitButtonClicked;

        Refresh();
    }

    private void Refresh()
    {
        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(SavePath);

        playButton.text = summary.SaveExists ? "Continue" : "Play";
        newGameButton.style.display = summary.FileExists ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnPlayButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        SceneManager.LoadScene(GameplaySceneName);
    }

    private void OnNewGameButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        PanelAnimator.Open(confirmRoot, confirmFrame);
    }

    private void OnConfirmYesButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        SceneManager.LoadScene(GameplaySceneName);
    }

    private void OnConfirmNoButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        PanelAnimator.Close(confirmRoot, confirmFrame);
    }

    private void OnSettingsButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        settingsUI?.SetOpen(true);
    }

    private void OnQuitButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        Application.Quit();
    }
}
