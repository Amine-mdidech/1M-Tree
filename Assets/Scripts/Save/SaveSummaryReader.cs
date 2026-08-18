using System.IO;
using UnityEngine;

public static class SaveSummaryReader
{
    public struct SaveSummary
    {
        public bool SaveExists;
        public bool FileExists;
    }

    public static SaveSummary ReadSummary(string savePath)
    {
        if (!File.Exists(savePath))
        {
            return new SaveSummary { SaveExists = false, FileExists = false };
        }

        try
        {
            string json = File.ReadAllText(savePath);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            return new SaveSummary { SaveExists = data != null, FileExists = true };
        }
        catch (System.Exception)
        {
            return new SaveSummary { SaveExists = false, FileExists = true };
        }
    }
}
