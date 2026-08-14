using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ProfileUI : MonoBehaviour
{
    [SerializeField] private MilestoneManager milestoneManager;
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private DailyGrant dailyGrant;
    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement profileRoot;
    private VisualElement profileFrame;
    private Label levelValueLabel;
    private Label landValueLabel;
    private Label gemsValueLabel;
    private Label treesValueLabel;
    private Label speciesValueLabel;
    private Label vegetationValueLabel;
    private Label milestonesValueLabel;
    private Label dailyCapValueLabel;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        profileRoot = root.Q<VisualElement>("profile-root");

        profileFrame = root.Q<VisualElement>("profile-frame");
        profileFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateWoodGrainTexture(256, 256, new Color(0.58f, 0.42f, 0.22f)));

        ScrollView profileContent = root.Q<ScrollView>("profile-content");
        profileContent.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateParchmentTexture(256, 256, new Color(0.94f, 0.87f, 0.72f)));
        profileContent.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        profileContent.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        levelValueLabel = root.Q<Label>("level-value");
        landValueLabel = root.Q<Label>("land-value");
        gemsValueLabel = root.Q<Label>("gems-value");
        treesValueLabel = root.Q<Label>("trees-value");
        speciesValueLabel = root.Q<Label>("species-value");
        vegetationValueLabel = root.Q<Label>("vegetation-value");
        milestonesValueLabel = root.Q<Label>("milestones-value");
        dailyCapValueLabel = root.Q<Label>("daily-cap-value");

        root.Q<VisualElement>("level-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.55f, 0.35f, 0.85f), PixelArtUtility.DrawStarGlyph));
        root.Q<VisualElement>("land-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.4f, 0.65f, 0.35f), PixelArtUtility.DrawTerritoryGlyph));
        root.Q<VisualElement>("gems-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(1f, 0.8f, 0.28f), PixelArtUtility.DrawGemGlyph));
        root.Q<VisualElement>("trees-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.28f, 0.65f, 0.28f), PixelArtUtility.DrawLeafGlyph));
        root.Q<VisualElement>("species-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.68f, 0.45f, 0.22f), PixelArtUtility.DrawPawGlyph));
        root.Q<VisualElement>("vegetation-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.14f, 0.55f, 0.6f), PixelArtUtility.DrawSproutGlyph));
        root.Q<VisualElement>("milestones-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.95f, 0.7f, 0.15f), PixelArtUtility.DrawStarGlyph));
        root.Q<VisualElement>("daily-cap-icon").style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.75f, 0.55f, 0.28f), PixelArtUtility.DrawHourglassGlyph));

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        profileRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            ToggleProfile();
        }
    }

    [ContextMenu("Debug: Toggle Profile")]
    public void ToggleProfile()
    {
        bool isVisible = profileRoot.style.display == DisplayStyle.Flex;
        SetOpen(!isVisible);
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            Refresh();
            PanelAnimator.Open(profileRoot, profileFrame);
        }
        else
        {
            PanelAnimator.Close(profileRoot, profileFrame);
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

    private void Refresh()
    {
        if (PlayerLevel.Instance != null)
        {
            levelValueLabel.text = PlayerLevel.Instance.Level.ToString();
        }

        if (LandManager.Instance != null)
        {
            landValueLabel.text = $"{LandManager.Instance.CurrentTierIndex + 1} / {LandManager.Instance.TierCount}";
        }

        if (milestoneManager != null)
        {
            gemsValueLabel.text = milestoneManager.Gems.ToString();
            milestonesValueLabel.text = $"{milestoneManager.CompletedMilestoneIds.Count} / {milestoneManager.TotalMilestoneCount}";
        }

        treesValueLabel.text = EcosystemState.TotalTreeCount.ToString();

        if (emergenceSystem != null)
        {
            speciesValueLabel.text = $"{emergenceSystem.UnlockedSpeciesCount} / {allSpecies.Count}";
        }

        if (treePlanter != null)
        {
            vegetationValueLabel.text = $"{treePlanter.GetUnlockedVegetationIds().Count} / {allVegetation.Count}";
        }

        if (dailyGrant != null)
        {
            dailyCapValueLabel.text = $"{dailyGrant.TreesPlantedToday} / {dailyGrant.DailyTreeCap}";
        }
    }
}
