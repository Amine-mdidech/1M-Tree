using NUnit.Framework;
using UnityEngine;

public class MilestoneManagerTests
{
    private GameObject go;
    private MilestoneManager manager;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestMilestoneManager");
        manager = go.AddComponent<MilestoneManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
    }

    [Test]
    public void AddGems_IncreasesBalance()
    {
        manager.AddGems(50);

        Assert.AreEqual(50, manager.Gems);
    }

    [Test]
    public void AddGems_NegativeAmount_IsIgnored()
    {
        manager.AddGems(-10);

        Assert.AreEqual(0, manager.Gems);
    }

    [Test]
    public void TrySpendGems_SucceedsWhenAffordable()
    {
        manager.AddGems(20);

        bool result = manager.TrySpendGems(15);

        Assert.IsTrue(result);
        Assert.AreEqual(5, manager.Gems);
    }

    [Test]
    public void TrySpendGems_FailsWhenInsufficient()
    {
        manager.AddGems(5);

        bool result = manager.TrySpendGems(10);

        Assert.IsFalse(result);
        Assert.AreEqual(5, manager.Gems);
    }

    [Test]
    public void TrySpendGems_NegativeAmount_Fails()
    {
        manager.AddGems(20);

        bool result = manager.TrySpendGems(-5);

        Assert.IsFalse(result);
        Assert.AreEqual(20, manager.Gems);
    }
}
