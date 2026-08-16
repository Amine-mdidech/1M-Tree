using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("rewardDailyCapBonus")]
    public int rewardDailyFundsBonus;
    public int rewardGems;
}
