using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SpeciesSummaryUI : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement summaryRoot;
    private VisualElement summaryFrame;
    private Button treesHeaderButton;
    private VisualElement treesRowList;
    private Button herbivoresHeaderButton;
    private VisualElement herbivoresRowList;
    private Button predatorsHeaderButton;
    private VisualElement predatorsRowList;
    private VisualElement criteriaPopupRoot;
    private Label criteriaPopupTitle;
    private Label criteriaPopupMessage;
    private Dictionary<string, string> vegetationDisplayNamesById;
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
        summaryRoot = root.Q<VisualElement>("species-summary-root");
        summaryFrame = root.Q<VisualElement>("species-summary-frame");

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

        criteriaPopupRoot = root.Q<VisualElement>("criteria-popup-root");
        criteriaPopupTitle = root.Q<Label>("criteria-popup-title");
        criteriaPopupMessage = root.Q<Label>("criteria-popup-message");
        root.Q<Button>("criteria-popup-close-button").clicked += OnCriteriaPopupCloseButtonClicked;

        vegetationDisplayNamesById = new Dictionary<string, string>();
        foreach (VegetationDefinition vegetation in allVegetation)
        {
            if (vegetation != null)
            {
                vegetationDisplayNamesById[vegetation.id] = vegetation.displayName;
            }
        }

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        summaryRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (summaryRoot == null || summaryRoot.style.display != DisplayStyle.Flex)
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
            PanelAnimator.Open(summaryRoot, summaryFrame);
        }
        else
        {
            PanelAnimator.Close(summaryRoot, summaryFrame);
            HideCriteriaPopup();
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

        Dictionary<string, int> counts = GroupTreeCountsByVegetationId(PlantedTree.ActiveTrees);
        Dictionary<string, VegetationDefinition> representative = new Dictionary<string, VegetationDefinition>();
        foreach (PlantedTree tree in PlantedTree.ActiveTrees)
        {
            if (tree != null && tree.Vegetation != null)
            {
                representative[tree.Vegetation.id] = tree.Vegetation;
            }
        }

        int typeCount = 0;
        foreach (KeyValuePair<string, int> entry in counts)
        {
            if (entry.Value <= 0 || !representative.TryGetValue(entry.Key, out VegetationDefinition vegetation))
            {
                continue;
            }

            typeCount++;
            Sprite icon = vegetation.heroSprite != null
                ? vegetation.heroSprite
                : PixelArtUtility.CreateVegetationSprite(48, vegetation.plantSilhouette, vegetation.foliageColor, vegetation.trunkColor, vegetation.id.GetHashCode());
            treesRowList.Add(CreateRow(icon, vegetation.displayName, entry.Value, null));
        }

        treesHeaderButton.text = $"Trees ({typeCount})";
    }

    private void RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory category, VisualElement rowList, Button headerButton, string categoryLabel)
    {
        rowList.Clear();

        List<SpeciesDefinition> present = FilterPresentSpecies(allSpecies, category);
        foreach (SpeciesDefinition species in present)
        {
            Sprite icon = EmergenceSystem.GetSpeciesIconSprite(species);
            int population = EcosystemState.GetSpeciesPopulation(species.id);
            SpeciesDefinition capturedSpecies = species;
            rowList.Add(CreateRow(icon, species.displayName, population, () => ShowCriteriaPopup(capturedSpecies)));
        }

        headerButton.text = $"{categoryLabel} ({present.Count})";
    }

    private VisualElement CreateRow(Sprite icon, string displayName, int count, System.Action onClick)
    {
        VisualElement row = onClick != null ? new Button(onClick) : new VisualElement();
        row.AddToClassList("summary-row");

        VisualElement iconElement = new VisualElement();
        iconElement.AddToClassList("summary-row-icon");
        iconElement.style.backgroundImage = new StyleBackground(icon);
        row.Add(iconElement);

        Label nameLabel = new Label(displayName);
        nameLabel.AddToClassList("summary-row-name");
        row.Add(nameLabel);

        Label countLabel = new Label(count.ToString());
        countLabel.AddToClassList("summary-row-count");
        row.Add(countLabel);

        return row;
    }

    private void ShowCriteriaPopup(SpeciesDefinition species)
    {
        AudioManager.Instance?.PlayTap();
        criteriaPopupTitle.text = species.displayName;
        criteriaPopupMessage.text = BuildCriteriaText(species, vegetationDisplayNamesById);
        criteriaPopupRoot.style.display = DisplayStyle.Flex;
    }

    private void OnCriteriaPopupCloseButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        HideCriteriaPopup();
    }

    private void HideCriteriaPopup()
    {
        if (criteriaPopupRoot != null)
        {
            criteriaPopupRoot.style.display = DisplayStyle.None;
        }
    }

    // Pure grouping logic, extracted for direct unit testing (see SpeciesSummaryTests.cs).
    public static Dictionary<string, int> GroupTreeCountsByVegetationId(IReadOnlyList<PlantedTree> trees)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (PlantedTree tree in trees)
        {
            if (tree == null || tree.Vegetation == null)
            {
                continue;
            }

            counts.TryGetValue(tree.Vegetation.id, out int count);
            counts[tree.Vegetation.id] = count + 1;
        }

        return counts;
    }

    // Pure filtering logic, extracted for direct unit testing (see SpeciesSummaryTests.cs).
    public static List<SpeciesDefinition> FilterPresentSpecies(List<SpeciesDefinition> allSpecies, SpeciesDefinition.SpeciesCategory category)
    {
        List<SpeciesDefinition> result = new List<SpeciesDefinition>();
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species != null && species.category == category && EcosystemState.GetSpeciesPopulation(species.id) > 0)
            {
                result.Add(species);
            }
        }

        return result;
    }

    // Pure text-building logic, extracted for direct unit testing (see SpeciesSummaryTests.cs).
    // Surfaces the same criteria EmergenceSystem.CanRelease already gates on, as human-readable
    // text - does not change or duplicate the actual gating decision.
    public static string BuildCriteriaText(SpeciesDefinition species, Dictionary<string, string> vegetationDisplayNamesById)
    {
        if (species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            int totalPrey = 0;
            if (species.preySpeciesIds != null)
            {
                foreach (string preyId in species.preySpeciesIds)
                {
                    totalPrey += EcosystemState.GetSpeciesPopulation(preyId);
                }
            }

            return $"Needs {species.preyThreshold}+ prey present (currently {totalPrey}). Capacity: 1 per {species.preyToPredatorRatio} prey.";
        }

        string text = species.treesPerIndividualCap > 0
            ? $"Needs {species.treesPerIndividualCap} trees planted per individual (currently {EcosystemState.TotalTreeCount} trees)."
            : "No tree requirement.";

        if (species.requiredVegetationIds != null && species.requiredVegetationIds.Length > 0)
        {
            List<string> names = new List<string>();
            foreach (string vegetationId in species.requiredVegetationIds)
            {
                string name = vegetationDisplayNamesById != null && vegetationDisplayNamesById.TryGetValue(vegetationId, out string found)
                    ? found
                    : vegetationId;
                names.Add(name);
            }

            text += $" Requires: {string.Join(", ", names)}.";
        }

        return text;
    }
}
