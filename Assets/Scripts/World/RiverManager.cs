using System.Collections.Generic;
using UnityEngine;

// A winding river built from four stacked LineRenderer ribbons along the same Catmull-Rom spline
// (widest to narrowest, back to front): sand bank, a bright toon-style shoreline rim, the water
// base, and an animated sparkle/shimmer overlay scrolling at its own speed. This layered-ribbon
// approach (rather than a single flat scrolling texture) is what gives mobile-game water its
// readable "gamified" look — a bold graphic outline plus a moving highlight layer distinct from
// the base water color, not just a photographic surface. All four use one of URP's built-in 2D
// sprite shaders (see FallbackShaderNames) rather than a custom one — smooth flow animation comes
// from nudging each material's texture offset in Update() every frame instead of a scrolling-UV
// shader. Also the single source of truth for "is this point river" — TreePlanter and
// CreatureVisual both query IsBlocked/DistanceToRiver instead of duplicating the geometry check.
[RequireComponent(typeof(LineRenderer))]
public class RiverManager : MonoBehaviour
{
    public static RiverManager Instance { get; private set; }

    [SerializeField] private Texture2D waterTexture;
    [SerializeField] private Texture2D sandTexture;
    [SerializeField] private Texture2D shimmerTexture;
    [SerializeField] private float riverWidth = 2f;
    [SerializeField] private float sandBankWidth = 1f;
    // Thin bright rim between sand and water — the bold cartoon outline mobile-game water reads
    // its shape from at a glance, rather than a soft photographic edge blending into the bank.
    [SerializeField] private float shoreHighlightWidth = 0.35f;
    [SerializeField] private Color shoreHighlightColor = new Color(0.85f, 0.98f, 1f, 1f);
    [SerializeField] private int samplesPerSegment = 12;

    // A single winding vertical river running the full height of the map, staying outside the
    // tier-0 boundary (half-extent 10) along its right edge rather than looping around the whole
    // plot — matched directly against a reference shape sketched over a map screenshot. Also stays
    // clear of the Laboratory (world position ~0,3, 3-unit no-plant radius) by a wide margin.
    [SerializeField]
    private Vector2[] controlPoints =
    {
        new Vector2(13f, -30f),
        new Vector2(16f, -20f),
        new Vector2(10f, -10f),
        new Vector2(16f, 0f),
        new Vector2(10f, 10f),
        new Vector2(16f, 20f),
        new Vector2(13f, 30f),
    };

    // Tried first, in order — the first one found is guaranteed to already work correctly under
    // URP's 2D Renderer, since it's the exact shader family every SpriteRenderer in this game
    // (trees, animals, the Laboratory) already renders successfully with. A custom hand-written
    // shader was tried first and, despite the mesh/material/shader all individually validating as
    // correct, never actually appeared on screen — using known-working built-in infrastructure
    // instead of a from-scratch shader removes that whole class of URP/2D-Renderer compatibility
    // guesswork.
    private static readonly string[] FallbackShaderNames =
    {
        "Universal Render Pipeline/2D/Sprite-Unlit-Default",
        "Universal Render Pipeline/2D/Sprite-Lit-Default",
        "Sprites/Default",
    };

    // U (X) is the along-ribbon axis in LineRenderer's tiled UV space — regardless of how the path
    // bends in world space, scrolling U alone reads as flowing along the river's length. The water
    // and shimmer layers scroll at different speeds/directions from each other deliberately: two
    // layers moving in lockstep would just look like one layer, but different speeds create a
    // parallax glint as the sparkle overlay slides across the base water.
    private const float WaterScrollSpeedX = 0.18f;
    private const float WaterScrollSpeedY = 0.015f;
    private const float ShimmerScrollSpeedX = 0.4f;
    private const float ShimmerScrollSpeedY = -0.05f;

    private Vector2[] pathSamples;
    private float halfWidth;
    private Material waterMaterial;
    private Material shimmerMaterial;
    private Vector2 waterScrollOffset;
    private Vector2 shimmerScrollOffset;

    public IReadOnlyList<Vector2> PathSamples => pathSamples;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        halfWidth = riverWidth * 0.5f;

        BuildPathSamples();

        // Back to front: sand (widest) -> shoreline rim -> water -> shimmer (narrowest), each
        // strictly narrower than the last so every layer beneath still shows its own visible band.
        BuildSandBank();
        BuildShoreHighlight();
        // Fewer, bigger repeats than a plain 1:1 tile: the water texture is now a handful of BOLD
        // diagonal bands (not fine photographic ripple detail — that gets minified into a flat
        // color on a ribbon this thin at normal camera distance, no matter how much contrast it
        // has). Big shapes need more world-length per tile to read as broad sweeps rather than a
        // busy repeating stripe pattern.
        waterMaterial = BuildRibbon(GetComponent<LineRenderer>(), riverWidth, waterTexture, sortingOrder: -10, tileFrequencyMultiplier: 0.5f);
        BuildShimmerOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (waterMaterial != null)
        {
            // Smooth flow animation without a custom shader: nudge the tiled texture's offset
            // every frame. Works with any standard shader that respects material.mainTextureOffset
            // (which Sprite-Unlit-Default and Sprites/Default both do), same visual effect as a
            // scrolling-UV shader, just driven from C# instead of HLSL.
            waterScrollOffset += new Vector2(WaterScrollSpeedX, WaterScrollSpeedY) * Time.deltaTime;
            waterMaterial.mainTextureOffset = waterScrollOffset;
        }

        if (shimmerMaterial != null)
        {
            shimmerScrollOffset += new Vector2(ShimmerScrollSpeedX, ShimmerScrollSpeedY) * Time.deltaTime;
            shimmerMaterial.mainTextureOffset = shimmerScrollOffset;
        }
    }

    public bool IsBlocked(Vector3 worldPosition)
    {
        return DistanceToRiver(worldPosition) <= halfWidth;
    }

    public float DistanceToRiver(Vector3 worldPosition)
    {
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        float closest = float.MaxValue;

        for (int i = 0; i < pathSamples.Length - 1; i++)
        {
            float distance = DistancePointToSegment(point, pathSamples[i], pathSamples[i + 1]);
            if (distance < closest)
            {
                closest = distance;
            }
        }

        return closest;
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 0.0001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(point, projection);
    }

    private void BuildPathSamples()
    {
        List<Vector2> samples = new List<Vector2>();

        for (int i = 0; i < controlPoints.Length - 1; i++)
        {
            Vector2 p0 = controlPoints[Mathf.Max(0, i - 1)];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[i + 1];
            Vector2 p3 = controlPoints[Mathf.Min(controlPoints.Length - 1, i + 2)];

            // Last segment also emits its final endpoint so the ribbon reaches the last control point.
            int steps = i == controlPoints.Length - 2 ? samplesPerSegment + 1 : samplesPerSegment;
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)samplesPerSegment;
                samples.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        pathSamples = samples.ToArray();
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (p2 - p0) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (3f * p1 - 3f * p2 + p3 - p0) * t3);
    }

    private void BuildSandBank()
    {
        GameObject bankObject = new GameObject("RiverBank");
        bankObject.transform.SetParent(transform, false);
        LineRenderer bankRenderer = bankObject.AddComponent<LineRenderer>();
        BuildRibbon(bankRenderer, riverWidth + sandBankWidth, sandTexture, sortingOrder: -12);
    }

    private void BuildShoreHighlight()
    {
        GameObject shoreObject = new GameObject("RiverShoreHighlight");
        shoreObject.transform.SetParent(transform, false);
        LineRenderer shoreRenderer = shoreObject.AddComponent<LineRenderer>();
        BuildRibbon(shoreRenderer, riverWidth + shoreHighlightWidth, null, sortingOrder: -11, tint: shoreHighlightColor);
    }

    private void BuildShimmerOverlay()
    {
        GameObject shimmerObject = new GameObject("RiverShimmer");
        shimmerObject.transform.SetParent(transform, false);
        LineRenderer shimmerRenderer = shimmerObject.AddComponent<LineRenderer>();
        // Slightly narrower than the water so the shimmer never pokes out past the water's own
        // edge as it scrolls sideways. Same bold-shapes-over-fine-detail reasoning as the water
        // texture: fewer, bigger, fully-opaque glints survive minification at this ribbon width.
        shimmerMaterial = BuildRibbon(shimmerRenderer, riverWidth * 0.92f, shimmerTexture, sortingOrder: -9, tileFrequencyMultiplier: 0.6f);
    }

    // Shared by every ribbon layer — same spline, same texturing approach, different width/
    // texture/sortingOrder/tint. Returns the created material so the caller can keep a reference
    // for animated layers (water, shimmer); the sand and shore-highlight layers discard it.
    private Material BuildRibbon(LineRenderer lineRenderer, float width, Texture2D texture, int sortingOrder, Color? tint = null, float tileFrequencyMultiplier = 1f)
    {
        Vector3[] positions = new Vector3[pathSamples.Length];
        float totalLength = 0f;
        for (int i = 0; i < pathSamples.Length; i++)
        {
            positions[i] = new Vector3(pathSamples[i].x, pathSamples[i].y, 0f);
            if (i > 0)
            {
                totalLength += Vector2.Distance(pathSamples[i], pathSamples[i - 1]);
            }
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);
        lineRenderer.widthMultiplier = width;
        // A LineRenderer added via RequireComponent's auto-add (rather than set up by hand in the
        // Inspector) can come with an uninitialized widthCurve — Evaluate() returns 0 everywhere,
        // silently zeroing the visible width regardless of widthMultiplier. Force a flat 1.0 curve
        // explicitly rather than relying on whatever default the component happened to get.
        lineRenderer.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        // View-alignment (the default) billboards each segment to face the camera, which is
        // ambiguous/inconsistent for a flat top-down orthographic view — force it flat in the XY
        // plane instead so it behaves identically regardless of camera framing.
        lineRenderer.alignment = LineAlignment.TransformZ;
        // numCornerVertices > 0 inserts extra rounded-joint geometry at EVERY vertex along the
        // path — with dozens of densely-packed Catmull-Rom samples making up this curve, that
        // means dozens of extra UV creases, which showed up as regularly-spaced diagonal streaks
        // running across the tiled ribbon. The path is already smooth from dense sampling, so
        // rounded joins add nothing visually here; 0 uses plain miter joints and removes the
        // per-vertex UV discontinuity entirely.
        lineRenderer.numCornerVertices = 0;
        lineRenderer.numCapVertices = 4;
        lineRenderer.textureMode = LineTextureMode.Tile;

        // LineRenderer always maps the texture's V axis across the ribbon's FULL current width,
        // regardless of textureScale.y — so each tile must be exactly `width` wide or the pattern
        // gets stretched non-uniformly (a tile sized for one width crammed into a narrower/wider
        // ribbon reads as visibly elongated or squashed, "wrong"-looking ripples/grain).
        float tileCount = totalLength / width * tileFrequencyMultiplier;
        lineRenderer.textureScale = new Vector2(tileCount, 1f);

        Material material = BuildMaterial(texture, tint);
        lineRenderer.material = material;
        lineRenderer.sortingOrder = sortingOrder;
        return material;
    }

    private Material BuildMaterial(Texture2D texture, Color? tint)
    {
        Shader shader = null;
        foreach (string shaderName in FallbackShaderNames)
        {
            shader = Shader.Find(shaderName);
            if (shader != null)
            {
                break;
            }
        }

        if (shader == null)
        {
            Debug.LogWarning("[RiverManager] None of the fallback sprite shaders were found.");
            return null;
        }

        Material material = new Material(shader);
        if (texture != null)
        {
            material.mainTexture = texture;
        }

        if (tint.HasValue)
        {
            material.color = tint.Value;
        }

        return material;
    }
}
