using UnityEngine;

public class LandManager : MonoBehaviour
{
    [System.Serializable]
    public struct LandTier
    {
        public int requiredLevel;
        public int gemCost;
        public float halfExtent;
    }

    public static LandManager Instance { get; private set; }

    [SerializeField] private MilestoneManager milestoneManager;
    [SerializeField]
    private LandTier[] tiers =
    {
        new LandTier { requiredLevel = 1, gemCost = 0, halfExtent = 10f },
        new LandTier { requiredLevel = 3, gemCost = 50, halfExtent = 16f },
        new LandTier { requiredLevel = 6, gemCost = 150, halfExtent = 22f },
        new LandTier { requiredLevel = 10, gemCost = 400, halfExtent = 30f },
    };

    private int currentTierIndex;

    public int CurrentTierIndex => currentTierIndex;
    public int TierCount => tiers.Length;
    public float CurrentLandHalfExtent => tiers[currentTierIndex].halfExtent;
    public bool HasNextTier => currentTierIndex < tiers.Length - 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsWithinOwnedLand(Vector3 worldPosition)
    {
        float halfExtent = CurrentLandHalfExtent;
        return Mathf.Abs(worldPosition.x) <= halfExtent && Mathf.Abs(worldPosition.y) <= halfExtent;
    }

    public LandTier GetNextTier()
    {
        return tiers[currentTierIndex + 1];
    }

    public bool CanAffordNextTier()
    {
        if (!HasNextTier || milestoneManager == null || PlayerLevel.Instance == null)
        {
            return false;
        }

        LandTier next = GetNextTier();
        return PlayerLevel.Instance.Level >= next.requiredLevel && milestoneManager.Gems >= next.gemCost;
    }

    public bool TryUnlockNextTier()
    {
        if (!CanAffordNextTier())
        {
            return false;
        }

        LandTier next = GetNextTier();
        if (!milestoneManager.TrySpendGems(next.gemCost))
        {
            return false;
        }

        currentTierIndex++;
        AudioManager.Instance?.PlayUnlock();
        return true;
    }

    public void LoadState(int savedTierIndex)
    {
        currentTierIndex = Mathf.Clamp(savedTierIndex, 0, tiers.Length - 1);
    }
}
