using NUnit.Framework;
using UnityEngine;

public class DailyGrantTests
{
    private GameObject go;
    private DailyGrant grant;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestDailyGrant");
        grant = go.AddComponent<DailyGrant>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Awake_SetsBaseCapAndAllowsPlanting()
    {
        Assert.AreEqual(30, grant.DailyTreeCap);
        Assert.AreEqual(0, grant.TreesPlantedToday);
        Assert.IsTrue(grant.CanPlantTree);
    }

    [Test]
    public void TryConsumeTreePlant_IncrementsCountUntilCapReached()
    {
        for (int i = 0; i < 30; i++)
        {
            Assert.IsTrue(grant.TryConsumeTreePlant());
        }

        Assert.AreEqual(30, grant.TreesPlantedToday);
        Assert.IsFalse(grant.CanPlantTree);
        Assert.IsFalse(grant.TryConsumeTreePlant());
    }

    [Test]
    public void AddDailyCapBonus_RaisesCapPermanently()
    {
        grant.AddDailyCapBonus(10);

        Assert.AreEqual(40, grant.DailyTreeCap);
    }

    [Test]
    public void DebugForceReset_ClearsTreesPlantedToday()
    {
        grant.TryConsumeTreePlant();
        grant.TryConsumeTreePlant();

        grant.DebugForceReset();

        Assert.AreEqual(0, grant.TreesPlantedToday);
        Assert.IsTrue(grant.CanPlantTree);
    }
}
