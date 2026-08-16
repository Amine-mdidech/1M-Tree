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

    // Optional fal.ai-generated hero art (see docs/art-design-document.md) that overrides the
    // procedural silhouette above when set. Kept per-definition rather than a global switch so
    // individual species can be upgraded one at a time.
    public Sprite heroSprite;
    public float heroSpriteMatureWorldDiameter = 2.4f;

    // Flat Funds cost to plant one instance of this vegetation (see EconomyManager).
    public int plantCost;
}
