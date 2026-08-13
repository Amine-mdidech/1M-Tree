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
    [SerializeField] private SettingsUI settingsUI;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private MilestoneManager milestoneManager;
    [SerializeField] private CameraPanController cameraPanController;
    [SerializeField] private LaboratoryBuilding laboratoryBuilding;
    [SerializeField] private MapUI mapUI;

    private const int MinimapSize = 42;
    private const float MinimapRefreshInterval = 0.5f;
    private const float MinimapTapMaxDragDistance = 5f;

    public static HudUI Instance { get; private set; }

    private UIDocument uiDocument;
    private VisualElement actionCluster;
    private VisualElement demoButtonElement;
    private VisualElement topLeftCluster;
    private VisualElement currencyBadge;
    private VisualElement minimapCluster;
    private VisualElement minimapView;
    private VisualElement landLockPopupRoot;
    private Label landLockPopupMessage;
    private Button landLockUnlockButton;
    private Button landLockCloseButton;
    private Button vegetationButton;
    private Label vegetationLabel;
    private Label gemCountLabel;
    private int lastDisplayedGems = -1;
    private VisualElement xpBarFill;
    private Label xpLevelLabel;
    private int lastDisplayedLevel = -1;
    private int lastDisplayedXp = -1;
    private float minimapRefreshTimer;
    private bool minimapPointerActive;
    private bool minimapDragging;
    private Vector2 minimapPressPosition;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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

        // Icon buttons render their whole circular fill (shade/highlight/grain) as part of the
        // sprite itself via CreateBadgedGlyphSprite, instead of a flat CSS backgroundColor behind a
        // transparent glyph sprite — a flat single-color fill read as "1-dimensional"/plasticky.
        Button codexButton = root.Q<Button>("codex-button");
        codexButton.clicked += OnCodexButtonClicked;
        codexButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.25f, 0.3f, 0.55f), PixelArtUtility.DrawBookGlyph));

        Button labButton = root.Q<Button>("lab-button");
        labButton.clicked += OnLabButtonClicked;
        labButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.12f, 0.64f, 0.68f), PixelArtUtility.DrawFlaskGlyph));

        vegetationButton = root.Q<Button>("vegetation-button");
        vegetationButton.clicked += OnVegetationButtonClicked;
        vegetationButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.55f, 0.38f, 0.22f), PixelArtUtility.DrawPineGlyph));

        Button profileButton = root.Q<Button>("profile-button");
        profileButton.clicked += OnProfileButtonClicked;
        profileButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.73f, 0.54f, 0.24f), PixelArtUtility.DrawPersonGlyph));

        Button settingsButton = root.Q<Button>("settings-button");
        settingsButton.clicked += OnSettingsButtonClicked;
        settingsButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.3f, 0.45f, 0.6f), PixelArtUtility.DrawSpeakerGlyph));

        Button demoButton = root.Q<Button>("demo-button");
        demoButton.clicked += OnDemoButtonClicked;
        demoButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.42f, 0.45f, 0.5f), PixelArtUtility.DrawGearGlyph));

        vegetationLabel = root.Q<Label>("vegetation-label");
        // Flat rgba fills read as "1-dimensional" — layer a subtle gradient+grain texture behind
        // the existing translucent tint instead (stretch-to-fill in USS handles the elements'
        // content-driven widths; the effect is subtle enough that mild horizontal stretch doesn't
        // read as distorted).
        vegetationLabel.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(96, 24, new Color(0.08f, 0.08f, 0.08f, 0.2f)));

        VisualElement gemIcon = root.Q<VisualElement>("gem-icon");
        gemIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawGemGlyph));
        gemCountLabel = root.Q<Label>("gem-count-label");

        VisualElement xpBadge = root.Q<VisualElement>("xp-badge");
        xpBadge.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(140, 32, new Color(0.094f, 0.078f, 0.071f, 0.82f)));

        VisualElement xpIcon = root.Q<VisualElement>("xp-icon");
        xpIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawStarGlyph));

        xpLevelLabel = root.Q<Label>("xp-level-label");
        xpBarFill = root.Q<VisualElement>("xp-bar-fill");

        topLeftCluster = root.Q<VisualElement>("top-left-cluster");
        currencyBadge = root.Q<VisualElement>("currency-badge");
        currencyBadge.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(140, 32, new Color(0.094f, 0.078f, 0.071f, 0.82f)));
        currencyBadge.AddToClassList("currency-badge--enter");
        currencyBadge.schedule.Execute(() => currencyBadge.RemoveFromClassList("currency-badge--enter")).ExecuteLater(50);

        minimapCluster = root.Q<VisualElement>("minimap-cluster");
        minimapView = root.Q<VisualElement>("minimap-view");
        minimapView.RegisterCallback<PointerDownEvent>(OnMinimapPointerDown);
        minimapView.RegisterCallback<PointerMoveEvent>(OnMinimapPointerMove);
        minimapView.RegisterCallback<PointerUpEvent>(OnMinimapPointerUp);

        landLockPopupRoot = root.Q<VisualElement>("land-lock-popup-root");
        landLockPopupMessage = root.Q<Label>("land-lock-popup-message");
        landLockUnlockButton = root.Q<Button>("land-lock-unlock-button");
        landLockUnlockButton.clicked += OnLandLockUnlockButtonClicked;
        landLockCloseButton = root.Q<Button>("land-lock-close-button");
        landLockCloseButton.clicked += OnLandLockCloseButtonClicked;

        RefreshVegetationLabel();
        RefreshGemCount();
        RefreshXp();
        minimapRefreshTimer = 0f;
    }

    private void Update()
    {
        RefreshGemCount();
        RefreshXp();

        minimapRefreshTimer -= Time.deltaTime;
        if (minimapRefreshTimer <= 0f)
        {
            minimapRefreshTimer = MinimapRefreshInterval;
            RefreshMinimap();
        }
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

        if (topLeftCluster != null)
        {
            topLeftCluster.style.display = display;
        }

        if (minimapCluster != null)
        {
            minimapCluster.style.display = display;
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

    private void OnSettingsButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenSettings();
        }
        else if (settingsUI != null)
        {
            settingsUI.SetOpen(true);
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

    private void OnMinimapPointerDown(PointerDownEvent evt)
    {
        minimapPointerActive = true;
        minimapDragging = false;
        minimapPressPosition = evt.localPosition;
        minimapView.CapturePointer(evt.pointerId);
    }

    private void OnMinimapPointerMove(PointerMoveEvent evt)
    {
        if (!minimapPointerActive)
        {
            return;
        }

        if (!minimapDragging)
        {
            if (Vector2.Distance(evt.localPosition, minimapPressPosition) < MinimapTapMaxDragDistance)
            {
                return;
            }

            minimapDragging = true;
        }

        JumpCameraToMinimapPoint(evt.localPosition);
    }

    private void OnMinimapPointerUp(PointerUpEvent evt)
    {
        if (!minimapPointerActive)
        {
            return;
        }

        minimapPointerActive = false;
        minimapView.ReleasePointer(evt.pointerId);

        if (!minimapDragging)
        {
            OpenMap();
        }
    }

    private void OpenMap()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenMap();
        }
        else if (mapUI != null)
        {
            mapUI.SetOpen(true);
        }
    }

    private void JumpCameraToMinimapPoint(Vector2 localPosition)
    {
        if (cameraPanController == null || minimapView == null)
        {
            return;
        }

        float width = minimapView.resolvedStyle.width;
        float height = minimapView.resolvedStyle.height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        float normalizedX = Mathf.Clamp01(localPosition.x / width);
        float normalizedY = Mathf.Clamp01(localPosition.y / height);

        float worldX = (normalizedX * 2f - 1f) * cameraPanController.MapHalfWidth;
        float worldY = (1f - normalizedY * 2f) * cameraPanController.MapHalfHeight;

        cameraPanController.JumpToWorldPosition(new Vector2(worldX, worldY));
    }

    public void ShowLandLockedPopup()
    {
        if (landLockPopupRoot == null || LandManager.Instance == null || !LandManager.Instance.HasNextTier)
        {
            return;
        }

        LandManager.LandTier next = LandManager.Instance.GetNextTier();
        int currentLevel = PlayerLevel.Instance != null ? PlayerLevel.Instance.Level : 1;
        int currentGems = milestoneManager != null ? milestoneManager.Gems : 0;

        landLockPopupMessage.text =
            $"You need Level {next.requiredLevel} and {next.gemCost} gems to unlock more land.\n" +
            $"You are Level {currentLevel} with {currentGems} gems.";

        bool canAfford = LandManager.Instance.CanAffordNextTier();
        landLockUnlockButton.text = canAfford ? $"Unlock ({next.gemCost}g)" : "Not Yet";
        landLockUnlockButton.SetEnabled(canAfford);

        if (canAfford)
        {
            landLockUnlockButton.RemoveFromClassList("land-lock-unlock-button--locked");
        }
        else
        {
            landLockUnlockButton.AddToClassList("land-lock-unlock-button--locked");
        }

        landLockPopupRoot.style.display = DisplayStyle.Flex;
    }

    private void OnLandLockUnlockButtonClicked()
    {
        if (LandManager.Instance != null && LandManager.Instance.TryUnlockNextTier())
        {
            AudioManager.Instance?.PlayTap();
            HideLandLockedPopup();
        }
        else
        {
            AudioManager.Instance?.PlayError();
        }
    }

    private void OnLandLockCloseButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        HideLandLockedPopup();
    }

    private void HideLandLockedPopup()
    {
        if (landLockPopupRoot != null)
        {
            landLockPopupRoot.style.display = DisplayStyle.None;
        }
    }

    private void RefreshMinimap()
    {
        if (minimapView == null)
        {
            return;
        }

        minimapView.style.backgroundImage = new StyleBackground(GenerateMapTexture(MinimapSize));
    }

    // Shared by the small corner minimap and the full-screen MapUI panel (which calls this via
    // HudUI.Instance) so both draw from one implementation instead of duplicating it. Dot/outline
    // sizes scale with `size` so they stay proportionate whether this is a 42px corner widget or a
    // 512px full-screen map.
    public Texture2D GenerateMapTexture(int size)
    {
        PixelArtUtility.PixelBuffer buffer = new PixelArtUtility.PixelBuffer(size, size);

        PixelArtUtility.FillRect(buffer, new RectInt(0, 0, size, size), new Color(0.16f, 0.22f, 0.14f));

        float mapHalfWidth = cameraPanController != null ? cameraPanController.MapHalfWidth : 30f;
        float mapHalfHeight = cameraPanController != null ? cameraPanController.MapHalfHeight : 30f;

        Vector2 WorldToMap(Vector2 worldPosition)
        {
            float nx = (worldPosition.x + mapHalfWidth) / (mapHalfWidth * 2f);
            float ny = (worldPosition.y + mapHalfHeight) / (mapHalfHeight * 2f);
            return new Vector2(nx * size, (1f - ny) * size);
        }

        if (LandManager.Instance != null)
        {
            float halfExtent = LandManager.Instance.CurrentLandHalfExtent;
            Vector2 landMin = WorldToMap(new Vector2(-halfExtent, -halfExtent));
            Vector2 landMax = WorldToMap(new Vector2(halfExtent, halfExtent));
            RectInt landRect = new RectInt(
                Mathf.RoundToInt(Mathf.Min(landMin.x, landMax.x)),
                Mathf.RoundToInt(Mathf.Min(landMin.y, landMax.y)),
                Mathf.RoundToInt(Mathf.Abs(landMax.x - landMin.x)),
                Mathf.RoundToInt(Mathf.Abs(landMax.y - landMin.y)));

            PixelArtUtility.FillRect(buffer, landRect, new Color(0.42f, 0.58f, 0.3f));
        }

        if (RiverManager.Instance != null)
        {
            Color riverColor = new Color(0.3f, 0.55f, 0.85f);
            float riverRadius = Mathf.Max(1f, size * 0.012f);
            var riverPath = RiverManager.Instance.PathSamples;
            for (int i = 0; i < riverPath.Count; i++)
            {
                Vector2 mapPosition = WorldToMap(riverPath[i]);
                PixelArtUtility.FillCircle(buffer, mapPosition, riverRadius, riverColor, Color.clear, 0);
            }
        }

        float treeRadius = Mathf.Max(1.5f, size * 0.018f);
        int treeOutline = Mathf.Max(1, size / 200);
        foreach (PlantedTree tree in PlantedTree.ActiveTrees)
        {
            if (tree == null || tree.Vegetation == null)
            {
                continue;
            }

            Vector2 mapPosition = WorldToMap(tree.transform.position);
            PixelArtUtility.FillCircle(buffer, mapPosition, treeRadius, tree.Vegetation.foliageColor, Color.black, treeOutline);
        }

        if (laboratoryBuilding != null)
        {
            float labRadius = Mathf.Max(3f, size * 0.03f);
            int labOutline = Mathf.Max(1, size / 200);
            Vector2 labPosition = WorldToMap(laboratoryBuilding.transform.position);
            PixelArtUtility.FillCircle(buffer, labPosition, labRadius, new Color(0.85f, 0.6f, 0.3f), Color.white, labOutline);
        }

        if (cameraPanController != null)
        {
            Vector3 cameraPosition = cameraPanController.CameraWorldPosition;
            float viewportHalfWidth = cameraPanController.ViewportHalfWidth;
            float viewportHalfHeight = cameraPanController.ViewportHalfHeight;

            Vector2 viewMin = WorldToMap(new Vector2(cameraPosition.x - viewportHalfWidth, cameraPosition.y - viewportHalfHeight));
            Vector2 viewMax = WorldToMap(new Vector2(cameraPosition.x + viewportHalfWidth, cameraPosition.y + viewportHalfHeight));
            DrawRectOutline(buffer, viewMin, viewMax, Color.white);
        }

        return buffer.ToTexture();
    }

    private static void DrawRectOutline(PixelArtUtility.PixelBuffer buffer, Vector2 a, Vector2 b, Color color)
    {
        int xMin = Mathf.RoundToInt(Mathf.Min(a.x, b.x));
        int xMax = Mathf.RoundToInt(Mathf.Max(a.x, b.x));
        int yMin = Mathf.RoundToInt(Mathf.Min(a.y, b.y));
        int yMax = Mathf.RoundToInt(Mathf.Max(a.y, b.y));

        for (int x = xMin; x <= xMax; x++)
        {
            buffer.Set(x, yMin, color);
            buffer.Set(x, yMax, color);
        }

        for (int y = yMin; y <= yMax; y++)
        {
            buffer.Set(xMin, y, color);
            buffer.Set(xMax, y, color);
        }
    }
}
