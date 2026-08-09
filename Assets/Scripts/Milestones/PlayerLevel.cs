using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    public static PlayerLevel Instance { get; private set; }

    public const int XpPerTreePlanted = 5;
    public const int XpPerUnlock = 20;
    public const int XpPerMilestone = 50;

    private const int BaseXpToNextLevel = 100;
    private const int XpIncreasePerLevel = 50;
    private const int GemsPerLevelUp = 10;

    [SerializeField] private MilestoneManager milestoneManager;

    private int level = 1;
    private int currentXp;

    public int Level => level;
    public int CurrentXp => currentXp;
    public int XpToNextLevel => BaseXpToNextLevel + (level - 1) * XpIncreasePerLevel;

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

    public void AddXp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentXp += amount;

        while (currentXp >= XpToNextLevel)
        {
            currentXp -= XpToNextLevel;
            level++;
            milestoneManager?.AddGems(GemsPerLevelUp);
            AudioManager.Instance?.PlayMilestone();
        }
    }

    public void LoadState(int savedLevel, int savedXp)
    {
        level = Mathf.Max(1, savedLevel);
        currentXp = Mathf.Max(0, savedXp);
    }
}
