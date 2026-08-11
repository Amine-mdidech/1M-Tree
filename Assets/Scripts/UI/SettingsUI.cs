using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SettingsUI : MonoBehaviour
{
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement settingsRoot;
    private VisualElement settingsFrame;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Button musicMuteButton;
    private Button sfxMuteButton;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        settingsRoot = root.Q<VisualElement>("settings-root");

        settingsFrame = root.Q<VisualElement>("settings-frame");
        settingsFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.16f, 0.2f, 0.27f)));

        VisualElement settingsContent = root.Q<VisualElement>("settings-content");
        settingsContent.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.88f, 0.91f, 0.94f)));

        musicVolumeSlider = root.Q<Slider>("music-volume-slider");
        sfxVolumeSlider = root.Q<Slider>("sfx-volume-slider");
        musicMuteButton = root.Q<Button>("music-mute-button");
        sfxMuteButton = root.Q<Button>("sfx-mute-button");

        musicVolumeSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
        sfxVolumeSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);
        musicMuteButton.clicked += OnMusicMuteButtonClicked;
        sfxMuteButton.clicked += OnSfxMuteButtonClicked;

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        settingsRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            Refresh();
            PanelAnimator.Open(settingsRoot, settingsFrame);
        }
        else
        {
            PanelAnimator.Close(settingsRoot, settingsFrame);
        }

        IsOpen = visible;
    }

    private void OnCloseButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.GoHome();
        }
        else
        {
            SetOpen(false);
        }
    }

    private void OnMusicVolumeChanged(ChangeEvent<float> evt)
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetMusicVolume(evt.newValue);
        RefreshMuteButton(musicMuteButton, AudioManager.Instance.MusicMuted);
    }

    private void OnSfxVolumeChanged(ChangeEvent<float> evt)
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetSfxVolume(evt.newValue);
        RefreshMuteButton(sfxMuteButton, AudioManager.Instance.SfxMuted);
    }

    private void OnMusicMuteButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetMusicMuted(!AudioManager.Instance.MusicMuted);
        RefreshMuteButton(musicMuteButton, AudioManager.Instance.MusicMuted);
    }

    private void OnSfxMuteButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetSfxMuted(!AudioManager.Instance.SfxMuted);
        RefreshMuteButton(sfxMuteButton, AudioManager.Instance.SfxMuted);
    }

    private void Refresh()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);
        RefreshMuteButton(musicMuteButton, AudioManager.Instance.MusicMuted);
        RefreshMuteButton(sfxMuteButton, AudioManager.Instance.SfxMuted);
    }

    private static void RefreshMuteButton(Button button, bool muted)
    {
        button.text = muted ? "Unmute" : "Mute";

        if (muted)
        {
            button.AddToClassList("mute-button--active");
        }
        else
        {
            button.RemoveFromClassList("mute-button--active");
        }
    }
}
