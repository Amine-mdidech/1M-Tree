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
    // Field initializer (not just a default) so an old save file missing this key — from before
    // this field existed — deserializes to a reasonable starting balance instead of 0. References
    // EconomyManager.StartingFunds directly rather than duplicating the literal.
    public int funds = EconomyManager.StartingFunds;
    public int milestoneFundsBonus;
    public string lastFundsGrantTimestamp;
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
