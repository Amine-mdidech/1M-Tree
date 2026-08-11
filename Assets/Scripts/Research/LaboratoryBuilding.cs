using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class LaboratoryBuilding : MonoBehaviour
{
    [SerializeField] private LaboratoryUI laboratoryUI;
    // fal.ai-generated complex eco/scientific building (see Assets/Textures/Laboratory) — falls
    // back to the small procedural icon if unassigned so the scene never renders empty.
    [SerializeField] private Sprite buildingSprite;
    [SerializeField] private float noPlantRadius = 3.2f;
    [SerializeField] private float clickRadius = 2.6f;

    private static Sprite proceduralFallbackSprite;

    private void Awake()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = buildingSprite != null ? buildingSprite : GetProceduralFallbackSprite();
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 2;
    }

    private static Sprite GetProceduralFallbackSprite()
    {
        if (proceduralFallbackSprite != null)
        {
            return proceduralFallbackSprite;
        }

        proceduralFallbackSprite = PixelArtUtility.CreateLabBuildingSprite(96);
        return proceduralFallbackSprite;
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
