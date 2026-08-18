using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSummaryReader
{
    public struct SaveSummary
    {
        public bool SaveExists;
        public bool FileExists;
        public int TreesPlanted;
        public List<string> CompletedMilestoneIds;
    }

    public static SaveSummary ReadSummary(string savePath)
    {
        if (!File.Exists(savePath))
        {
            return Empty(false);
        }

        try
        {
            string json = File.ReadAllText(savePath);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            if (data == null)
            {
                return Empty(true);
            }

            return new SaveSummary
            {
                SaveExists = true,
                FileExists = true,
                TreesPlanted = data.plantedTrees.Count,
                CompletedMilestoneIds = data.completedMilestoneIds ?? new List<string>()
            };
        }
        catch (System.Exception)
        {
            return Empty(true);
        }
    }

    private static SaveSummary Empty(bool fileExists)
    {
        return new SaveSummary
        {
            SaveExists = false,
            FileExists = fileExists,
            TreesPlanted = 0,
            CompletedMilestoneIds = new List<string>()
        };
    }
}
