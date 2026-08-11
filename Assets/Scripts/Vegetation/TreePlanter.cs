using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TreePlanter : MonoBehaviour
{
    [SerializeField] private List<VegetationDefinition> plantableVegetation = new List<VegetationDefinition>();
    [SerializeField] private List<VegetationDefinition> unlockedVegetation = new List<VegetationDefinition>();
    [SerializeField] private int currentVegetationIndex;
    [SerializeField] private Sprite treeSprite;
    [SerializeField] private DailyGrant dailyGrant;
    [SerializeField] private LaboratoryBuilding laboratoryBuilding;
    [SerializeField] private int debugBatchPlantCount = 20;

    private static Sprite fallbackSprite;

    public VegetationDefinition CurrentVegetation =>
        unlockedVegetation.Count == 0 ? null : unlockedVegetation[currentVegetationIndex];

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            CycleVegetation();
        }
    }

    public void CycleVegetation()
    {
        if (unlockedVegetation.Count == 0)
        {
            return;
        }

        currentVegetationIndex = (currentVegetationIndex + 1) % unlockedVegetation.Count;
        Debug.Log($"Selected vegetation: {CurrentVegetation.displayName}");
    }

    public bool IsVegetationUnlocked(string vegetationId)
    {
        foreach (VegetationDefinition vegetation in unlockedVegetation)
        {
            if (vegetation != null && vegetation.id == vegetationId)
            {
                return true;
            }
        }

        return false;
    }

    public void AddUnlockedVegetation(VegetationDefinition vegetation)
    {
        if (vegetation == null || IsVegetationUnlocked(vegetation.id))
        {
            return;
        }

        unlockedVegetation.Add(vegetation);
    }

    public List<string> GetUnlockedVegetationIds()
    {
        List<string> ids = new List<string>();

        foreach (VegetationDefinition vegetation in unlockedVegetation)
        {
            if (vegetation != null)
            {
                ids.Add(vegetation.id);
            }
        }

        return ids;
    }

    public void PlantTree(Vector3 position)
    {
        VegetationDefinition vegetation = CurrentVegetation;
        if (vegetation == null)
        {
            Debug.LogWarning("TreePlanter has no plantable vegetation assigned.");
            return;
        }

        if (dailyGrant == null)
        {
            Debug.LogWarning("TreePlanter has no DailyGrant assigned.");
            return;
        }

        if (LandManager.Instance != null && !LandManager.Instance.IsWithinOwnedLand(position))
        {
            Debug.Log("Can't plant a tree here — outside your owned land.");
            AudioManager.Instance?.PlayError();
            HudUI.Instance?.ShowLandLockedPopup();
            return;
        }

        if (laboratoryBuilding != null && laboratoryBuilding.IsWithinExclusionZone(position))
        {
            Debug.Log("Can't plant a tree here — too close to the Laboratory.");
            AudioManager.Instance?.PlayError();
            return;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsBlocked(position))
        {
            Debug.Log("Can't plant a tree here — that's the river.");
            AudioManager.Instance?.PlayError();
            return;
        }

        if (!dailyGrant.TryConsumeTreePlant())
        {
            Debug.Log($"Daily tree cap reached ({dailyGrant.TreesPlantedToday}/{dailyGrant.DailyTreeCap}). Come back after the next grant reset.");
            AudioManager.Instance?.PlayError();
            return;
        }

        PlantedTree plantedTree = SpawnRestoredTree(vegetation, position);
        plantedTree.Initialize(vegetation);

        EcosystemState.RegisterTreePlanted(vegetation.id);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerTreePlanted);
        VfxUtility.SpawnBurst(position, vegetation.foliageColor);
        Handheld.Vibrate();
    }

    public PlantedTree SpawnRestoredTree(VegetationDefinition vegetation, Vector3 position)
    {
        GameObject go = new GameObject($"Tree_{vegetation.id}");
        go.transform.position = position;

        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = treeSprite != null ? treeSprite : GetFallbackSprite();
        spriteRenderer.sortingOrder = 2;

        return go.AddComponent<PlantedTree>();
    }

    public VegetationDefinition FindVegetationById(string id)
    {
        foreach (VegetationDefinition vegetation in plantableVegetation)
        {
            if (vegetation != null && vegetation.id == id)
            {
                return vegetation;
            }
        }

        return null;
    }

    [ContextMenu("Debug: Plant N Trees (auto-resets daily cap)")]
    public void DebugPlantBatch()
    {
        for (int i = 0; i < debugBatchPlantCount; i++)
        {
            if (dailyGrant != null && !dailyGrant.CanPlantTree)
            {
                dailyGrant.DebugForceReset();
            }

            Vector3 position = new Vector3(i * 0.5f, 0f, 0f);
            PlantTree(position);
        }
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return fallbackSprite;
    }
}
