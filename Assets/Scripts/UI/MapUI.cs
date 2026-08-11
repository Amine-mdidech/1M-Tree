using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MapUI : MonoBehaviour
{
    private const int MapTextureSize = 512;

    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement mapRoot;
    private VisualElement mapFrame;
    private VisualElement mapView;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        mapRoot = root.Q<VisualElement>("map-root");

        mapFrame = root.Q<VisualElement>("map-frame");
        mapFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.16f, 0.2f, 0.27f)));

        // Purely a static overview, no drag-to-navigate here — a full-screen modal that also moved
        // the live world camera underneath it read as broken ("why is the world scrolling behind
        // the map"). The small corner minimap is still the drag-to-navigate shortcut.
        mapView = root.Q<VisualElement>("map-view");

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        mapRoot.style.display = DisplayStyle.None;
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
            PanelAnimator.Open(mapRoot, mapFrame);
        }
        else
        {
            PanelAnimator.Close(mapRoot, mapFrame);
        }

        IsOpen = visible;
    }

    private void Refresh()
    {
        if (mapView == null || HudUI.Instance == null)
        {
            return;
        }

        mapView.style.backgroundImage = new StyleBackground(HudUI.Instance.GenerateMapTexture(MapTextureSize));
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
}
