using System.Collections.Generic;
using UnityEngine;

public class MilestoneManager : MonoBehaviour
{
    private const float CheckIntervalSeconds = 1f;

    [SerializeField] private List<MilestoneDefinition> milestones = new List<MilestoneDefinition>();
    [SerializeField] private List<string> completedMilestoneIds = new List<string>();
    [SerializeField] private int gems;
    [SerializeField] private DailyGrant dailyGrant;
    [SerializeField] private EmergenceSystem emergenceSystem;

    private float checkTimer;

    public int Gems => gems;
    public IReadOnlyList<string> CompletedMilestoneIds => completedMilestoneIds;
    public int TotalMilestoneCount => milestones.Count;

    public bool TrySpendGems(int amount)
    {
        if (amount < 0 || gems < amount)
        {
            return false;
        }

        gems -= amount;
        return true;
    }

    public void AddGems(int amount)
    {
        if (amount < 0)
        {
            return;
        }

        gems += amount;
    }

    [ContextMenu("Debug: Add 100 Gems")]
    private void DebugAddGems()
    {
        AddGems(100);
    }

    public void LoadState(int savedGems, List<string> savedCompletedMilestoneIds)
    {
        gems = savedGems;
        completedMilestoneIds.Clear();
        completedMilestoneIds.AddRange(savedCompletedMilestoneIds);
        RefreshEnabledState();
    }

    private void Start()
    {
        RefreshEnabledState();
    }

    private void Update()
    {
        checkTimer += Time.deltaTime;
        if (checkTimer >= CheckIntervalSeconds)
        {
            checkTimer = 0f;
            CheckMilestones();
        }
    }

    private void CheckMilestones()
    {
        foreach (MilestoneDefinition milestone in milestones)
        {
            if (milestone == null || completedMilestoneIds.Contains(milestone.id))
            {
                continue;
            }

            if (IsRequirementMet(milestone))
            {
                CompleteMilestone(milestone);
            }
        }

        RefreshEnabledState();
    }

    private void RefreshEnabledState()
    {
        // Nothing left to check once every milestone is completed, so stop paying per-frame overhead.
        enabled = completedMilestoneIds.Count < milestones.Count;
    }

    private bool IsRequirementMet(MilestoneDefinition milestone)
    {
        switch (milestone.requirementType)
        {
            case MilestoneDefinition.RequirementType.TotalTreesPlanted:
                return EcosystemState.TotalTreeCount >= milestone.requirementValue;

            case MilestoneDefinition.RequirementType.SpeciesUnlockedCount:
                return emergenceSystem != null && emergenceSystem.UnlockedSpeciesCount >= milestone.requirementValue;

            case MilestoneDefinition.RequirementType.FirstPredatorEmerged:
                return emergenceSystem != null && emergenceSystem.HasAnyPredatorEmerged();

            default:
                return false;
        }
    }

    private void CompleteMilestone(MilestoneDefinition milestone)
    {
        completedMilestoneIds.Add(milestone.id);

        if (milestone.rewardDailyCapBonus > 0 && dailyGrant != null)
        {
            dailyGrant.AddDailyCapBonus(milestone.rewardDailyCapBonus);
        }

        gems += milestone.rewardGems;

        Debug.Log($"Milestone completed: {milestone.id} (+{milestone.rewardDailyCapBonus} daily cap, +{milestone.rewardGems} gems).");
        AudioManager.Instance?.PlayMilestone();
        Handheld.Vibrate();
    }
}
