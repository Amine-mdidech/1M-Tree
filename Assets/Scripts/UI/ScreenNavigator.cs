using UnityEngine;

public class ScreenNavigator : MonoBehaviour
{
    [SerializeField] private HudUI hudUI;
    [SerializeField] private CodexUI codexUI;
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private DemoUI demoUI;
    [SerializeField] private SettingsUI settingsUI;
    [SerializeField] private MapUI mapUI;
    [SerializeField] private SpeciesSummaryUI speciesSummaryUI;
    [SerializeField] private TreeBoostUI treeBoostUI;

    public void OpenCodex()
    {
        CloseOtherPanels(codexUI);
        if (codexUI != null) codexUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void OpenLab()
    {
        CloseOtherPanels(laboratoryUI);
        if (laboratoryUI != null) laboratoryUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void OpenProfile()
    {
        CloseOtherPanels(profileUI);
        if (profileUI != null) profileUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void OpenSettings()
    {
        CloseOtherPanels(settingsUI);
        if (settingsUI != null) settingsUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void OpenMap()
    {
        CloseOtherPanels(mapUI);
        if (mapUI != null) mapUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void OpenDemo()
    {
        CloseOtherPanels(demoUI);
        if (demoUI != null) demoUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void OpenSpeciesSummary()
    {
        CloseOtherPanels(speciesSummaryUI);
        if (speciesSummaryUI != null) speciesSummaryUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }

    public void GoHome()
    {
        CloseOtherPanels(null);
        SetHomeToolbarVisible(true);
    }

    // `except` is skipped so opening a panel never redundantly closes-then-reopens itself in the
    // same call - the animated close schedules a delayed hide that must not fire after a reopen.
    private void CloseOtherPanels(object except)
    {
        if (codexUI != null && !ReferenceEquals(codexUI, except))
        {
            codexUI.SetOpen(false);
        }

        if (laboratoryUI != null && !ReferenceEquals(laboratoryUI, except))
        {
            laboratoryUI.SetOpen(false);
        }

        if (profileUI != null && !ReferenceEquals(profileUI, except))
        {
            profileUI.SetOpen(false);
        }

        if (demoUI != null && !ReferenceEquals(demoUI, except))
        {
            demoUI.SetOpen(false);
        }

        if (settingsUI != null && !ReferenceEquals(settingsUI, except))
        {
            settingsUI.SetOpen(false);
        }

        if (mapUI != null && !ReferenceEquals(mapUI, except))
        {
            mapUI.SetOpen(false);
        }

        if (speciesSummaryUI != null && !ReferenceEquals(speciesSummaryUI, except))
        {
            speciesSummaryUI.SetOpen(false);
        }

        // TreeBoostUI is a small contextual popup opened directly by CameraPanController on a tree
        // tap, not through this navigator's OpenX() methods - it's never passed as `except`, so it
        // always closes here, whichever full panel is opening.
        if (treeBoostUI != null)
        {
            treeBoostUI.Close();
        }
    }

    private void SetHomeToolbarVisible(bool visible)
    {
        if (hudUI != null)
        {
            hudUI.SetToolbarVisible(visible);
        }
    }
}
