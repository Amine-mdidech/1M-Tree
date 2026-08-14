using NUnit.Framework;
using UnityEditor;

public class SpeciesRosterTests
{
    [Test]
    public void SurvivingSpecies_StillExist()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Gazelle.asset"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Fox.asset"));
    }

    [Test]
    public void SurvivingVegetation_StillExist()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Oak.asset"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Pine.asset"));
    }

    [Test]
    public void DeletedSpecies_NoLongerExist()
    {
        string[] deletedIds = { "Antelope", "Boar", "Buffalo", "Deer", "Eagle", "Elephant", "Giraffe", "Goat", "Hare", "Lion", "Moose", "Rabbit", "Zebra" };
        foreach (string id in deletedIds)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>($"Assets/Data/Species/{id}.asset"), $"{id}.asset should have been deleted");
        }
    }

    [Test]
    public void DeletedVegetation_NoLongerExist()
    {
        string[] deletedIds = { "Bamboo", "BerryBush", "Birch", "Fern", "Mangrove", "Maple", "Reed", "Wildflower", "Willow" };
        foreach (string id in deletedIds)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<VegetationDefinition>($"Assets/Data/Vegetation/{id}.asset"), $"{id}.asset should have been deleted");
        }
    }

    [Test]
    public void Wolf_PreysOnGazelle()
    {
        SpeciesDefinition wolf = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset");
        CollectionAssert.AreEqual(new[] { "gazelle" }, wolf.preySpeciesIds);
    }

    [Test]
    public void Fox_PreysOnSquirrel()
    {
        SpeciesDefinition fox = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Fox.asset");
        CollectionAssert.AreEqual(new[] { "squirrel" }, fox.preySpeciesIds);
    }

    [Test]
    public void PineResearch_HasNoPrerequisite()
    {
        ResearchProjectDefinition pineResearch = AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/PineResearch.asset");
        Assert.IsEmpty(pineResearch.prerequisiteProjectIds);
    }

    [Test]
    public void FoxResearch_HasNoPrerequisite()
    {
        ResearchProjectDefinition foxResearch = AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/FoxResearch.asset");
        Assert.IsEmpty(foxResearch.prerequisiteProjectIds);
    }

    [Test]
    public void Squirrel_ExistsWithCorrectData()
    {
        SpeciesDefinition squirrel = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Squirrel.asset");
        Assert.IsNotNull(squirrel);
        Assert.AreEqual("squirrel", squirrel.id);
        Assert.AreEqual(SpeciesDefinition.SpeciesCategory.Herbivore, squirrel.category);
        Assert.IsFalse(squirrel.unlockedByDefault);
    }

    [Test]
    public void SquirrelResearch_ExistsWithNoPrerequisite()
    {
        ResearchProjectDefinition squirrelResearch = AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/SquirrelResearch.asset");
        Assert.IsNotNull(squirrelResearch);
        Assert.AreEqual("squirrel", squirrelResearch.unlocksSpeciesId);
        Assert.IsEmpty(squirrelResearch.prerequisiteProjectIds);
    }
}
