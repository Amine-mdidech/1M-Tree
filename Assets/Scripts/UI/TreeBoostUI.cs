using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TreeBoostUI : MonoBehaviour
{
    private const int GemCostPerRemainingMinute = 1;

    [SerializeField] private MilestoneManager milestoneManager;

    private UIDocument uiDocument;
    private VisualElement boostRoot;
    private Label treeNameLabel;
    private Label remainingTimeLabel;
    private Button boostButton;
    private Button moveButton;

    private PlantedTree targetTree;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        boostRoot = root.Q<VisualElement>("boost-root");

        VisualElement boostCard = root.Q<VisualElement>("boost-card");
        boostCard.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateParchmentTexture(256, 256, new Color(0.83f, 0.9f, 0.75f)));

        treeNameLabel = root.Q<Label>("tree-name-label");
        remainingTimeLabel = root.Q<Label>("remaining-time-label");
        boostButton = root.Q<Button>("boost-button");
        moveButton = root.Q<Button>("move-button");

        root.Q<Button>("close-button").clicked += Close;
        boostButton.clicked += OnBoostButtonClicked;
        moveButton.clicked += OnMoveButtonClicked;

        SetVisible(false);
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (targetTree == null)
        {
            return;
        }

        Refresh();
    }

    public void Open(PlantedTree tree)
    {
        if (tree == null)
        {
            return;
        }

        targetTree = tree;
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        targetTree = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        boostRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        IsOpen = visible;
    }

    private void Refresh()
    {
        treeNameLabel.text = targetTree.Vegetation.displayName;

        bool isMature = targetTree.IsMature;
        remainingTimeLabel.style.display = isMature ? DisplayStyle.None : DisplayStyle.Flex;
        boostButton.style.display = isMature ? DisplayStyle.None : DisplayStyle.Flex;

        if (isMature)
        {
            return;
        }

        int remaining = Mathf.Max(0, Mathf.CeilToInt(targetTree.GetRemainingGrowthSeconds()));
        int minutes = remaining / 60;
        int seconds = remaining % 60;
        remainingTimeLabel.text = $"Matures in {minutes:00}:{seconds:00}";

        boostButton.text = $"Speed Up ({GetBoostCost(targetTree)} gems)";
    }

    private static int GetBoostCost(PlantedTree tree)
    {
        int remainingMinutes = Mathf.CeilToInt(tree.GetRemainingGrowthSeconds() / 60f);
        return Mathf.Max(1, GemCostPerRemainingMinute * remainingMinutes);
    }

    private void OnBoostButtonClicked()
    {
        if (targetTree == null || milestoneManager == null)
        {
            return;
        }

        int cost = GetBoostCost(targetTree);
        if (!milestoneManager.TrySpendGems(cost))
        {
            Debug.Log($"Not enough gems to speed up growth (need {cost}).");
            AudioManager.Instance?.PlayError();
            return;
        }

        Vector3 boostPosition = targetTree.transform.position;
        targetTree.CompleteGrowthInstantly();
        AudioManager.Instance?.PlayBoost();
        VfxUtility.SpawnBurst(boostPosition, new Color(1f, 0.85f, 0.3f));
        Handheld.Vibrate();
        Close();
    }

    private void OnMoveButtonClicked()
    {
        if (targetTree == null)
        {
            return;
        }

        CameraPanController.Instance?.BeginMoveTree(targetTree);
        Close();
    }
}
