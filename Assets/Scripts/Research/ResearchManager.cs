using System.Collections.Generic;
using UnityEngine;

public class ResearchManager : MonoBehaviour
{
    [SerializeField] private List<ResearchProjectDefinition> availableResearchProjects = new List<ResearchProjectDefinition>();
    [SerializeField] private List<SpeciesDefinition> unlockableSpecies = new List<SpeciesDefinition>();
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private MilestoneManager milestoneManager;
    [SerializeField] private TreePlanter treePlanter;

    [SerializeField] private ResearchProjectDefinition activeProject;
    [SerializeField] private bool isResearching;
    [SerializeField] private float remainingSeconds;

    public bool IsResearching => isResearching;
    public float RemainingSeconds => remainingSeconds;
    public ResearchProjectDefinition ActiveProject => activeProject;
    public IReadOnlyList<ResearchProjectDefinition> AvailableResearchProjects => availableResearchProjects;

    private void Update()
    {
        if (!isResearching)
        {
            return;
        }

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds <= 0f)
        {
            CompleteResearch();
        }
    }

    public bool CanStartResearch(ResearchProjectDefinition project)
    {
        if (project == null || isResearching)
        {
            return false;
        }

        return !IsProjectUnlocked(project) && ArePrerequisitesMet(project);
    }

    public bool ArePrerequisitesMet(ResearchProjectDefinition project)
    {
        if (project == null || project.prerequisiteProjectIds == null)
        {
            return true;
        }

        foreach (string prerequisiteId in project.prerequisiteProjectIds)
        {
            ResearchProjectDefinition prerequisiteProject = FindProjectById(prerequisiteId);
            if (prerequisiteProject == null || !IsProjectUnlocked(prerequisiteProject))
            {
                return false;
            }
        }

        return true;
    }

    public ResearchProjectDefinition FindProjectById(string id)
    {
        foreach (ResearchProjectDefinition project in availableResearchProjects)
        {
            if (project != null && project.id == id)
            {
                return project;
            }
        }

        return null;
    }

    public bool IsProjectUnlocked(ResearchProjectDefinition project)
    {
        if (project == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(project.unlocksVegetationId))
        {
            return treePlanter != null && treePlanter.IsVegetationUnlocked(project.unlocksVegetationId);
        }

        return emergenceSystem != null && emergenceSystem.IsSpeciesUnlocked(project.unlocksSpeciesId);
    }

    public string GetProjectDisplayName(ResearchProjectDefinition project)
    {
        if (project == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(project.unlocksVegetationId))
        {
            VegetationDefinition vegetation = treePlanter != null ? treePlanter.FindVegetationById(project.unlocksVegetationId) : null;
            if (vegetation != null)
            {
                return vegetation.displayName;
            }
        }

        SpeciesDefinition species = FindUnlockableSpecies(project.unlocksSpeciesId);
        return species != null ? species.displayName : project.id;
    }

    public void StartResearch(ResearchProjectDefinition project)
    {
        if (!CanStartResearch(project))
        {
            return;
        }

        activeProject = project;
        isResearching = true;
        remainingSeconds = project.baseDurationSeconds;
        Debug.Log($"Research started: {project.id} ({project.baseDurationSeconds}s).");
        AudioManager.Instance?.PlayResearchStart();
    }

    public bool SkipWithGems()
    {
        if (!isResearching || activeProject == null || milestoneManager == null)
        {
            return false;
        }

        int remainingMinutes = Mathf.CeilToInt(remainingSeconds / 60f);
        int cost = activeProject.gemSkipCostPerRemainingMinute * remainingMinutes;

        if (!milestoneManager.TrySpendGems(cost))
        {
            Debug.Log($"Not enough gems to skip research (need {cost}).");
            AudioManager.Instance?.PlayError();
            return false;
        }

        Debug.Log($"Spent {cost} gems to skip remaining research.");
        CompleteResearch();
        return true;
    }

    private void CompleteResearch()
    {
        ResearchProjectDefinition finishedProject = activeProject;
        isResearching = false;
        remainingSeconds = 0f;
        activeProject = null;

        if (!string.IsNullOrEmpty(finishedProject.unlocksVegetationId))
        {
            VegetationDefinition vegetation = treePlanter != null ? treePlanter.FindVegetationById(finishedProject.unlocksVegetationId) : null;
            if (vegetation == null)
            {
                Debug.LogWarning($"Research complete but no VegetationDefinition found for id '{finishedProject.unlocksVegetationId}'.");
                return;
            }

            treePlanter.AddUnlockedVegetation(vegetation);
            Debug.Log($"Research complete: {vegetation.displayName} unlocked!");
            AudioManager.Instance?.PlayUnlock();
            Handheld.Vibrate();
            return;
        }

        SpeciesDefinition species = FindUnlockableSpecies(finishedProject.unlocksSpeciesId);
        if (species == null)
        {
            Debug.LogWarning($"Research complete but no SpeciesDefinition found for id '{finishedProject.unlocksSpeciesId}'.");
            return;
        }

        emergenceSystem.AddUnlockedSpecies(species);
        Debug.Log($"Research complete: {species.displayName} unlocked!");
        AudioManager.Instance?.PlayUnlock();
        Handheld.Vibrate();
    }

    public SpeciesDefinition FindUnlockableSpecies(string speciesId)
    {
        foreach (SpeciesDefinition species in unlockableSpecies)
        {
            if (species != null && species.id == speciesId)
            {
                return species;
            }
        }

        return null;
    }

    public void LoadActiveResearch(string projectId, float savedRemainingSeconds)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return;
        }

        foreach (ResearchProjectDefinition project in availableResearchProjects)
        {
            if (project != null && project.id == projectId)
            {
                activeProject = project;
                isResearching = true;
                remainingSeconds = savedRemainingSeconds;
                return;
            }
        }
    }

    [ContextMenu("Debug: Start Next Available Research")]
    private void DebugStartNextAvailable()
    {
        foreach (ResearchProjectDefinition project in availableResearchProjects)
        {
            if (CanStartResearch(project))
            {
                StartResearch(project);
                return;
            }
        }

        Debug.Log("No available research projects left to start.");
    }

    [ContextMenu("Debug: Instant-Complete Research")]
    public void DebugInstantComplete()
    {
        if (isResearching)
        {
            CompleteResearch();
        }
    }

    [ContextMenu("Debug: Skip With Gems")]
    private void DebugSkipWithGems()
    {
        SkipWithGems();
    }
}
