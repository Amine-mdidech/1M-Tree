using System.Collections.Generic;

public static class EcosystemState
{
    private static int totalTreeCount;
    private static readonly HashSet<string> distinctVegetationIds = new HashSet<string>();
    private static readonly Dictionary<string, int> speciesPopulation = new Dictionary<string, int>();

    public static int TotalTreeCount => totalTreeCount;
    public static int DistinctVegetationTypeCount => distinctVegetationIds.Count;

    public static void RegisterTreePlanted(string vegetationId)
    {
        totalTreeCount++;
        distinctVegetationIds.Add(vegetationId);
    }

    public static bool HasVegetationType(string vegetationId)
    {
        return distinctVegetationIds.Contains(vegetationId);
    }

    public static int GetSpeciesPopulation(string speciesId)
    {
        return speciesPopulation.TryGetValue(speciesId, out int count) ? count : 0;
    }

    public static void AddSpeciesIndividual(string speciesId)
    {
        speciesPopulation.TryGetValue(speciesId, out int count);
        speciesPopulation[speciesId] = count + 1;
    }

    public static void SetSpeciesPopulation(string speciesId, int population)
    {
        speciesPopulation[speciesId] = population;
    }

    public static IEnumerable<KeyValuePair<string, int>> GetAllPopulations()
    {
        return speciesPopulation;
    }

    public static void ResetAll()
    {
        totalTreeCount = 0;
        distinctVegetationIds.Clear();
        speciesPopulation.Clear();
    }
}
