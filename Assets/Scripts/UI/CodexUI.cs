using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CodexUI : MonoBehaviour
{
    private enum EntryKind
    {
        Species,
        Vegetation
    }

    private class BookEntry
    {
        public EntryKind Kind;
        public SpeciesDefinition Species;
        public VegetationDefinition Vegetation;
    }

    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement codexRoot;
    private VisualElement codexFrame;
    private Label pageCategoryLabel;
    private Label pageNameLabel;
    private Label pageSubtitleLabel;
    private Label pageBodyLabel;
    private Label pageConditionsLabel;
    private Label pageIndicatorLabel;

    private readonly List<BookEntry> pages = new List<BookEntry>();
    private int currentPageIndex;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        codexRoot = root.Q<VisualElement>("codex-root");

        codexFrame = root.Q<VisualElement>("codex-frame");
        codexFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateWoodGrainTexture(256, 256, new Color(0.55f, 0.35f, 0.16f)));

        VisualElement bookPage = root.Q<VisualElement>("book-page");
        bookPage.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateParchmentTexture(256, 256, new Color(0.93f, 0.87f, 0.72f)));

        pageCategoryLabel = root.Q<Label>("page-category");
        pageNameLabel = root.Q<Label>("page-name");
        pageSubtitleLabel = root.Q<Label>("page-subtitle");
        pageBodyLabel = root.Q<Label>("page-body");
        pageConditionsLabel = root.Q<Label>("page-conditions");
        pageIndicatorLabel = root.Q<Label>("page-indicator");

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;
        root.Q<Button>("prev-button").clicked += ShowPreviousPage;
        root.Q<Button>("next-button").clicked += ShowNextPage;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        codexRoot.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            ToggleCodex();
        }

        if (codexRoot != null && codexRoot.style.display == DisplayStyle.Flex)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                ShowPreviousPage();
            }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                ShowNextPage();
            }
        }
    }

    private void BuildPages()
    {
        pages.Clear();

        AddSpeciesPages(SpeciesDefinition.SpeciesCategory.Herbivore);
        AddSpeciesPages(SpeciesDefinition.SpeciesCategory.Predator);

        foreach (VegetationDefinition vegetation in allVegetation)
        {
            if (vegetation == null)
            {
                continue;
            }

            pages.Add(new BookEntry { Kind = EntryKind.Vegetation, Vegetation = vegetation });
        }

        if (currentPageIndex >= pages.Count)
        {
            currentPageIndex = 0;
        }
    }

    private void AddSpeciesPages(SpeciesDefinition.SpeciesCategory category)
    {
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species != null && species.category == category)
            {
                pages.Add(new BookEntry { Kind = EntryKind.Species, Species = species });
            }
        }
    }

    private bool IsUnlocked(SpeciesDefinition species)
    {
        return species.unlockedByDefault || (emergenceSystem != null && emergenceSystem.IsSpeciesUnlocked(species.id));
    }

    private bool IsUnlocked(VegetationDefinition vegetation)
    {
        return vegetation.unlockedByDefault || (treePlanter != null && treePlanter.IsVegetationUnlocked(vegetation.id));
    }

    private void RenderCurrentPage()
    {
        if (pages.Count == 0)
        {
            pageCategoryLabel.text = "";
            pageNameLabel.text = "No entries yet";
            pageSubtitleLabel.text = "";
            pageBodyLabel.text = "";
            pageConditionsLabel.text = "";
            pageIndicatorLabel.text = "0 / 0";
            return;
        }

        BookEntry entry = pages[currentPageIndex];

        if (entry.Kind == EntryKind.Species)
        {
            SpeciesDefinition species = entry.Species;
            bool unlocked = IsUnlocked(species);

            pageCategoryLabel.text = species.category.ToString();
            pageNameLabel.text = unlocked ? species.displayName : "???";

            if (unlocked)
            {
                pageSubtitleLabel.text = string.IsNullOrEmpty(species.documentaryTitle) ? species.displayName : species.documentaryTitle;
                pageBodyLabel.text = string.IsNullOrEmpty(species.documentaryText) ? "Documentary text coming soon." : species.documentaryText;
            }
            else
            {
                pageSubtitleLabel.text = "Not yet researched";
                pageBodyLabel.text = "Unlock this species in the lab to reveal its documentary entry.";
            }

            pageConditionsLabel.text = DescribeEmergenceConditions(species);
        }
        else
        {
            VegetationDefinition vegetation = entry.Vegetation;
            bool unlocked = IsUnlocked(vegetation);

            pageCategoryLabel.text = "Plant";
            pageNameLabel.text = unlocked ? vegetation.displayName : "???";

            if (unlocked)
            {
                pageSubtitleLabel.text = "Growth stages";
                pageBodyLabel.text = DescribeGrowthStages(vegetation);
            }
            else
            {
                pageSubtitleLabel.text = "Not yet researched";
                pageBodyLabel.text = "Unlock this plant in the lab to reveal its growth stages.";
            }

            pageConditionsLabel.text = "";
        }

        pageIndicatorLabel.text = $"{currentPageIndex + 1} / {pages.Count}";
    }

    private string DescribeEmergenceConditions(SpeciesDefinition species)
    {
        List<string> parts = new List<string>();

        if (species.requiredVegetationIds != null && species.requiredVegetationIds.Length > 0)
        {
            List<string> vegetationNames = new List<string>();
            foreach (string vegetationId in species.requiredVegetationIds)
            {
                VegetationDefinition required = treePlanter != null ? treePlanter.FindVegetationById(vegetationId) : null;
                vegetationNames.Add(required != null ? required.displayName : vegetationId);
            }

            parts.Add("Requires vegetation: " + string.Join(", ", vegetationNames));
        }

        if (species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            List<string> preyNames = new List<string>();
            if (species.preySpeciesIds != null)
            {
                foreach (string preyId in species.preySpeciesIds)
                {
                    SpeciesDefinition prey = FindSpeciesById(preyId);
                    preyNames.Add(prey != null ? prey.displayName : preyId);
                }
            }

            string preyList = preyNames.Count > 0 ? string.Join(", ", preyNames) : "prey";
            parts.Add($"Hunts: {preyList} (needs {species.preyThreshold}+ prey population, 1 predator per {species.preyToPredatorRatio} prey)");
        }
        else if (species.treesPerIndividualCap > 0)
        {
            parts.Add($"Population limit: 1 per {species.treesPerIndividualCap} trees");
        }
        else
        {
            parts.Add("No population limit");
        }

        return string.Join("\n", parts);
    }

    private SpeciesDefinition FindSpeciesById(string id)
    {
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species != null && species.id == id)
            {
                return species;
            }
        }

        return null;
    }

    private static string DescribeGrowthStages(VegetationDefinition vegetation)
    {
        if (vegetation.growthStages == null || vegetation.growthStages.Length == 0)
        {
            return "";
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < vegetation.growthStages.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(" -> ");
            }

            sb.Append($"{vegetation.growthStages[i].stageName} ({vegetation.growthStages[i].durationSeconds}s)");
        }

        return sb.ToString();
    }

    private void ShowNextPage()
    {
        if (pages.Count == 0)
        {
            return;
        }

        currentPageIndex = (currentPageIndex + 1) % pages.Count;
        RenderCurrentPage();
    }

    private void ShowPreviousPage()
    {
        if (pages.Count == 0)
        {
            return;
        }

        currentPageIndex = (currentPageIndex - 1 + pages.Count) % pages.Count;
        RenderCurrentPage();
    }

    [ContextMenu("Debug: Toggle Codex")]
    public void ToggleCodex()
    {
        bool isVisible = codexRoot.style.display == DisplayStyle.Flex;
        SetOpen(!isVisible);
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            BuildPages();
            RenderCurrentPage();
            PanelAnimator.Open(codexRoot, codexFrame);
        }
        else
        {
            PanelAnimator.Close(codexRoot, codexFrame);
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

    private void OnDisable()
    {
        IsOpen = false;
    }
}
