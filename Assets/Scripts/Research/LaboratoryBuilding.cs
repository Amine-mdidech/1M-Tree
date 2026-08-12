using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class LaboratoryBuilding : MonoBehaviour
{
    [SerializeField] private LaboratoryUI laboratoryUI;
    // fal.ai-generated complex eco/scientific building (see Assets/Textures/Laboratory) — falls
    // back to the small procedural icon if unassigned so the scene never renders empty.
    [SerializeField] private Sprite buildingSprite;
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

    // Blocks planting only on the building's own footprint, not a wider buffer zone around it -
    // reuses the same clickRadius as the tap hit-test rather than a separate, larger radius.
    public bool IsWithinExclusionZone(Vector3 worldPosition)
    {
        return IsPointOnBuilding(worldPosition);
    }

    // Pure query, no side effects - lets callers check "would a tap here hit the building" without
    // triggering TryHandleTap's UI toggle.
    public bool IsPointOnBuilding(Vector3 worldPosition)
    {
        return Vector2.Distance(transform.position, worldPosition) <= clickRadius;
    }

    public bool TryHandleTap(Vector3 worldPosition)
    {
        if (!IsPointOnBuilding(worldPosition))
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
