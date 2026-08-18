using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenuUI : MonoBehaviour
{
    private const string GameplaySceneName = "SampleScene";

    [SerializeField] private List<MilestoneDefinition> allMilestones = new List<MilestoneDefinition>();
    [SerializeField] private SettingsUI settingsUI;

    private UIDocument uiDocument;
    private VisualElement confirmRoot;
    private VisualElement confirmFrame;
    private Label treesPlantedLabel;
    private Button playButton;
    private Button newGameButton;
    private ScrollView milestonesScroll;

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;

        confirmRoot = root.Q<VisualElement>("confirm-root");
        confirmFrame = root.Q<VisualElement>("confirm-frame");
        // Direct hide (not PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash.
        confirmRoot.style.display = DisplayStyle.None;

        treesPlantedLabel = root.Q<Label>("menu-trees-planted");
        playButton = root.Q<Button>("play-button");
        newGameButton = root.Q<Button>("new-game-button");
        milestonesScroll = root.Q<ScrollView>("milestones-scroll");

        playButton.clicked += OnPlayButtonClicked;
        newGameButton.clicked += OnNewGameButtonClicked;
        root.Q<Button>("confirm-yes-button").clicked += OnConfirmYesButtonClicked;
        root.Q<Button>("confirm-no-button").clicked += OnConfirmNoButtonClicked;
        root.Q<Button>("settings-button").clicked += OnSettingsButtonClicked;
        root.Q<Button>("quit-button").clicked += OnQuitButtonClicked;

        Refresh();
    }

    private void Refresh()
    {
        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(SavePath);

        playButton.text = summary.SaveExists ? "Continue" : "Play";
        newGameButton.style.display = summary.SaveExists ? DisplayStyle.Flex : DisplayStyle.None;
        treesPlantedLabel.text = $"Trees planted: {summary.TreesPlanted}";

        RefreshMilestones(summary.CompletedMilestoneIds);
    }

    private void RefreshMilestones(List<string> completedMilestoneIds)
    {
        milestonesScroll.Clear();

        foreach (MilestoneDefinition milestone in allMilestones)
        {
            if (milestone == null || completedMilestoneIds.Contains(milestone.id))
            {
                continue;
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("menu-milestone-row");

            Label requirementLabel = new Label(MilestoneLabelFormatter.FormatRequirement(milestone));
            requirementLabel.AddToClassList("menu-milestone-requirement");
            row.Add(requirementLabel);

            string reward = MilestoneLabelFormatter.FormatReward(milestone);
            if (!string.IsNullOrEmpty(reward))
            {
                Label rewardLabel = new Label(reward);
                rewardLabel.AddToClassList("menu-milestone-reward");
                row.Add(rewardLabel);
            }

            milestonesScroll.Add(row);
        }
    }

    private void OnPlayButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        SceneManager.LoadScene(GameplaySceneName);
    }

    private void OnNewGameButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        PanelAnimator.Open(confirmRoot, confirmFrame);
    }

    private void OnConfirmYesButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        SceneManager.LoadScene(GameplaySceneName);
    }

    private void OnConfirmNoButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        PanelAnimator.Close(confirmRoot, confirmFrame);
    }

    private void OnSettingsButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        settingsUI?.SetOpen(true);
    }

    private void OnQuitButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        Application.Quit();
    }
}
