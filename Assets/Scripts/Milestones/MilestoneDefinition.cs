using UnityEngine;

[CreateAssetMenu(fileName = "MilestoneDefinition", menuName = "Grove/Milestone Definition")]
public class MilestoneDefinition : ScriptableObject
{
    public enum RequirementType
    {
        TotalTreesPlanted,
        SpeciesUnlockedCount,
        FirstPredatorEmerged
    }

    public string id;
    public RequirementType requirementType;
    public int requirementValue;
    public int rewardDailyFundsBonus;
    public int rewardGems;
}
