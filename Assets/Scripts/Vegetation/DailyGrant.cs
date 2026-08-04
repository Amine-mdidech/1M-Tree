using System;
using UnityEngine;

public class DailyGrant : MonoBehaviour
{
    private const int BaseDailyTreeCap = 30;
    private const float ResetCheckIntervalSeconds = 5f;

    [SerializeField] private int treesPlantedToday;
    [SerializeField] private int dailyTreeCap = BaseDailyTreeCap;
    [SerializeField] private int milestoneCapBonus;
    [SerializeField] private string lastGrantResetTimestamp;

    private DateTime lastResetDate;
    private float resetCheckTimer;

    public int TreesPlantedToday => treesPlantedToday;
    public int DailyTreeCap => dailyTreeCap;
    public string LastGrantResetTimestamp => lastGrantResetTimestamp;
    public bool CanPlantTree => treesPlantedToday < dailyTreeCap;

    private void Awake()
    {
        milestoneCapBonus = 0;
        dailyTreeCap = BaseDailyTreeCap;
        lastResetDate = DateTime.Now.Date;
        lastGrantResetTimestamp = lastResetDate.ToString("o");
    }

    public void AddDailyCapBonus(int amount)
    {
        milestoneCapBonus += amount;
        dailyTreeCap = BaseDailyTreeCap + milestoneCapBonus;
        Debug.Log($"Daily tree cap increased by {amount}. New cap: {dailyTreeCap}.");
    }

    public void LoadState(int savedTreesPlantedToday, int savedDailyTreeCap, string savedLastGrantResetTimestamp)
    {
        treesPlantedToday = savedTreesPlantedToday;
        dailyTreeCap = savedDailyTreeCap;
        milestoneCapBonus = savedDailyTreeCap - BaseDailyTreeCap;

        if (DateTime.TryParse(savedLastGrantResetTimestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            lastResetDate = parsed.ToLocalTime().Date;
        }
        else
        {
            lastResetDate = DateTime.Now.Date;
        }

        lastGrantResetTimestamp = lastResetDate.ToString("o");

        CheckForDailyReset();
    }

    private void Update()
    {
        resetCheckTimer += Time.deltaTime;
        if (resetCheckTimer >= ResetCheckIntervalSeconds)
        {
            resetCheckTimer = 0f;
            CheckForDailyReset();
        }
    }

    private void CheckForDailyReset()
    {
        DateTime today = DateTime.Now.Date;
        if (today > lastResetDate)
        {
            ResetDailyAllowance();
        }
    }

    private void ResetDailyAllowance()
    {
        treesPlantedToday = 0;
        lastResetDate = DateTime.Now.Date;
        lastGrantResetTimestamp = lastResetDate.ToString("o");
        Debug.Log($"Daily grant reset. Tree cap: {dailyTreeCap}, planted today reset to 0.");
    }

    public bool TryConsumeTreePlant()
    {
        CheckForDailyReset();

        if (!CanPlantTree)
        {
            return false;
        }

        treesPlantedToday++;
        return true;
    }

    [ContextMenu("Debug: Simulate Next Day")]
    private void DebugSimulateNextDay()
    {
        lastResetDate = DateTime.Now.Date.AddDays(-1);
        CheckForDailyReset();
    }

    [ContextMenu("Debug: Force Reset Now")]
    public void DebugForceReset()
    {
        ResetDailyAllowance();
    }
}
