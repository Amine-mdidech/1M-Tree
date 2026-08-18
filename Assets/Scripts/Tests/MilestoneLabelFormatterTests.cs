using NUnit.Framework;
using UnityEngine;

public class MilestoneLabelFormatterTests
{
    private MilestoneDefinition milestone;

    [SetUp]
    public void SetUp()
    {
        milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(milestone);
    }

    [Test]
    public void FormatRequirement_TotalTreesPlanted_ReturnsPlantTreesLabel()
    {
        milestone.requirementType = MilestoneDefinition.RequirementType.TotalTreesPlanted;
        milestone.requirementValue = 50;

        Assert.AreEqual("Plant 50 trees", MilestoneLabelFormatter.FormatRequirement(milestone));
    }

    [Test]
    public void FormatRequirement_SpeciesUnlockedCount_ReturnsUnlockSpeciesLabel()
    {
        milestone.requirementType = MilestoneDefinition.RequirementType.SpeciesUnlockedCount;
        milestone.requirementValue = 2;

        Assert.AreEqual("Unlock 2 species", MilestoneLabelFormatter.FormatRequirement(milestone));
    }

    [Test]
    public void FormatRequirement_FirstPredatorEmerged_ReturnsFixedLabel()
    {
        milestone.requirementType = MilestoneDefinition.RequirementType.FirstPredatorEmerged;

        Assert.AreEqual("Release your first predator", MilestoneLabelFormatter.FormatRequirement(milestone));
    }

    [Test]
    public void FormatReward_BothRewards_JoinsWithComma()
    {
        milestone.rewardDailyFundsBonus = 15;
        milestone.rewardGems = 25;

        Assert.AreEqual("+15 daily funds, +25 gems", MilestoneLabelFormatter.FormatReward(milestone));
    }

    [Test]
    public void FormatReward_OnlyGems_OmitsFundsHalf()
    {
        milestone.rewardDailyFundsBonus = 0;
        milestone.rewardGems = 5;

        Assert.AreEqual("+5 gems", MilestoneLabelFormatter.FormatReward(milestone));
    }

    [Test]
    public void FormatReward_NoRewards_ReturnsEmptyString()
    {
        Assert.AreEqual("", MilestoneLabelFormatter.FormatReward(milestone));
    }
}
