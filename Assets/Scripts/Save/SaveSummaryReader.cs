using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSummaryReader
{
    public struct SaveSummary
    {
        public bool SaveExists;
        public int TreesPlanted;
        public List<string> CompletedMilestoneIds;
    }

    public static SaveSummary ReadSummary(string savePath)
    {
        if (!File.Exists(savePath))
        {
            return Empty();
        }

        try
        {
            string json = File.ReadAllText(savePath);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            if (data == null)
            {
                return Empty();
            }

            return new SaveSummary
            {
                SaveExists = true,
                TreesPlanted = data.plantedTrees.Count,
                CompletedMilestoneIds = data.completedMilestoneIds
            };
        }
        catch (System.Exception)
        {
            return Empty();
        }
    }

    private static SaveSummary Empty()
    {
        return new SaveSummary
        {
            SaveExists = false,
            TreesPlanted = 0,
            CompletedMilestoneIds = new List<string>()
        };
    }
}
