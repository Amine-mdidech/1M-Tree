using System;
using System.Collections.Generic;

[Serializable]
public class PlantedTreeSaveData
{
    public string vegetationId;
    public string plantedAt;
    public string stage;
    public float elapsedSecondsInStage;
    public float positionX;
    public float positionY;
}

[Serializable]
public class SpeciesPopulationEntry
{
    public string speciesId;
    public int population;
}

[Serializable]
public class ActiveResearchSaveData
{
    public string projectId;
    public float remainingSeconds;
}

[Serializable]
public class PlayerSaveData
{
    public int treesPlantedToday;
    public int dailyTreeCap;
    public string lastGrantResetTimestamp;
    public string lastSessionTimestamp;
    public int gems;
    public int level;
    public int xp;
    public int landTier;
    public List<PlantedTreeSaveData> plantedTrees = new List<PlantedTreeSaveData>();
    public List<SpeciesPopulationEntry> speciesPopulation = new List<SpeciesPopulationEntry>();
    public List<string> unlockedSpeciesIds = new List<string>();
    public List<string> unlockedVegetationIds = new List<string>();
    public List<string> completedMilestoneIds = new List<string>();
    public ActiveResearchSaveData activeResearch;
}
