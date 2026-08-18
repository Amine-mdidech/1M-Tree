using System;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    private const int BaseDailyFunds = 300;
    private const int FundsPerTree = 1;
    private const int FundsPerSpecies = 50;
    public const int StartingFunds = 300;
    private const float ResetCheckIntervalSeconds = 5f;

    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private int funds;
    [SerializeField] private int milestoneFundsBonus;
    [SerializeField] private string lastFundsGrantTimestamp;

    private DateTime lastResetDate;
    private float resetCheckTimer;

    public int Funds => funds;
    public int MilestoneFundsBonus => milestoneFundsBonus;
    public string LastFundsGrantTimestamp => lastFundsGrantTimestamp;

    private void Awake()
    {
        milestoneFundsBonus = 0;
        funds = StartingFunds;
        lastResetDate = DateTime.Now.Date;
        lastFundsGrantTimestamp = lastResetDate.ToString("o");
    }

    public bool CanAfford(int cost)
    {
        return cost >= 0 && funds >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        funds -= cost;
        return true;
    }

    public void AddFunds(int amount)
    {
        if (amount < 0)
        {
            return;
        }

        funds += amount;
    }

    public void AddDailyFundsBonus(int amount)
    {
        milestoneFundsBonus += amount;
        Debug.Log($"Daily funds bonus increased by {amount}. New base daily funds: {BaseDailyFunds + milestoneFundsBonus}.");
    }

    public void LoadState(int savedFunds, int savedMilestoneFundsBonus, string savedLastFundsGrantTimestamp)
    {
        funds = savedFunds;
        milestoneFundsBonus = savedMilestoneFundsBonus;

        if (DateTime.TryParse(savedLastFundsGrantTimestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            lastResetDate = parsed.ToLocalTime().Date;
        }
        else
        {
            lastResetDate = DateTime.Now.Date;
        }

        lastFundsGrantTimestamp = lastResetDate.ToString("o");

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
            GrantDailyFunds();
        }
    }

    private void GrantDailyFunds()
    {
        int speciesCount = emergenceSystem != null ? emergenceSystem.UnlockedSpeciesCount : 0;
        int amount = BaseDailyFunds + milestoneFundsBonus
            + (EcosystemState.TotalTreeCount * FundsPerTree)
            + (speciesCount * FundsPerSpecies);

        funds += amount;
        lastResetDate = DateTime.Now.Date;
        lastFundsGrantTimestamp = lastResetDate.ToString("o");
        Debug.Log($"Daily funds granted: +{amount}. Total funds: {funds}.");
    }

    [ContextMenu("Debug: Simulate Next Day")]
    private void DebugSimulateNextDay()
    {
        lastResetDate = DateTime.Now.Date.AddDays(-1);
        CheckForDailyReset();
    }

    [ContextMenu("Debug: Force Grant Now")]
    public void DebugForceGrant()
    {
        GrantDailyFunds();
    }
}
