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

    [SerializeField] private VegetationDefinition vegetation;
    [SerializeField] private float elapsedSecondsInStage;
    [SerializeField] private int currentStageIndex;

    private SpriteRenderer spriteRenderer;

    public VegetationDefinition Vegetation => vegetation;
    public int CurrentStageIndex => currentStageIndex;
    public string CurrentStageName => vegetation.growthStages[currentStageIndex].stageName;
    public float ElapsedSecondsInStage => elapsedSecondsInStage;
    public DateTime PlantedAtUtc { get; private set; }
    public bool IsMature => currentStageIndex >= vegetation.growthStages.Length - 1;

    public void Initialize(VegetationDefinition definition)
    {
        vegetation = definition;
        elapsedSecondsInStage = 0f;
        currentStageIndex = 0;
        PlantedAtUtc = DateTime.UtcNow;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        UpdateVisual();
        RefreshEnabledState();
    }

    public void RestoreState(VegetationDefinition definition, int stageIndex, float elapsedInStage, DateTime plantedAtUtc)
    {
        vegetation = definition;
        currentStageIndex = Mathf.Clamp(stageIndex, 0, definition.growthStages.Length - 1);
        elapsedSecondsInStage = elapsedInStage;
        PlantedAtUtc = plantedAtUtc;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

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
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Registered in Awake/OnDestroy rather than OnEnable/OnDisable: RefreshEnabledState()
        // below deliberately sets `enabled = false` once a tree matures (to stop paying for
        // per-frame Update calls), and enabled-toggling also fires OnDisable - registering there
        // would silently drop every mature tree from the registry the moment it finished growing.
        ActiveTrees.Add(this);
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

        float scale = Mathf.Lerp(1f, 2f, t);
        transform.localScale = new Vector3(scale, scale, 1f);
        ScalePunch.Punch(transform, scale);

        if (spriteRenderer != null)
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
    }
}
