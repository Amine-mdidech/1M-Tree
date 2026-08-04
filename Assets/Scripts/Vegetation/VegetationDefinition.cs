using UnityEngine;

[CreateAssetMenu(fileName = "VegetationDefinition", menuName = "Grove/Vegetation Definition")]
public class VegetationDefinition : ScriptableObject
{
    [System.Serializable]
    public struct GrowthStage
    {
        public string stageName;
        public int durationSeconds;
    }

    // Which procedural silhouette PixelArtUtility.CreateVegetationSprite draws for this plant.
    public enum PlantSilhouette
    {
        BroadleafTree,
        Conifer,
        WeepingTree,
        Shrub,
        StalkCluster,
        Fern,
        Flower,
        RootedTree
    }

    public string id;
    public string displayName;
    public string biomeId;
    public bool unlockedByDefault;
    public GrowthStage[] growthStages;

    public PlantSilhouette plantSilhouette = PlantSilhouette.BroadleafTree;
    public Color foliageColor = new Color(0.3f, 0.6f, 0.3f);
    public Color trunkColor = new Color(0.4f, 0.28f, 0.16f);
}
