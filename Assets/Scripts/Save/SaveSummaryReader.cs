using System.IO;
using UnityEngine;

public static class SaveSummaryReader
{
    public struct SaveSummary
    {
        public bool SaveExists;
        public bool FileExists;
        public int Level;
    }

    public static SaveSummary ReadSummary(string savePath)
    {
        if (!File.Exists(savePath))
        {
            return new SaveSummary { SaveExists = false, FileExists = false, Level = 0 };
        }

        try
        {
            string json = File.ReadAllText(savePath);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            return new SaveSummary { SaveExists = data != null, FileExists = true, Level = data != null ? data.level : 0 };
        }
        catch (System.Exception)
        {
            return new SaveSummary { SaveExists = false, FileExists = true, Level = 0 };
        }
    }
}
