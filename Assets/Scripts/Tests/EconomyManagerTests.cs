using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EconomyManagerTests
{
    private GameObject go;
    private EconomyManager economy;
    private GameObject emergenceGO;
    private EmergenceSystem emergenceSystem;

    [SetUp]
    public void SetUp()
    {
        EcosystemState.ResetAll();

        emergenceGO = new GameObject("TestEmergenceSystem");
        emergenceSystem = emergenceGO.AddComponent<EmergenceSystem>();

        go = new GameObject("TestEconomyManager");
        economy = go.AddComponent<EconomyManager>();
        SetPrivateField(economy, "emergenceSystem", emergenceSystem);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(emergenceGO);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    [Test]
    public void Awake_StartsWithStartingFunds()
    {
        Assert.AreEqual(300, economy.Funds);
    }

    [Test]
    public void CanAfford_TrueUpToExactBalance()
    {
        Assert.IsTrue(economy.CanAfford(300));
        Assert.IsFalse(economy.CanAfford(301));
    }

    [Test]
    public void TrySpend_SucceedsAndDeductsWhenAffordable()
    {
        bool result = economy.TrySpend(100);

        Assert.IsTrue(result);
        Assert.AreEqual(200, economy.Funds);
    }

    [Test]
    public void TrySpend_FailsAndLeavesBalanceWhenInsufficient()
    {
        bool result = economy.TrySpend(500);

        Assert.IsFalse(result);
        Assert.AreEqual(300, economy.Funds);
    }

    [Test]
    public void DebugForceGrant_AddsBaseAmountWithNoTreesOrSpecies()
    {
        economy.DebugForceGrant();

        Assert.AreEqual(300 + 300, economy.Funds);
    }

    [Test]
    public void DebugForceGrant_AddsTreeAndSpeciesContribution()
    {
        EcosystemState.RegisterTreePlanted("oak");
        EcosystemState.RegisterTreePlanted("oak");

        SpeciesDefinition species = ScriptableObject.CreateInstance<SpeciesDefinition>();
        species.id = "squirrel";
        species.category = SpeciesDefinition.SpeciesCategory.Herbivore;
        emergenceSystem.AddUnlockedSpecies(species);

        economy.DebugForceGrant();

        // starting 300 + base 300 + 2 trees * 1 + 1 species * 50
        Assert.AreEqual(300 + 300 + 2 + 50, economy.Funds);

        Object.DestroyImmediate(species);
    }

    [Test]
    public void AddDailyFundsBonus_RaisesFutureGrantAmount()
    {
        economy.AddDailyFundsBonus(50);

        economy.DebugForceGrant();

        Assert.AreEqual(300 + 300 + 50, economy.Funds);
    }

    [Test]
    public void LoadState_RestoresFundsAndCatchesUpOneDayGrant()
    {
        string yesterday = System.DateTime.Now.Date.AddDays(-1).ToString("o");

        economy.LoadState(50, 0, yesterday);

        // 50 restored + one catch-up grant of 300 (no trees/species)
        Assert.AreEqual(50 + 300, economy.Funds);
    }

    [Test]
    public void LoadState_SameDayTimestamp_DoesNotGrantAgain()
    {
        string today = System.DateTime.Now.Date.ToString("o");

        economy.LoadState(50, 0, today);

        Assert.AreEqual(50, economy.Funds);
    }

    [Test]
    public void LoadState_RestoresMilestoneFundsBonus()
    {
        economy.LoadState(50, 25, System.DateTime.Now.Date.ToString("o"));

        Assert.AreEqual(25, economy.MilestoneFundsBonus);
    }

    [Test]
    public void CanAfford_NegativeCost_ReturnsFalse()
    {
        Assert.IsFalse(economy.CanAfford(-1));
    }

    [Test]
    public void TrySpend_NegativeCost_FailsAndLeavesBalanceUnchanged()
    {
        bool result = economy.TrySpend(-5);

        Assert.IsFalse(result);
        Assert.AreEqual(300, economy.Funds);
    }

    [Test]
    public void LoadState_OfflineForAWeek_StillGrantsOnlyOneDay()
    {
        string aWeekAgo = System.DateTime.Now.Date.AddDays(-7).ToString("o");

        economy.LoadState(50, 0, aWeekAgo);

        // 50 restored + exactly one day's catch-up grant of 300, not seven
        Assert.AreEqual(50 + 300, economy.Funds);
    }

    [Test]
    public void AddFunds_IncreasesBalance()
    {
        economy.AddFunds(500);

        Assert.AreEqual(300 + 500, economy.Funds);
    }

    [Test]
    public void AddFunds_NegativeAmount_IsIgnored()
    {
        economy.AddFunds(-10);

        Assert.AreEqual(300, economy.Funds);
    }
}
