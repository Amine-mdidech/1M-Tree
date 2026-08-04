using UnityEngine;

[CreateAssetMenu(fileName = "ResearchProjectDefinition", menuName = "Grove/Research Project Definition")]
public class ResearchProjectDefinition : ScriptableObject
{
    public string id;
    public string unlocksSpeciesId;
    public string unlocksVegetationId;
    public string[] prerequisiteProjectIds = new string[0];
    public int baseDurationSeconds;
    public int gemSkipCostPerRemainingMinute;
}
