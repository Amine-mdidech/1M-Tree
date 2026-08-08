using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HudUI : MonoBehaviour
{
    private const int IconSize = 64;

    [SerializeField] private ScreenNavigator screenNavigator;
    [SerializeField] private CodexUI codexUI;
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private DemoUI demoUI;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private MilestoneManager milestoneManager;

    private UIDocument uiDocument;
    private VisualElement actionCluster;
    private VisualElement demoButtonElement;
    private VisualElement currencyBadge;
    private Button vegetationButton;
    private Label vegetationLabel;
    private Label gemCountLabel;
    private int lastDisplayedGems = -1;
    private VisualElement xpBarFill;
    private Label xpLevelLabel;
    private int lastDisplayedLevel = -1;
    private int lastDisplayedXp = -1;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        VisualElement hudRoot = root.Q<VisualElement>("hud-root");
        hudRoot.style.flexGrow = 1;
        hudRoot.style.width = Length.Percent(100);
        hudRoot.style.height = Length.Percent(100);

        actionCluster = root.Q<VisualElement>("action-cluster");
        demoButtonElement = root.Q<Button>("demo-button");

        Button codexButton = root.Q<Button>("codex-button");
        codexButton.clicked += OnCodexButtonClicked;
        codexButton.style.backgroundColor = new Color(0.25f, 0.3f, 0.55f);
        codexButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawBookGlyph));

        Button labButton = root.Q<Button>("lab-button");
        labButton.clicked += OnLabButtonClicked;
        labButton.style.backgroundColor = new Color(0.12f, 0.64f, 0.68f);
        labButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawFlaskGlyph));

        vegetationButton = root.Q<Button>("vegetation-button");
        vegetationButton.clicked += OnVegetationButtonClicked;
        vegetationButton.style.backgroundColor = new Color(0.55f, 0.38f, 0.22f);
        vegetationButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawLeafGlyph));

        Button profileButton = root.Q<Button>("profile-button");
        profileButton.clicked += OnProfileButtonClicked;
        profileButton.style.backgroundColor = new Color(0.73f, 0.54f, 0.24f);
        profileButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawPersonGlyph));

        Button demoButton = root.Q<Button>("demo-button");
        demoButton.clicked += OnDemoButtonClicked;
        demoButton.style.backgroundColor = new Color(0.42f, 0.45f, 0.5f);
        demoButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawGearGlyph));

        vegetationLabel = root.Q<Label>("vegetation-label");

        VisualElement gemIcon = root.Q<VisualElement>("gem-icon");
        gemIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawGemGlyph));
        gemCountLabel = root.Q<Label>("gem-count-label");

        VisualElement xpBadge = root.Q<VisualElement>("xp-badge");
        xpBadge.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(140, 32, new Color(0.094f, 0.078f, 0.071f, 0.82f)));

        VisualElement xpIcon = root.Q<VisualElement>("xp-icon");
        xpIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawStarGlyph));

        xpLevelLabel = root.Q<Label>("xp-level-label");
        xpBarFill = root.Q<VisualElement>("xp-bar-fill");

        currencyBadge = root.Q<VisualElement>("currency-badge");
        currencyBadge.AddToClassList("currency-badge--enter");
        currencyBadge.schedule.Execute(() => currencyBadge.RemoveFromClassList("currency-badge--enter")).ExecuteLater(50);

        RefreshVegetationLabel();
        RefreshGemCount();
        RefreshXp();
    }

    private void Update()
    {
        RefreshGemCount();
        RefreshXp();
    }

    public void SetToolbarVisible(bool visible)
    {
        DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (actionCluster != null)
        {
            actionCluster.style.display = display;
        }

        if (demoButtonElement != null)
        {
            demoButtonElement.style.display = display;
        }

        if (currencyBadge != null)
        {
            currencyBadge.style.display = display;
        }
    }

    private void OnCodexButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenCodex();
        }
        else if (codexUI != null)
        {
            codexUI.SetOpen(true);
        }
    }

    private void OnLabButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenLab();
        }
        else if (laboratoryUI != null)
        {
            laboratoryUI.SetOpen(true);
        }
    }

    private void OnProfileButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenProfile();
        }
        else if (profileUI != null)
        {
            profileUI.SetOpen(true);
        }
    }

    private void OnDemoButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenDemo();
        }
        else if (demoUI != null)
        {
            demoUI.SetOpen(true);
        }
    }

    private void OnVegetationButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (treePlanter == null)
        {
            return;
        }

        treePlanter.CycleVegetation();
        RefreshVegetationLabel();
    }

    private void RefreshVegetationLabel()
    {
        if (vegetationLabel == null || treePlanter == null || treePlanter.CurrentVegetation == null)
        {
            return;
        }

        vegetationLabel.text = treePlanter.CurrentVegetation.displayName;
    }

    private void RefreshGemCount()
    {
        if (gemCountLabel == null || milestoneManager == null)
        {
            return;
        }

        int currentGems = milestoneManager.Gems;
        if (currentGems == lastDisplayedGems)
        {
            return;
        }

        int previousGems = lastDisplayedGems;
        lastDisplayedGems = currentGems;
        gemCountLabel.text = currentGems.ToString();

        if (previousGems >= 0 && currentGems > previousGems)
        {
            AnimateGemGain(currentGems - previousGems);
        }
    }

    private void AnimateGemGain(int amount)
    {
        if (currencyBadge != null)
        {
            currencyBadge.AddToClassList("currency-badge--pulse");
            currencyBadge.schedule.Execute(() => currencyBadge.RemoveFromClassList("currency-badge--pulse")).ExecuteLater(150);
        }

        VisualElement hudLayer = currencyBadge?.parent;
        if (hudLayer == null)
        {
            return;
        }

        Label floatingLabel = new Label($"+{amount}");
        floatingLabel.AddToClassList("gem-float-label");
        floatingLabel.pickingMode = PickingMode.Ignore;
        hudLayer.Add(floatingLabel);

        floatingLabel.schedule.Execute(() => floatingLabel.AddToClassList("gem-float-label--animate")).ExecuteLater(10);
        floatingLabel.schedule.Execute(() => floatingLabel.RemoveFromHierarchy()).ExecuteLater(800);
    }

    private void RefreshXp()
    {
        if (xpBarFill == null || xpLevelLabel == null || PlayerLevel.Instance == null)
        {
            return;
        }

        int currentLevel = PlayerLevel.Instance.Level;
        int currentXp = PlayerLevel.Instance.CurrentXp;
        int xpToNextLevel = PlayerLevel.Instance.XpToNextLevel;

        if (currentLevel == lastDisplayedLevel && currentXp == lastDisplayedXp)
        {
            return;
        }

        bool leveledUp = lastDisplayedLevel >= 0 && currentLevel > lastDisplayedLevel;

        lastDisplayedLevel = currentLevel;
        lastDisplayedXp = currentXp;

        xpLevelLabel.text = $"Lv {currentLevel}";

        float fillPercent = xpToNextLevel > 0 ? Mathf.Clamp01((float)currentXp / xpToNextLevel) * 100f : 0f;
        xpBarFill.style.width = Length.Percent(fillPercent);

        if (leveledUp)
        {
            AnimateLevelUp();
        }
    }

    private void AnimateLevelUp()
    {
        xpBarFill.AddToClassList("xp-bar-fill--flash");
        xpBarFill.schedule.Execute(() => xpBarFill.RemoveFromClassList("xp-bar-fill--flash")).ExecuteLater(300);

        xpLevelLabel.AddToClassList("xp-level-label--punch");
        xpLevelLabel.schedule.Execute(() => xpLevelLabel.RemoveFromClassList("xp-level-label--punch")).ExecuteLater(200);
    }
}
