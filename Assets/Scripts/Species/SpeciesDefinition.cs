using UnityEngine;

[CreateAssetMenu(fileName = "SpeciesDefinition", menuName = "Grove/Species Definition")]
public class SpeciesDefinition : ScriptableObject
{
    public enum SpeciesCategory
    {
        Insect,
        Herbivore,
        Predator
    }

    public string id;
    public string displayName;
    public SpeciesCategory category;
    public bool unlockedByDefault;
    public string[] requiredVegetationIds = new string[0];
    public float baseEmergenceWeight;
    public int treesPerIndividualCap;

    // Flat Funds cost to release one individual of this species (see EconomyManager).
    public int releaseCost;

    [Header("Predator-only fields")]
    public string[] preySpeciesIds = new string[0];
    public int preyThreshold;
    public int preyToPredatorRatio;

    public int researchDurationSeconds;
    public string documentaryTitle;
    [TextArea] public string documentaryText;
}
