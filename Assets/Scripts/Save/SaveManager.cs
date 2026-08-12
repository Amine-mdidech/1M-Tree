using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    private const int MaxOfflineSeconds = 24 * 60 * 60;

    [SerializeField] private DailyGrant dailyGrant;
    [SerializeField] private MilestoneManager milestoneManager;
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private ResearchManager researchManager;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private LandManager landManager;
    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private bool hasLoadedThisSession;

    private void Start()
    {
        LoadGame();
        hasLoadedThisSession = true;
    }

    private void OnApplicationQuit()
    {
        if (!hasLoadedThisSession)
        {
            return;
        }

        SaveGame();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (!isPaused || !hasLoadedThisSession)
        {
            return;
        }

        SaveGame();
    }

    [ContextMenu("Debug: Save Now")]
    public void SaveGame()
    {
        PlayerSaveData data = new PlayerSaveData
        {
            treesPlantedToday = dailyGrant.TreesPlantedToday,
            dailyTreeCap = dailyGrant.DailyTreeCap,
            lastGrantResetTimestamp = dailyGrant.LastGrantResetTimestamp,
            lastSessionTimestamp = DateTime.UtcNow.ToString("o"),
            gems = milestoneManager.Gems,
            level = playerLevel.Level,
            xp = playerLevel.CurrentXp,
            landTier = landManager.CurrentTierIndex,
            unlockedSpeciesIds = emergenceSystem.GetUnlockedSpeciesIds(),
            unlockedVegetationIds = treePlanter.GetUnlockedVegetationIds(),
            completedMilestoneIds = new List<string>(milestoneManager.CompletedMilestoneIds)
        };

        foreach (KeyValuePair<string, int> entry in EcosystemState.GetAllPopulations())
        {
            data.speciesPopulation.Add(new SpeciesPopulationEntry { speciesId = entry.Key, population = entry.Value });
        }

        foreach (PlantedTree tree in PlantedTree.ActiveTrees)
        {
            // ActiveTrees can transiently hold a destroyed entry (Destroy()/DestroyImmediate()
            // don't always invoke OnDestroy synchronously) - skip it rather than saving garbage
            // or throwing mid-save.
            if (tree == null)
            {
                continue;
            }

            data.plantedTrees.Add(new PlantedTreeSaveData
            {
                vegetationId = tree.Vegetation.id,
                plantedAt = tree.PlantedAtUtc.ToString("o"),
                stage = tree.CurrentStageName,
                elapsedSecondsInStage = tree.ElapsedSecondsInStage,
                positionX = tree.transform.position.x,
                positionY = tree.transform.position.y
            });
        }

        if (researchManager.IsResearching && researchManager.ActiveProject != null)
        {
            data.activeResearch = new ActiveResearchSaveData
            {
                projectId = researchManager.ActiveProject.id,
                remainingSeconds = researchManager.RemainingSeconds
            };
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Game saved to {SavePath}");
    }

    [ContextMenu("Debug: Load Now")]
    public void LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No save file found; starting fresh.");
            return;
        }

        string json = File.ReadAllText(SavePath);
        PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);

        dailyGrant.LoadState(data.treesPlantedToday, data.dailyTreeCap, data.lastGrantResetTimestamp);
        milestoneManager.LoadState(data.gems, data.completedMilestoneIds);
        playerLevel.LoadState(data.level, data.xp);
        landManager.LoadState(data.landTier);

        foreach (string speciesId in data.unlockedSpeciesIds)
        {
            SpeciesDefinition species = FindSpeciesById(speciesId);
            if (species != null)
            {
                emergenceSystem.AddUnlockedSpecies(species);
            }
        }

        foreach (string vegetationId in data.unlockedVegetationIds)
        {
            VegetationDefinition vegetation = treePlanter.FindVegetationById(vegetationId);
            if (vegetation != null)
            {
                treePlanter.AddUnlockedVegetation(vegetation);
            }
        }

        EcosystemState.ResetAll();
        foreach (SpeciesPopulationEntry entry in data.speciesPopulation)
        {
            EcosystemState.SetSpeciesPopulation(entry.speciesId, entry.population);
        }

        double offlineSeconds = RestorePlantedTrees(data);

        // Catch up emergence ticks for the real-world time the app was closed, the same way
        // RestorePlantedTrees just caught up tree growth - otherwise a session shorter than one
        // tick interval (60s) could never see a new animal, and any time spent fully closed
        // between sessions would be silently discarded instead of counting toward the next tick.
        emergenceSystem.AdvanceTicks((float)offlineSeconds);
        emergenceSystem.SpawnMissingCreatureVisuals();

        if (data.activeResearch != null)
        {
            researchManager.LoadActiveResearch(data.activeResearch.projectId, data.activeResearch.remainingSeconds);
        }

        Debug.Log($"Game loaded from {SavePath}");
    }

    private double RestorePlantedTrees(PlayerSaveData data)
    {
        foreach (PlantedTree existing in PlantedTree.ActiveTrees)
        {
            Destroy(existing.gameObject);
        }

        // Destroy() defers actual removal (and PlantedTree.OnDisable) to end of frame; clear the
        // registry immediately so trees spawned below don't sit alongside stale zombie entries
        // for the rest of this frame.
        PlantedTree.ActiveTrees.Clear();

        DateTime lastSession = ParseOrNow(data.lastSessionTimestamp);
        double offlineSeconds = Math.Max(0, (DateTime.UtcNow - lastSession).TotalSeconds);
        offlineSeconds = Math.Min(offlineSeconds, MaxOfflineSeconds);

        foreach (PlantedTreeSaveData treeData in data.plantedTrees)
        {
            VegetationDefinition vegetation = treePlanter.FindVegetationById(treeData.vegetationId);
            if (vegetation == null)
            {
                continue;
            }

            Vector3 position = new Vector3(treeData.positionX, treeData.positionY, 0f);
            PlantedTree tree = treePlanter.SpawnRestoredTree(vegetation, position);

            int stageIndex = FindStageIndexByName(vegetation, treeData.stage);
            tree.RestoreState(vegetation, stageIndex, treeData.elapsedSecondsInStage, ParseOrNow(treeData.plantedAt));
            tree.AdvanceGrowth((float)offlineSeconds);

            EcosystemState.RegisterTreePlanted(vegetation.id);
        }

        return offlineSeconds;
    }

    private static DateTime ParseOrNow(string iso)
    {
        return DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out DateTime parsed) ? parsed : DateTime.UtcNow;
    }

    private static int FindStageIndexByName(VegetationDefinition vegetation, string stageName)
    {
        for (int i = 0; i < vegetation.growthStages.Length; i++)
        {
            if (vegetation.growthStages[i].stageName == stageName)
            {
                return i;
            }
        }

        return 0;
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

    [ContextMenu("Debug: Delete Save File")]
    public void DebugDeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Save file deleted.");
        }

        // Deleting the file alone doesn't touch anything already loaded - every manager's fields,
        // every planted tree/creature GameObject, and the static EcosystemState/ActiveTrees/
        // ActiveCreatures registries would all still hold the old game. A scene reload is the only
        // way to reset every MonoBehaviour's state at once without hand-maintaining a reset method
        // per manager; the static registries don't get cleared by a scene reload on their own (they
        // have no GameObject lifecycle tying them to scene teardown), so they're reset explicitly
        // here first. hasLoadedThisSession is deliberately left true so OnApplicationQuit/Pause
        // during the reload doesn't skip a save; SaveManager.Start() -> LoadGame() will find no
        // file (just deleted) and correctly leave the reloaded scene's fresh defaults in place.
        EcosystemState.ResetAll();
        PlantedTree.ActiveTrees.Clear();
        CreatureVisual.ActiveCreatures.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }
}
