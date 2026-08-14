using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DemoUI : MonoBehaviour
{
    private const int GemGrant = 100;

    [SerializeField] private MilestoneManager milestoneManager;
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private ResearchManager researchManager;
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement demoRoot;
    private VisualElement demoFrame;
    private Label statusLabel;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        demoRoot = root.Q<VisualElement>("demo-root");

        demoFrame = root.Q<VisualElement>("demo-frame");
        demoFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.32f, 0.34f, 0.36f)));

        ScrollView demoScroll = root.Q<ScrollView>("demo-scroll");
        demoScroll.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.85f, 0.86f, 0.87f)));
        demoScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        demoScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        statusLabel = root.Q<Label>("demo-status");

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;
        root.Q<Button>("add-gems-button").clicked += OnAddGemsClicked;
        root.Q<Button>("level-up-button").clicked += OnLevelUpClicked;
        root.Q<Button>("unlock-species-button").clicked += OnUnlockSpeciesClicked;
        root.Q<Button>("unlock-vegetation-button").clicked += OnUnlockVegetationClicked;
        root.Q<Button>("finish-research-button").clicked += OnFinishResearchClicked;
        root.Q<Button>("mature-trees-button").clicked += OnMatureTreesClicked;
        root.Q<Button>("release-gazelle-button").clicked += () => OnReleaseSpeciesClicked("gazelle");
        root.Q<Button>("release-squirrel-button").clicked += () => OnReleaseSpeciesClicked("squirrel");
        root.Q<Button>("release-wolf-button").clicked += () => OnReleaseSpeciesClicked("wolf");
        root.Q<Button>("release-fox-button").clicked += () => OnReleaseSpeciesClicked("fox");
        root.Q<Button>("plant-batch-button").clicked += OnPlantBatchClicked;
        root.Q<Button>("reset-save-button").clicked += OnResetSaveClicked;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        demoRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            ToggleDemo();
        }
    }

    [ContextMenu("Debug: Toggle Demo Panel")]
    public void ToggleDemo()
    {
        bool isVisible = demoRoot.style.display == DisplayStyle.Flex;
        SetOpen(!isVisible);
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            PanelAnimator.Open(demoRoot, demoFrame);
        }
        else
        {
            PanelAnimator.Close(demoRoot, demoFrame);
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

    private void SetStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }

    private void OnAddGemsClicked()
    {
        if (milestoneManager == null)
        {
            return;
        }

        milestoneManager.AddGems(GemGrant);
        SetStatus($"+{GemGrant} gems.");
    }

    private void OnLevelUpClicked()
    {
        if (playerLevel == null)
        {
            return;
        }

        // Top up exactly to the XP threshold instead of adding a big flat chunk, so this always
        // triggers exactly one level-up regardless of how much XP is already banked.
        int xpNeeded = playerLevel.XpToNextLevel - playerLevel.CurrentXp;
        playerLevel.AddXp(xpNeeded);
        SetStatus($"Leveled up to level {playerLevel.Level}.");
    }

    private void OnUnlockSpeciesClicked()
    {
        if (emergenceSystem == null)
        {
            return;
        }

        int unlockedCount = 0;
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species == null || emergenceSystem.IsSpeciesUnlocked(species.id))
            {
                continue;
            }

            emergenceSystem.AddUnlockedSpecies(species);
            unlockedCount++;
        }

        SetStatus($"Unlocked {unlockedCount} species.");
    }

    private void OnUnlockVegetationClicked()
    {
        if (treePlanter == null)
        {
            return;
        }

        int unlockedCount = 0;
        foreach (VegetationDefinition vegetation in allVegetation)
        {
            if (vegetation == null || treePlanter.IsVegetationUnlocked(vegetation.id))
            {
                continue;
            }

            treePlanter.AddUnlockedVegetation(vegetation);
            unlockedCount++;
        }

        SetStatus($"Unlocked {unlockedCount} plants.");
    }

    private void OnFinishResearchClicked()
    {
        if (researchManager == null)
        {
            return;
        }

        if (!researchManager.IsResearching)
        {
            SetStatus("No research is currently active.");
            return;
        }

        researchManager.DebugInstantComplete();
        SetStatus("Active research completed.");
    }

    private void OnMatureTreesClicked()
    {
        int maturedCount = 0;
        // Iterate a snapshot copy: CompleteGrowthInstantly() regenerates each tree's sprite, and
        // maturing changes nothing about registry membership, but copying avoids ever depending
        // on ActiveTrees staying unmodified mid-loop as this debug action evolves.
        foreach (PlantedTree tree in new List<PlantedTree>(PlantedTree.ActiveTrees))
        {
            // ActiveTrees can transiently hold a destroyed entry (Destroy()/DestroyImmediate()
            // don't always invoke OnDestroy synchronously) - skip it rather than throwing.
            if (tree == null || tree.IsMature)
            {
                continue;
            }

            tree.CompleteGrowthInstantly();
            maturedCount++;
        }

        SetStatus($"Matured {maturedCount} trees.");
    }

    private void OnReleaseSpeciesClicked(string speciesId)
    {
        if (emergenceSystem == null)
        {
            return;
        }

        SpeciesDefinition species = FindSpeciesById(speciesId);
        if (species == null)
        {
            SetStatus($"No species definition wired for '{speciesId}'.");
            return;
        }

        bool released = emergenceSystem.TryReleaseSpecies(species);
        SetStatus(released
            ? $"Released a {species.displayName}."
            : $"Can't release {species.displayName} yet - criteria not met.");
    }

    private SpeciesDefinition FindSpeciesById(string speciesId)
    {
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species != null && species.id == speciesId)
            {
                return species;
            }
        }

        return null;
    }

    private void OnPlantBatchClicked()
    {
        if (treePlanter == null)
        {
            return;
        }

        treePlanter.DebugPlantBatch();
        SetStatus("Planted a batch of trees.");
    }

    private void OnResetSaveClicked()
    {
        if (saveManager == null)
        {
            return;
        }

        // DebugDeleteSave() now reloads the scene as its last step to reset every manager back to
        // a fresh game - nothing after this call can safely touch `this` (or any other object from
        // the scene being reloaded), so it must be the last thing this method does.
        saveManager.DebugDeleteSave();
    }
}
