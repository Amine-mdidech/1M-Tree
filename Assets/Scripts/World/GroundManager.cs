using UnityEngine;

// A single large tiled sprite covering the whole map with a flat, near-uniform gamified dirt
// color (see docs/art-design-document.md), plus individually-scattered rock decoration props on
// top. Replaces what used to be just the camera's flat background clear color. Deliberately a
// SpriteRenderer (SpriteDrawMode.Tiled), not a MeshRenderer+Quad: a plain mesh went through a
// different render path than every other object in this game and came out visibly blurred even
// with mipmaps/compression/filtering all ruled out - going through the exact same SpriteRenderer
// path as trees/animals/the Laboratory renders it just as crisply as everything else, no separate
// render-path debugging needed.
//
// The base is deliberately near-flat rather than a single richly-detailed painted texture: an
// earlier attempt at one seamless texture carrying bold cartoon rock shapes always repeated as an
// obvious grid once tiled - unlike photographic noise, the eye locks onto a repeated distinct
// outlined shape immediately. Individually-placed rock props (the same pattern already used for
// trees/creatures) scatter that detail without any single tile repeating identically.
public class GroundManager : MonoBehaviour
{
    private const int GroundSortingOrder = -100;
    private const int RockPropSortingOrder = -99;

    [SerializeField] private Sprite groundSprite;
    [SerializeField] private Sprite[] rockPropSprites;
    [SerializeField] private float mapHalfWidth = 30f;
    [SerializeField] private float mapHalfHeight = 30f;
    [SerializeField] private int rockPropCount = 90;
    [SerializeField] private float rockPropMinScale = 0.5f;
    [SerializeField] private float rockPropMaxScale = 1f;
    [SerializeField] private LaboratoryBuilding laboratoryBuilding;

    private void Awake()
    {
        BuildGroundPlane();
        ScatterRockProps();
    }

    private void BuildGroundPlane()
    {
        GameObject groundGo = new GameObject("GroundPlane");
        groundGo.transform.SetParent(transform, worldPositionStays: false);
        groundGo.transform.localPosition = Vector3.zero;

        SpriteRenderer spriteRenderer = groundGo.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = groundSprite;
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        spriteRenderer.size = new Vector2(mapHalfWidth * 2f, mapHalfHeight * 2f);
        // Far below every sprite in the game (trees=2, creatures=1, the Laboratory=2) so the
        // ground plane always renders as the floor, never on top of anything.
        spriteRenderer.sortingOrder = GroundSortingOrder;
    }

    private void ScatterRockProps()
    {
        if (rockPropSprites == null || rockPropSprites.Length == 0)
        {
            return;
        }

        GameObject propsRoot = new GameObject("RockProps");
        propsRoot.transform.SetParent(transform, worldPositionStays: false);

        for (int i = 0; i < rockPropCount; i++)
        {
            Vector3 position = PickRockPropPosition();
            if (position == Vector3.negativeInfinity)
            {
                continue;
            }

            Sprite sprite = rockPropSprites[Random.Range(0, rockPropSprites.Length)];
            GameObject propGo = new GameObject("RockProp");
            propGo.transform.SetParent(propsRoot.transform, worldPositionStays: false);
            propGo.transform.localPosition = position;
            propGo.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float scale = Random.Range(rockPropMinScale, rockPropMaxScale);
            propGo.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer spriteRenderer = propGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = RockPropSortingOrder;
        }
    }

    // Rejects candidates in the river or the Laboratory's exclusion zone, retrying a bounded
    // number of times before giving up on that one prop rather than looping forever.
    private Vector3 PickRockPropPosition()
    {
        const int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(-mapHalfWidth, mapHalfWidth),
                Random.Range(-mapHalfHeight, mapHalfHeight),
                0f);

            if (RiverManager.Instance != null && RiverManager.Instance.IsBlocked(candidate))
            {
                continue;
            }

            if (laboratoryBuilding != null && laboratoryBuilding.IsWithinExclusionZone(candidate))
            {
                continue;
            }

            return candidate;
        }

        return Vector3.negativeInfinity;
    }
}
