using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class LaboratoryUI : MonoBehaviour
{
    private const int NodeWidth = 100;
    private const int NodeHeight = 42;
    private const int ColumnWidth = 130;
    private const int RowHeight = 54;
    private const int CanvasPadding = 20;

    private static readonly Color VegetationBranchColor = new Color(0.32f, 0.58f, 0.32f);
    private static readonly Color HerbivoreBranchColor = new Color(0.55f, 0.4f, 0.22f);
    private static readonly Color PredatorBranchColor = new Color(0.55f, 0.2f, 0.2f);
    private static readonly Color LockedColor = new Color(0.35f, 0.35f, 0.35f);
    private static readonly Color WaitingTint = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color InProgressBorder = new Color(1f, 0.8f, 0.2f);
    private static readonly Color CompletedBorder = new Color(1f, 1f, 1f);
    private static readonly Color LockedConnectorColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
    private static readonly Color UnlockedConnectorColor = new Color(1f, 0.85f, 0.3f, 0.9f);

    [SerializeField] private ResearchManager researchManager;
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement labRoot;
    private VisualElement labFrame;
    private VisualElement activeResearchPanel;
    private Label activeResearchLabel;
    private Button skipButton;
    private Button plantsTabButton;
    private Button herbivoresTabButton;
    private Button predatorsTabButton;
    private ScrollView treeScroll;
    private VisualElement treeCanvas;
    private TechTreeConnectorView connectorView;

    private Sprite leafIcon;
    private Sprite pawIcon;
    private Sprite fangIcon;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        labRoot = root.Q<VisualElement>("lab-root");
        activeResearchPanel = root.Q<VisualElement>("active-research-panel");
        activeResearchLabel = root.Q<Label>("active-research-label");
        skipButton = root.Q<Button>("skip-button");
        plantsTabButton = root.Q<Button>("plants-tab-button");
        herbivoresTabButton = root.Q<Button>("herbivores-tab-button");
        predatorsTabButton = root.Q<Button>("predators-tab-button");
        predatorsTabButton.style.marginRight = 0;
        plantsTabButton.clicked += () => SelectTab(LabTab.Plants);
        herbivoresTabButton.clicked += () => SelectTab(LabTab.Herbivores);
        predatorsTabButton.clicked += () => SelectTab(LabTab.Predators);
        treeScroll = root.Q<ScrollView>("tree-scroll");
        treeCanvas = root.Q<VisualElement>("tree-canvas");
        treeScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        treeScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        labFrame = root.Q<VisualElement>("lab-frame");
        labFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.28f, 0.5f, 0.5f)));

        Texture2D stoneInner = PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.82f, 0.93f, 0.92f));
        activeResearchPanel.style.backgroundImage = new StyleBackground(stoneInner);
        treeScroll.style.backgroundImage = new StyleBackground(stoneInner);

        connectorView = new TechTreeConnectorView();
        connectorView.style.position = Position.Absolute;
        connectorView.style.left = 0;
        connectorView.style.top = 0;
        connectorView.style.right = 0;
        connectorView.style.bottom = 0;
        treeCanvas.Add(connectorView);

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;
        skipButton.clicked += OnSkipButtonClicked;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        labRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            ToggleLab();
        }

        if (labRoot != null && labRoot.style.display == DisplayStyle.Flex
            && researchManager != null && researchManager.IsResearching)
        {
            RefreshActiveResearchLabel();
        }
    }

    [ContextMenu("Debug: Toggle Laboratory")]
    public void ToggleLab()
    {
        bool isVisible = labRoot.style.display == DisplayStyle.Flex;
        SetOpen(!isVisible);
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            selectedTab = LabTab.Plants;
            RefreshTabButtons();
            RefreshLab();
            PanelAnimator.Open(labRoot, labFrame);
        }
        else
        {
            PanelAnimator.Close(labRoot, labFrame);
        }

        IsOpen = visible;
    }

    private void SelectTab(LabTab tab)
    {
        if (selectedTab == tab)
        {
            return;
        }

        AudioManager.Instance?.PlayTap();
        selectedTab = tab;
        RefreshTabButtons();
        BuildTree();
    }

    private void RefreshTabButtons()
    {
        plantsTabButton.EnableInClassList("lab-tab-button--active", selectedTab == LabTab.Plants);
        herbivoresTabButton.EnableInClassList("lab-tab-button--active", selectedTab == LabTab.Herbivores);
        predatorsTabButton.EnableInClassList("lab-tab-button--active", selectedTab == LabTab.Predators);
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

    private void RefreshLab()
    {
        if (researchManager == null)
        {
            return;
        }

        bool researching = researchManager.IsResearching;
        activeResearchPanel.style.display = researching ? DisplayStyle.Flex : DisplayStyle.None;

        if (researching)
        {
            RefreshActiveResearchLabel();
        }

        BuildTree();
    }

    private void RefreshActiveResearchLabel()
    {
        ResearchProjectDefinition project = researchManager.ActiveProject;
        string name = researchManager.GetProjectDisplayName(project);
        int remaining = Mathf.Max(0, Mathf.CeilToInt(researchManager.RemainingSeconds));
        int minutes = remaining / 60;
        int seconds = remaining % 60;

        activeResearchLabel.text = $"Researching {name}... {minutes:00}:{seconds:00}";

        int remainingMinutes = Mathf.CeilToInt(researchManager.RemainingSeconds / 60f);
        int cost = project != null ? project.gemSkipCostPerRemainingMinute * remainingMinutes : 0;
        skipButton.text = $"Skip ({cost} gems)";
    }

    private void OnSkipButtonClicked()
    {
        researchManager.SkipWithGems();
        RefreshLab();
    }

    private enum NodeState
    {
        Completed,
        InProgress,
        Available,
        Waiting,
        Locked
    }

    private enum LabTab
    {
        Plants = 0,
        Herbivores = 1,
        Predators = 2
    }

    private LabTab selectedTab = LabTab.Plants;

    private void BuildTree()
    {
        treeCanvas.Clear();
        treeCanvas.Add(connectorView);

        List<ResearchProjectDefinition> projects = new List<ResearchProjectDefinition>();
        foreach (ResearchProjectDefinition project in researchManager.AvailableResearchProjects)
        {
            if (project != null && GetBranchRank(project) == (int)selectedTab)
            {
                projects.Add(project);
            }
        }

        Dictionary<string, ResearchProjectDefinition> byId = new Dictionary<string, ResearchProjectDefinition>();
        foreach (ResearchProjectDefinition project in projects)
        {
            if (project != null)
            {
                byId[project.id] = project;
            }
        }

        Dictionary<string, int> tiers = new Dictionary<string, int>();
        foreach (ResearchProjectDefinition project in projects)
        {
            if (project != null)
            {
                ComputeTier(project.id, byId, tiers);
            }
        }

        int tierCount = 0;
        foreach (int tier in tiers.Values)
        {
            tierCount = Mathf.Max(tierCount, tier + 1);
        }

        int[] laneCounters = new int[Mathf.Max(1, tierCount)];
        Dictionary<string, Vector2> nodeCenters = new Dictionary<string, Vector2>();
        Dictionary<string, NodeState> nodeStates = new Dictionary<string, NodeState>();

        List<ResearchProjectDefinition> sortedProjects = new List<ResearchProjectDefinition>(projects);
        sortedProjects.Sort((a, b) =>
        {
            if (a == null || b == null)
            {
                return 0;
            }

            int tierCompare = tiers[a.id].CompareTo(tiers[b.id]);
            if (tierCompare != 0)
            {
                return tierCompare;
            }

            return GetBranchRank(a).CompareTo(GetBranchRank(b));
        });

        foreach (ResearchProjectDefinition project in sortedProjects)
        {
            if (project == null)
            {
                continue;
            }

            int tier = tiers[project.id];
            int lane = laneCounters[tier]++;

            NodeState state = GetNodeState(project);
            nodeStates[project.id] = state;

            Vector2 position = new Vector2(
                CanvasPadding + tier * ColumnWidth,
                CanvasPadding + lane * RowHeight);

            VisualElement node = CreateNode(project, state);
            node.style.position = Position.Absolute;
            node.style.left = position.x;
            node.style.top = position.y;
            treeCanvas.Add(node);

            nodeCenters[project.id] = position + new Vector2(NodeWidth * 0.5f, NodeHeight * 0.5f);
        }

        int maxLaneCount = 1;
        foreach (int count in laneCounters)
        {
            maxLaneCount = Mathf.Max(maxLaneCount, count);
        }

        treeCanvas.style.width = CanvasPadding * 2 + tierCount * ColumnWidth;
        treeCanvas.style.height = CanvasPadding * 2 + maxLaneCount * RowHeight;

        List<TechTreeConnectorView.Connection> connections = new List<TechTreeConnectorView.Connection>();
        foreach (ResearchProjectDefinition project in projects)
        {
            if (project == null || project.prerequisiteProjectIds == null || !nodeCenters.TryGetValue(project.id, out Vector2 toCenter))
            {
                continue;
            }

            foreach (string prerequisiteId in project.prerequisiteProjectIds)
            {
                if (!nodeCenters.TryGetValue(prerequisiteId, out Vector2 fromCenter))
                {
                    continue;
                }

                bool prerequisiteUnlocked = byId.TryGetValue(prerequisiteId, out ResearchProjectDefinition prereqProject)
                    && researchManager.IsProjectUnlocked(prereqProject);

                connections.Add(new TechTreeConnectorView.Connection
                {
                    From = new Vector2(fromCenter.x + NodeWidth * 0.5f, fromCenter.y),
                    To = new Vector2(toCenter.x - NodeWidth * 0.5f, toCenter.y),
                    Color = prerequisiteUnlocked ? UnlockedConnectorColor : LockedConnectorColor
                });
            }
        }

        connectorView.SetConnections(connections);
    }

    private int ComputeTier(string projectId, Dictionary<string, ResearchProjectDefinition> byId, Dictionary<string, int> tiers)
    {
        if (tiers.TryGetValue(projectId, out int cached))
        {
            return cached;
        }

        if (!byId.TryGetValue(projectId, out ResearchProjectDefinition project))
        {
            return 0;
        }

        if (project.prerequisiteProjectIds == null || project.prerequisiteProjectIds.Length == 0)
        {
            tiers[projectId] = 0;
            return 0;
        }

        int maxPrerequisiteTier = -1;
        foreach (string prerequisiteId in project.prerequisiteProjectIds)
        {
            maxPrerequisiteTier = Mathf.Max(maxPrerequisiteTier, ComputeTier(prerequisiteId, byId, tiers));
        }

        int tier = maxPrerequisiteTier + 1;
        tiers[projectId] = tier;
        return tier;
    }

    private int GetBranchRank(ResearchProjectDefinition project)
    {
        if (!string.IsNullOrEmpty(project.unlocksVegetationId))
        {
            return 0;
        }

        SpeciesDefinition species = researchManager.FindUnlockableSpecies(project.unlocksSpeciesId);
        if (species != null && species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            return 2;
        }

        return 1;
    }

    private Color GetBranchColor(ResearchProjectDefinition project)
    {
        int rank = GetBranchRank(project);
        if (rank == 0)
        {
            return VegetationBranchColor;
        }

        return rank == 2 ? PredatorBranchColor : HerbivoreBranchColor;
    }

    private NodeState GetNodeState(ResearchProjectDefinition project)
    {
        if (researchManager.IsProjectUnlocked(project))
        {
            return NodeState.Completed;
        }

        if (researchManager.ActiveProject == project)
        {
            return NodeState.InProgress;
        }

        if (!researchManager.ArePrerequisitesMet(project))
        {
            return NodeState.Locked;
        }

        return researchManager.CanStartResearch(project) ? NodeState.Available : NodeState.Waiting;
    }

    private Sprite GetBranchGlyph(ResearchProjectDefinition project)
    {
        int rank = GetBranchRank(project);
        switch (rank)
        {
            case 0:
                if (leafIcon == null)
                {
                    leafIcon = PixelArtUtility.CreateGlyphSprite(24, PixelArtUtility.DrawLeafGlyph);
                }
                return leafIcon;

            case 2:
                if (fangIcon == null)
                {
                    fangIcon = PixelArtUtility.CreateGlyphSprite(24, PixelArtUtility.DrawFangGlyph);
                }
                return fangIcon;

            default:
                if (pawIcon == null)
                {
                    pawIcon = PixelArtUtility.CreateGlyphSprite(24, PixelArtUtility.DrawPawGlyph);
                }
                return pawIcon;
        }
    }

    private VisualElement CreateNode(ResearchProjectDefinition project, NodeState state)
    {
        Button node = new Button();
        node.AddToClassList("tree-node");
        node.style.width = NodeWidth;
        node.style.height = NodeHeight;

        VisualElement icon = new VisualElement();
        icon.AddToClassList("tree-node-icon");
        icon.style.backgroundImage = new StyleBackground(GetBranchGlyph(project));
        node.Add(icon);

        Label label = new Label();
        label.AddToClassList("tree-node-label");
        node.Add(label);

        Color branchColor = GetBranchColor(project);

        Color shadowEdge = new Color(0.07f, 0.05f, 0.04f);

        switch (state)
        {
            case NodeState.Completed:
                node.style.backgroundColor = branchColor;
                node.style.borderTopColor = CompletedBorder;
                node.style.borderLeftColor = CompletedBorder;
                node.style.borderBottomColor = shadowEdge;
                node.style.borderRightColor = shadowEdge;
                label.text = $"✓ {researchManager.GetProjectDisplayName(project)}";
                node.SetEnabled(false);
                break;

            case NodeState.InProgress:
                node.style.backgroundColor = branchColor;
                node.style.borderTopColor = InProgressBorder;
                node.style.borderLeftColor = InProgressBorder;
                node.style.borderBottomColor = shadowEdge;
                node.style.borderRightColor = shadowEdge;
                label.text = $"⏳ {researchManager.GetProjectDisplayName(project)}";
                node.SetEnabled(false);
                break;

            case NodeState.Available:
                node.style.backgroundColor = branchColor;
                node.style.borderTopColor = Color.white;
                node.style.borderLeftColor = Color.white;
                node.style.borderBottomColor = shadowEdge;
                node.style.borderRightColor = shadowEdge;
                label.text = researchManager.GetProjectDisplayName(project);
                node.clicked += () => OnNodeClicked(project);
                break;

            case NodeState.Waiting:
                Color dimmed = branchColor * WaitingTint;
                dimmed.a = 1f;
                node.style.backgroundColor = dimmed;
                node.style.borderTopColor = LockedColor;
                node.style.borderLeftColor = LockedColor;
                node.style.borderBottomColor = shadowEdge;
                node.style.borderRightColor = shadowEdge;
                label.text = researchManager.GetProjectDisplayName(project);
                node.SetEnabled(false);
                break;

            default:
                node.style.backgroundColor = LockedColor;
                node.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
                node.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
                node.style.borderBottomColor = shadowEdge;
                node.style.borderRightColor = shadowEdge;
                icon.style.display = DisplayStyle.None;
                label.text = "\U0001f512 ???";
                node.SetEnabled(false);
                break;
        }

        return node;
    }

    private void OnNodeClicked(ResearchProjectDefinition project)
    {
        researchManager.StartResearch(project);
        RefreshLab();
    }
}
