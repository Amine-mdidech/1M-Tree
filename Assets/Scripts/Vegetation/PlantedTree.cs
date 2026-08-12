using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlantedTree : MonoBehaviour
{
    // Maintained registry of every live tree, so callers (tap hit-testing, save/load, emergence
    // ticks) don't have to pay for a scene-wide FindObjectsByType<PlantedTree> scan every time
    // they need "all planted trees" - a query that used to run on every tap and every save.
    public static readonly List<PlantedTree> ActiveTrees = new List<PlantedTree>();

    // Just above GroundManager's rock props (-99) and far below every other sprite in the game, so
    // the patch always reads as ground cover - under the tree/creatures, over the dirt and rocks.
    private const int GroundPatchSortingOrder = -98;
    private const int GroundPatchTextureSize = 64;
    // Comfortably bigger than even a mature tree's own canopy sprite footprint (~1.15 world units
    // across) - otherwise the patch's solid center sits entirely hidden behind the canopy and only
    // a near-invisible feathered fringe pokes out past it.
    private const float GroundPatchWorldDiameter = 2.6f;
    private static readonly Color YoungPatchColor = new Color(0.55f, 0.78f, 0.35f, 0.85f);
    // Deliberately no darker than the old half-mature midpoint - a fully dark, near-black green
    // read as too heavy/muddy against the bright gamified dirt; this stays readably "grass" even
    // at full maturity.
    private static readonly Color MaturePatchColor = new Color(0.35f, 0.58f, 0.26f, 0.88f);

    [SerializeField] private VegetationDefinition vegetation;
    [SerializeField] private float elapsedSecondsInStage;
    [SerializeField] private int currentStageIndex;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer groundPatchRenderer;
    private bool awakeStateReady;

    public VegetationDefinition Vegetation => vegetation;
    public int CurrentStageIndex => currentStageIndex;
    public string CurrentStageName => vegetation.growthStages[currentStageIndex].stageName;
    public float ElapsedSecondsInStage => elapsedSecondsInStage;
    public DateTime PlantedAtUtc { get; private set; }
    public bool IsMature => currentStageIndex >= vegetation.growthStages.Length - 1;

    public void Initialize(VegetationDefinition definition)
    {
        EnsureAwakeState();

        vegetation = definition;
        elapsedSecondsInStage = 0f;
        currentStageIndex = 0;
        PlantedAtUtc = DateTime.UtcNow;

        UpdateVisual();
        RefreshEnabledState();
    }

    public void RestoreState(VegetationDefinition definition, int stageIndex, float elapsedInStage, DateTime plantedAtUtc)
    {
        EnsureAwakeState();

        vegetation = definition;
        currentStageIndex = Mathf.Clamp(stageIndex, 0, definition.growthStages.Length - 1);
        elapsedSecondsInStage = elapsedInStage;
        PlantedAtUtc = plantedAtUtc;

        UpdateVisual();
        RefreshEnabledState();
    }

    public void AdvanceGrowth(float seconds)
    {
        if (vegetation == null || vegetation.growthStages == null || vegetation.growthStages.Length == 0)
        {
            return;
        }

        if (IsMature)
        {
            return;
        }

        elapsedSecondsInStage += seconds;

        while (!IsMature && elapsedSecondsInStage >= vegetation.growthStages[currentStageIndex].durationSeconds)
        {
            elapsedSecondsInStage -= vegetation.growthStages[currentStageIndex].durationSeconds;
            currentStageIndex++;
            UpdateVisual();
        }

        RefreshEnabledState();
    }

    public float GetRemainingGrowthSeconds()
    {
        if (IsMature)
        {
            return 0f;
        }

        float remaining = vegetation.growthStages[currentStageIndex].durationSeconds - elapsedSecondsInStage;
        for (int i = currentStageIndex + 1; i < vegetation.growthStages.Length; i++)
        {
            remaining += vegetation.growthStages[i].durationSeconds;
        }

        return Mathf.Max(0f, remaining);
    }

    public void CompleteGrowthInstantly()
    {
        float remaining = GetRemainingGrowthSeconds();
        if (remaining > 0f)
        {
            AdvanceGrowth(remaining + 0.01f);
        }
    }

    private void RefreshEnabledState()
    {
        // Mature trees never grow further, so stop paying per-frame Update overhead for them.
        enabled = !IsMature;
    }

    private void Awake()
    {
        EnsureAwakeState();
    }

    // Normal gameplay always reaches this via Awake before Initialize/RestoreState are ever
    // called (TreePlanter does AddComponent then Initialize in the same call, and Unity runs
    // Awake first). But AddComponent doesn't synchronously invoke Awake outside of a running
    // Player/PlayMode loop - editor-driven code and EditMode tests can call Initialize before
    // Awake has run at all, leaving spriteRenderer/groundPatchRenderer null and this tree missing
    // from ActiveTrees (so the spacing rule silently didn't see it). Calling this from both paths,
    // guarded by awakeStateReady, makes setup correct regardless of which one runs first.
    private void EnsureAwakeState()
    {
        if (awakeStateReady)
        {
            return;
        }

        awakeStateReady = true;
        spriteRenderer = GetComponent<SpriteRenderer>();
        BuildGroundPatch();

        // Registered in Awake/OnDestroy rather than OnEnable/OnDisable: RefreshEnabledState()
        // below deliberately sets `enabled = false` once a tree matures (to stop paying for
        // per-frame Update calls), and enabled-toggling also fires OnDisable - registering there
        // would silently drop every mature tree from the registry the moment it finished growing.
        ActiveTrees.Add(this);
    }

    private void BuildGroundPatch()
    {
        GameObject patchGo = new GameObject("GroundPatch");
        patchGo.transform.SetParent(transform, worldPositionStays: false);
        patchGo.transform.localPosition = Vector3.zero;
        patchGo.transform.localRotation = Quaternion.identity;

        groundPatchRenderer = patchGo.AddComponent<SpriteRenderer>();
        groundPatchRenderer.sortingOrder = GroundPatchSortingOrder;
    }

    private void OnDestroy()
    {
        ActiveTrees.Remove(this);
    }

    private void Update()
    {
        AdvanceGrowth(Time.deltaTime);
    }

    private void UpdateVisual()
    {
        int stageCount = vegetation.growthStages.Length;
        float t = stageCount <= 1 ? 1f : (float)currentStageIndex / (stageCount - 1);
        bool useHeroSprite = vegetation.heroSprite != null;

        float scale = useHeroSprite ? ComputeHeroSpriteScale(t) : Mathf.Lerp(1.5f, 3f, t);
        transform.localScale = new Vector3(scale, scale, 1f);
        ScalePunch.Punch(transform, scale);

        if (spriteRenderer != null)
        {
            if (useHeroSprite)
            {
                ApplyHeroSpriteVisual();
            }
            else
            {
                ApplyProceduralVisual(t);
            }
        }

        UpdateGroundPatch(t, scale);
    }

    // Hero art (see docs/art-design-document.md) has a fixed native pixel size, so localScale must
    // be solved for the desired world diameter directly instead of reusing the procedural path's
    // fixed 1.5-3 multiplier range - that range was tuned for the much smaller 64px procedural
    // sprites and would render a hero sprite enormous. Young stages get half the mature diameter,
    // the same young:mature ratio the procedural path's 1.5:3 scale range already used.
    private float ComputeHeroSpriteScale(float t)
    {
        float targetDiameter = Mathf.Lerp(vegetation.heroSpriteMatureWorldDiameter * 0.5f, vegetation.heroSpriteMatureWorldDiameter, t);
        float nativeWorldSize = vegetation.heroSprite.rect.width / vegetation.heroSprite.pixelsPerUnit;
        return nativeWorldSize > 0f ? targetDiameter / nativeWorldSize : 1f;
    }

    // No per-stage color tint here (unlike the procedural path below): a real tree's growth reads
    // through its overall size, not a hue shift, and a multiply-tint that looks fine on a flat
    // procedural fill tends to look like a washed-out color error on a richly shaded hero texture.
    private void ApplyHeroSpriteVisual()
    {
        spriteRenderer.sprite = vegetation.heroSprite;
        spriteRenderer.color = Color.white;
    }

    private void ApplyProceduralVisual(float t)
    {
        Color youngFoliage = Color.Lerp(vegetation.foliageColor, Color.white, 0.35f);
        Color foliageColor = Color.Lerp(youngFoliage, vegetation.foliageColor, t);
        int seed = Mathf.RoundToInt(transform.position.x * 97.13f) ^ Mathf.RoundToInt(transform.position.y * 131.71f);

        Sprite oldSprite = spriteRenderer.sprite;
        spriteRenderer.sprite = PixelArtUtility.CreateVegetationSprite(64, vegetation.plantSilhouette, foliageColor, vegetation.trunkColor, seed);
        spriteRenderer.color = Color.white;

        if (oldSprite != null)
        {
            // DestroyImmediate (not Destroy): these are detached runtime-generated textures,
            // never scene objects, and this path also has to run safely from EditMode tests.
            DestroyImmediate(oldSprite.texture);
            DestroyImmediate(oldSprite);
        }
    }

    // Patch color eases from bright young-growth green to dark mature green as the tree grows, the
    // same t used for the tree's own foliage color above. World size is kept constant regardless of
    // stage (a "small radius" per the design, not a growing canopy) by counter-scaling against the
    // parent's own growth-stage scale - the same trick CreatureVisual's world-space name popup uses
    // to stay a fixed size under a scaled-up/down parent.
    private void UpdateGroundPatch(float t, float parentScale)
    {
        if (groundPatchRenderer == null)
        {
            return;
        }

        float inverseScale = parentScale != 0f ? 1f / parentScale : 1f;
        groundPatchRenderer.transform.localScale = new Vector3(inverseScale, inverseScale, 1f);

        Color patchColor = Color.Lerp(YoungPatchColor, MaturePatchColor, t);
        float pixelsPerUnit = GroundPatchTextureSize / GetGroundPatchWorldDiameter();

        Sprite oldPatchSprite = groundPatchRenderer.sprite;
        groundPatchRenderer.sprite = PixelArtUtility.CreateGroundPatchSprite(GroundPatchTextureSize, patchColor, pixelsPerUnit);

        if (oldPatchSprite != null)
        {
            DestroyImmediate(oldPatchSprite.texture);
            DestroyImmediate(oldPatchSprite);
        }
    }

    // A hero tree's own mature footprint varies per species (it's tuned per-asset via
    // heroSpriteMatureWorldDiameter), so the fixed GroundPatchWorldDiameter constant - tuned
    // against the small procedural canopy (~1.15 units) - isn't guaranteed to stay comfortably
    // bigger than a hero canopy. Scale the patch off the hero diameter instead when one is set.
    private float GetGroundPatchWorldDiameter()
    {
        return vegetation.heroSprite != null
            ? vegetation.heroSpriteMatureWorldDiameter * 2f
            : GroundPatchWorldDiameter;
    }
}
