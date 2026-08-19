using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenuUI : MonoBehaviour
{
    private const string GameplaySceneName = "SampleScene";
    private const int GlyphSize = 64;
    // One full drift (center -> edge -> center) takes this long; kept slow so it reads as
    // ambient atmosphere behind the buttons, not something competing for attention.
    private const float BackgroundPanSeconds = 24f;
    private const float BackgroundPanRangePercent = 37.5f;

    [SerializeField] private SettingsUI settingsUI;
    // fal.ai-generated flat-illustrated backdrop (see docs/art-design-document.md's screen-backdrop
    // style) panned slowly behind the menu content - decorative atmosphere, not the player's real
    // saved world (that would need loading gameplay systems into this otherwise-lightweight scene).
    [SerializeField] private Sprite backgroundSprite;
    // Reuses the same level icon HudUI shows next to XP, so the badge reads as the same "level"
    // concept in both places.
    [SerializeField] private Sprite levelIconSprite;

    private UIDocument uiDocument;
    private VisualElement backgroundImage;
    private VisualElement levelBadge;
    private Label levelBadgeLabel;
    private VisualElement muteButtonIcon;
    private VisualElement confirmRoot;
    private VisualElement confirmFrame;
    private Button playButton;
    private Label playButtonLabel;
    private Button newGameButton;
    private Button muteButton;
    private float backgroundPanElapsed;

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        backgroundImage = root.Q<VisualElement>("menu-bg-image");
        if (backgroundSprite != null)
        {
            backgroundImage.style.backgroundImage = new StyleBackground(backgroundSprite);
        }

        backgroundPanElapsed = 0f;
        backgroundImage.schedule.Execute(TickBackgroundPan).Every(16);

        root.Q<VisualElement>("menu-bg-scrim").style.backgroundImage = new StyleBackground(CreateScrimTexture());

        levelBadge = root.Q<VisualElement>("level-badge");
        levelBadgeLabel = root.Q<Label>("level-badge-label");
        if (levelIconSprite != null)
        {
            root.Q<VisualElement>("level-badge-icon").style.backgroundImage = new StyleBackground(levelIconSprite);
        }

        root.Q<VisualElement>("play-button-icon").style.backgroundImage =
            new StyleBackground(PixelArtUtility.CreateGlyphSprite(GlyphSize, PixelArtUtility.DrawPlayGlyph));
        root.Q<VisualElement>("new-game-button-icon").style.backgroundImage =
            new StyleBackground(PixelArtUtility.CreateGlyphSprite(GlyphSize, PixelArtUtility.DrawSproutGlyph));
        root.Q<VisualElement>("settings-button-icon").style.backgroundImage =
            new StyleBackground(PixelArtUtility.CreateGlyphSprite(GlyphSize, PixelArtUtility.DrawGearGlyph));
        root.Q<VisualElement>("quit-button-icon").style.backgroundImage =
            new StyleBackground(PixelArtUtility.CreateGlyphSprite(GlyphSize, PixelArtUtility.DrawExitGlyph));

        muteButtonIcon = root.Q<VisualElement>("mute-button-icon");
        muteButtonIcon.style.backgroundImage =
            new StyleBackground(PixelArtUtility.CreateGlyphSprite(GlyphSize, PixelArtUtility.DrawSpeakerGlyph));

        confirmRoot = root.Q<VisualElement>("confirm-root");
        confirmFrame = root.Q<VisualElement>("confirm-frame");
        // Direct hide (not PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash.
        confirmRoot.style.display = DisplayStyle.None;

        playButton = root.Q<Button>("play-button");
        playButtonLabel = root.Q<Label>("play-button-label");
        newGameButton = root.Q<Button>("new-game-button");
        muteButton = root.Q<Button>("mute-button");

        playButton.clicked += OnPlayButtonClicked;
        newGameButton.clicked += OnNewGameButtonClicked;
        root.Q<Button>("confirm-yes-button").clicked += OnConfirmYesButtonClicked;
        root.Q<Button>("confirm-no-button").clicked += OnConfirmNoButtonClicked;
        root.Q<Button>("settings-button").clicked += OnSettingsButtonClicked;
        root.Q<Button>("quit-button").clicked += OnQuitButtonClicked;
        muteButton.clicked += OnMuteButtonClicked;

        Refresh();
    }

    private void Refresh()
    {
        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(SavePath);

        playButtonLabel.text = summary.SaveExists ? "Continue" : "Play";
        newGameButton.style.display = summary.FileExists ? DisplayStyle.Flex : DisplayStyle.None;

        levelBadge.style.display = summary.SaveExists ? DisplayStyle.Flex : DisplayStyle.None;
        levelBadgeLabel.text = $"Lv. {summary.Level}";

        RefreshMuteIcon();
    }

    private void RefreshMuteIcon()
    {
        bool muted = AudioManager.Instance != null && AudioManager.Instance.MusicMuted;
        muteButtonIcon.style.opacity = muted ? 0.4f : 1f;
    }

    private void TickBackgroundPan()
    {
        backgroundPanElapsed += 16f / 1000f;
        float t = Mathf.PingPong(backgroundPanElapsed / BackgroundPanSeconds, 1f);
        backgroundImage.style.left = Length.Percent(Mathf.Lerp(0f, -BackgroundPanRangePercent, t));
    }

    // A 1xN texture stretched to fill the scrim element - cheaper than a full-size gradient and
    // reads identically since it's stretched uniformly. Row 0 is the texture's top edge (matching
    // this project's established PixelArtUtility convention, e.g. DrawGearGlyph's low-y prong
    // rendering at the visual top). The header/title/subtitle sit at the top and the button row
    // sits at the very bottom (per the mockup), so both ends need to stay dark enough to read -
    // alpha is highest at the top and bottom edges and dips gently in the middle, giving the
    // panning backdrop just a hint of visibility there without ever risking text/button contrast
    // at either end.
    private static Texture2D CreateScrimTexture()
    {
        const int height = 128;
        const float edgeAlpha = 0.94f;
        const float middleAlpha = 0.82f;
        Color solid = new Color(42f / 255f, 40f / 255f, 30f / 255f);

        Texture2D texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            // 0 at both edges, 1 at the midpoint - a smooth dip, not a hard-edged band.
            float dip = Mathf.Sin(t * Mathf.PI);
            float alpha = Mathf.Lerp(edgeAlpha, middleAlpha, dip);

            texture.SetPixel(0, y, new Color(solid.r, solid.g, solid.b, alpha));
        }

        texture.Apply();
        return texture;
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

    private void OnMuteButtonClicked()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlayTap();
        AudioManager.Instance.SetMusicMuted(!AudioManager.Instance.MusicMuted);
        RefreshMuteIcon();
    }
}
