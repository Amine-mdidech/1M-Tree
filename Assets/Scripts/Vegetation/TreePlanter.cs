using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TreePlanter : MonoBehaviour
{
    // Not shown anywhere as a number (there's no UI indicator for it, by design). Chosen to
    // comfortably clear the biggest tree's own footprint (the oak hero sprite's mature diameter is
    // 2.4, radius 1.2) so two full-grown trees never visibly overlap trunks/canopies once both
    // mature.
    private const float MinTreeSpacing = 1.3f;

    [SerializeField] private List<VegetationDefinition> plantableVegetation = new List<VegetationDefinition>();
    [SerializeField] private List<VegetationDefinition> unlockedVegetation = new List<VegetationDefinition>();
    [SerializeField] private int currentVegetationIndex;
    [SerializeField] private Sprite treeSprite;
    [SerializeField] private EconomyManager economyManager;
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

        if (economyManager == null)
        {
            Debug.LogWarning("TreePlanter has no EconomyManager assigned.");
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

        if (RiverManager.Instance != null && RiverManager.Instance.IsWithinSandBank(position))
        {
            Debug.Log("Can't plant a tree here — that's the sand bank.");
            AudioManager.Instance?.PlayError();
            return;
        }

        if (IsTooCloseToExistingTree(position, excludeTree: null))
        {
            Debug.Log("Can't plant a tree here — too close to another tree.");
            AudioManager.Instance?.PlayError();
            return;
        }

        if (!economyManager.TrySpend(vegetation.plantCost))
        {
            Debug.Log($"Not enough funds to plant {vegetation.displayName} (need {vegetation.plantCost}, have {economyManager.Funds}).");
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

    // excludeTree lets a move-in-progress check spacing against every OTHER tree without being
    // blocked by its own current position.
    private bool IsTooCloseToExistingTree(Vector3 position, PlantedTree excludeTree)
    {
        foreach (PlantedTree tree in PlantedTree.ActiveTrees)
        {
            // Guards against a destroyed-but-not-yet-unregistered entry (OnDestroy isn't always
            // synchronous with DestroyImmediate outside a running Player/PlayMode loop, e.g. in
            // EditMode tests) - Unity's overridden null check catches this safely before touching
            // transform on an object that no longer exists.
            if (tree == null || tree == excludeTree)
            {
                continue;
            }

            if (Vector2.Distance(tree.transform.position, position) < MinTreeSpacing)
            {
                return true;
            }
        }

        return false;
    }

    // Pure query mirroring every rejection check PlantTree performs (minus its side effects -
    // logging, sound, the land-locked popup) - lets callers check plantability without duplicating
    // the rule set and risking the two drifting apart.
    public bool CanPlantAt(Vector3 position)
    {
        if (LandManager.Instance != null && !LandManager.Instance.IsWithinOwnedLand(position))
        {
            return false;
        }

        if (laboratoryBuilding != null && laboratoryBuilding.IsWithinExclusionZone(position))
        {
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsBlocked(position))
        {
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsWithinSandBank(position))
        {
            return false;
        }

        if (IsTooCloseToExistingTree(position, excludeTree: null))
        {
            return false;
        }

        if (economyManager != null)
        {
            VegetationDefinition vegetation = CurrentVegetation;
            int cost = vegetation != null ? vegetation.plantCost : 0;
            if (!economyManager.CanAfford(cost))
            {
                return false;
            }
        }

        return true;
    }

    // Shares every placement rule CanPlantAt uses except the daily cap (moving an already-planted
    // tree doesn't spend a new plant) - and excludes the tree itself from the spacing check so it
    // isn't blocked by its own current position.
    public bool CanMoveTreeTo(PlantedTree tree, Vector3 position)
    {
        if (tree == null)
        {
            return false;
        }

        if (LandManager.Instance != null && !LandManager.Instance.IsWithinOwnedLand(position))
        {
            return false;
        }

        if (laboratoryBuilding != null && laboratoryBuilding.IsWithinExclusionZone(position))
        {
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsBlocked(position))
        {
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsWithinSandBank(position))
        {
            return false;
        }

        if (IsTooCloseToExistingTree(position, tree))
        {
            return false;
        }

        return true;
    }

    // Free and uncapped - relocating a tree you already planted is layout tidying, not new growth,
    // so it deliberately doesn't touch economyManager or EcosystemState.RegisterTreePlanted.
    public bool MoveTree(PlantedTree tree, Vector3 position)
    {
        if (!CanMoveTreeTo(tree, position))
        {
            return false;
        }

        tree.transform.position = position;
        AudioManager.Instance?.PlayPlant();
        VfxUtility.SpawnBurst(position, tree.Vegetation.foliageColor);
        Handheld.Vibrate();
        return true;
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

    [ContextMenu("Debug: Plant N Trees (auto-grants funds)")]
    public void DebugPlantBatch()
    {
        for (int i = 0; i < debugBatchPlantCount; i++)
        {
            VegetationDefinition vegetation = CurrentVegetation;
            int cost = vegetation != null ? vegetation.plantCost : 0;
            if (economyManager != null && !economyManager.CanAfford(cost))
            {
                economyManager.DebugForceGrant();
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
