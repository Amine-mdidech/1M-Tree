using System.Collections.Generic;

public static class MilestoneLabelFormatter
{
    public static string FormatRequirement(MilestoneDefinition milestone)
    {
        switch (milestone.requirementType)
        {
            case MilestoneDefinition.RequirementType.TotalTreesPlanted:
                return $"Plant {milestone.requirementValue} trees";
            case MilestoneDefinition.RequirementType.SpeciesUnlockedCount:
                return $"Unlock {milestone.requirementValue} species";
            case MilestoneDefinition.RequirementType.FirstPredatorEmerged:
                return "Release your first predator";
            default:
                return milestone.id;
        }
    }

    public static string FormatReward(MilestoneDefinition milestone)
    {
        List<string> parts = new List<string>();

        if (milestone.rewardDailyFundsBonus > 0)
        {
            parts.Add($"+{milestone.rewardDailyFundsBonus} daily funds");
        }

        if (milestone.rewardGems > 0)
        {
            parts.Add($"+{milestone.rewardGems} gems");
        }

        return string.Join(", ", parts);
    }
}
