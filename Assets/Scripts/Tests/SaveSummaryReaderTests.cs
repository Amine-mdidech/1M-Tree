using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SaveSummaryReaderTests
{
    private string tempPath;

    [SetUp]
    public void SetUp()
    {
        tempPath = Path.Combine(Path.GetTempPath(), "save_summary_reader_test_" + Guid.NewGuid() + ".json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }

    [Test]
    public void ReadSummary_NoFile_ReturnsSaveDoesNotExist()
    {
        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        Assert.IsFalse(summary.SaveExists);
        Assert.IsFalse(summary.FileExists);
        Assert.AreEqual(0, summary.TreesPlanted);
        Assert.IsEmpty(summary.CompletedMilestoneIds);
    }

    [Test]
    public void ReadSummary_ValidFile_ReturnsTreesPlantedCount()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.plantedTrees.Add(new PlantedTreeSaveData { vegetationId = "oak" });
        data.plantedTrees.Add(new PlantedTreeSaveData { vegetationId = "pine" });
        File.WriteAllText(tempPath, JsonUtility.ToJson(data));

        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        Assert.IsTrue(summary.SaveExists);
        Assert.IsTrue(summary.FileExists);
        Assert.AreEqual(2, summary.TreesPlanted);
    }

    [Test]
    public void ReadSummary_ValidFile_ReturnsCompletedMilestoneIds()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.completedMilestoneIds.Add("first_10_trees");
        File.WriteAllText(tempPath, JsonUtility.ToJson(data));

        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        CollectionAssert.AreEquivalent(new[] { "first_10_trees" }, summary.CompletedMilestoneIds);
    }

    [Test]
    public void ReadSummary_CorruptFile_ReturnsSaveDoesNotExist()
    {
        File.WriteAllText(tempPath, "not valid json at all, just plain text");

        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        Assert.IsFalse(summary.SaveExists);
    }

    [Test]
    public void ReadSummary_CorruptFile_StillReportsFileExists()
    {
        File.WriteAllText(tempPath, "not valid json at all, just plain text");

        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        Assert.IsTrue(summary.FileExists);
    }

    [Test]
    public void ReadSummary_NullCompletedMilestoneIds_ReturnsEmptyListInsteadOfNull()
    {
        // Simulates a hand-edited/corrupted save where JsonUtility parses successfully
        // but "completedMilestoneIds" is explicitly null in the JSON, bypassing the
        // field initializer default.
        File.WriteAllText(tempPath, "{\"completedMilestoneIds\":null}");

        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        Assert.IsNotNull(summary.CompletedMilestoneIds);
        Assert.IsEmpty(summary.CompletedMilestoneIds);
    }
}
