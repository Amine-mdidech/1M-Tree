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
    }

    [Test]
    public void ReadSummary_ValidFile_ReturnsSaveExistsAndFileExists()
    {
        PlayerSaveData data = new PlayerSaveData();
        File.WriteAllText(tempPath, JsonUtility.ToJson(data));

        SaveSummaryReader.SaveSummary summary = SaveSummaryReader.ReadSummary(tempPath);

        Assert.IsTrue(summary.SaveExists);
        Assert.IsTrue(summary.FileExists);
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
}
