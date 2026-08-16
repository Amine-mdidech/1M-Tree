using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class StoreUI : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement storeRoot;
    private VisualElement storeFrame;
    private Button treesHeaderButton;
    private VisualElement treesRowList;
    private Button herbivoresHeaderButton;
    private VisualElement herbivoresRowList;
    private Button predatorsHeaderButton;
    private VisualElement predatorsRowList;
    private float refreshTimer;

    private bool treesExpanded = true;
    private bool herbivoresExpanded = true;
    private bool predatorsExpanded = true;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        storeRoot = root.Q<VisualElement>("store-root");
        storeFrame = root.Q<VisualElement>("store-frame");

        ScrollView storeScroll = root.Q<ScrollView>("store-scroll");
        storeScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        storeScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;

        treesHeaderButton = root.Q<Button>("trees-header-button");
        treesHeaderButton.clicked += OnTreesHeaderClicked;
        treesRowList = root.Q<VisualElement>("trees-row-list");

        herbivoresHeaderButton = root.Q<Button>("herbivores-header-button");
        herbivoresHeaderButton.clicked += OnHerbivoresHeaderClicked;
        herbivoresRowList = root.Q<VisualElement>("herbivores-row-list");

        predatorsHeaderButton = root.Q<Button>("predators-header-button");
        predatorsHeaderButton.clicked += OnPredatorsHeaderClicked;
        predatorsRowList = root.Q<VisualElement>("predators-row-list");

        storeRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (storeRoot == null || storeRoot.style.display != DisplayStyle.Flex)
        {
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = RefreshInterval;
            RefreshAll();
        }
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            RefreshAll();
            refreshTimer = RefreshInterval;
            PanelAnimator.Open(storeRoot, storeFrame);
        }
        else
        {
            PanelAnimator.Close(storeRoot, storeFrame);
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

    private void OnTreesHeaderClicked()
    {
        AudioManager.Instance?.PlayTap();
        treesExpanded = !treesExpanded;
        treesRowList.style.display = treesExpanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnHerbivoresHeaderClicked()
    {
        AudioManager.Instance?.PlayTap();
        herbivoresExpanded = !herbivoresExpanded;
        herbivoresRowList.style.display = herbivoresExpanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnPredatorsHeaderClicked()
    {
        AudioManager.Instance?.PlayTap();
        predatorsExpanded = !predatorsExpanded;
        predatorsRowList.style.display = predatorsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshAll()
    {
        RefreshTreesSection();
        RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory.Herbivore, herbivoresRowList, herbivoresHeaderButton, "Herbivores");
        RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory.Predator, predatorsRowList, predatorsHeaderButton, "Predators");
    }

    private void RefreshTreesSection()
    {
        treesRowList.Clear();

        int count = 0;
        foreach (VegetationDefinition vegetation in allVegetation)
        {
            if (vegetation == null || treePlanter == null || !treePlanter.IsVegetationUnlocked(vegetation.id))
            {
                continue;
            }

            count++;
            Sprite icon = vegetation.heroSprite != null
                ? vegetation.heroSprite
                : PixelArtUtility.CreateVegetationSprite(48, vegetation.plantSilhouette, vegetation.foliageColor, vegetation.trunkColor, vegetation.id.GetHashCode());

            VegetationDefinition capturedVegetation = vegetation;
            treesRowList.Add(CreateRow(icon, vegetation.displayName, vegetation.plantCost, () => SelectVegetation(capturedVegetation)));
        }

        treesHeaderButton.text = $"Trees ({count})";
    }

    private void RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory category, VisualElement rowList, Button headerButton, string categoryLabel)
    {
        rowList.Clear();

        int count = 0;
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species == null || species.category != category || emergenceSystem == null || !emergenceSystem.IsSpeciesUnlocked(species.id))
            {
                continue;
            }

            count++;
            Sprite icon = EmergenceSystem.GetSpeciesIconSprite(species);
            SpeciesDefinition capturedSpecies = species;
            rowList.Add(CreateRow(icon, species.displayName, species.releaseCost, () => SelectSpecies(capturedSpecies)));
        }

        headerButton.text = $"{categoryLabel} ({count})";
    }

    private VisualElement CreateRow(Sprite icon, string displayName, int cost, System.Action onClick)
    {
        Button row = new Button(onClick);
        row.AddToClassList("summary-row");

        if (economyManager != null && !economyManager.CanAfford(cost))
        {
            row.AddToClassList("summary-row--unaffordable");
        }

        VisualElement iconElement = new VisualElement();
        iconElement.AddToClassList("summary-row-icon");
        iconElement.style.backgroundImage = new StyleBackground(icon);
        row.Add(iconElement);

        Label nameLabel = new Label(displayName);
        nameLabel.AddToClassList("summary-row-name");
        row.Add(nameLabel);

        Label costLabel = new Label(cost.ToString());
        costLabel.AddToClassList("summary-row-cost");
        row.Add(costLabel);

        return row;
    }

    private void SelectVegetation(VegetationDefinition vegetation)
    {
        AudioManager.Instance?.PlayTap();

        treePlanter?.SelectVegetation(vegetation);
        HudUI.Instance?.SetPickerCategory(HudUI.PickerCategory.Plant);

        OnCloseButtonClicked();
    }

    private void SelectSpecies(SpeciesDefinition species)
    {
        AudioManager.Instance?.PlayTap();

        emergenceSystem?.SelectReleasableSpecies(species);
        HudUI.PickerCategory category = species.category == SpeciesDefinition.SpeciesCategory.Predator
            ? HudUI.PickerCategory.Predator
            : HudUI.PickerCategory.Herbivore;
        HudUI.Instance?.SetPickerCategory(category);

        OnCloseButtonClicked();
    }
}
