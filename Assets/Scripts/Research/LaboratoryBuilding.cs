using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class LaboratoryBuilding : MonoBehaviour
{
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private float noPlantRadius = 3f;
    [SerializeField] private float clickRadius = 1.2f;

    private static Sprite labBuildingSprite;

    private void Awake()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetLabBuildingSprite();
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 2;
    }

    private static Sprite GetLabBuildingSprite()
    {
        if (labBuildingSprite != null)
        {
            return labBuildingSprite;
        }

        labBuildingSprite = PixelArtUtility.CreateLabBuildingSprite(96);
        return labBuildingSprite;
    }

    public bool IsWithinExclusionZone(Vector3 worldPosition)
    {
        return Vector2.Distance(transform.position, worldPosition) < noPlantRadius;
    }

    public bool TryHandleTap(Vector3 worldPosition)
    {
        if (Vector2.Distance(transform.position, worldPosition) > clickRadius)
        {
            return false;
        }

        if (laboratoryUI != null)
        {
            laboratoryUI.ToggleLab();
        }

        return true;
    }
}
