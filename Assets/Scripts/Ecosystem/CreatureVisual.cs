using System.Collections.Generic;
using UnityEngine;

public class CreatureVisual : MonoBehaviour
{
    private const float HuntChance = 0.1f;
    private const float HuntDetectionRadius = 3f;

    // Maintained registry of every live creature, avoiding a scene-wide FindObjectsByType scan
    // every time a predator looks for nearby prey.
    public static readonly List<CreatureVisual> ActiveCreatures = new List<CreatureVisual>();

    [SerializeField] private float moveSpeed = 0.5f;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private float wanderRadius = 2f;
    private float pickNewTargetTimer;
    private SpeciesDefinition species;
    private CreatureVisual huntTarget;

    public void Initialize(SpeciesDefinition speciesDefinition, Vector3 home, float radius)
    {
        species = speciesDefinition;
        homePosition = home;
        wanderRadius = radius;
        PickNewTarget();
    }

    private void Awake()
    {
        ActiveCreatures.Add(this);
    }

    private void OnDestroy()
    {
        ActiveCreatures.Remove(this);
    }

    private void Update()
    {
        pickNewTargetTimer -= Time.deltaTime;
        if (pickNewTargetTimer <= 0f)
        {
            PickNewTarget();
        }

        Vector3 destination = huntTarget != null ? huntTarget.transform.position : targetPosition;
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
    }

    private void PickNewTarget()
    {
        huntTarget = null;

        if (IsPredatorWithPrey() && Random.value < HuntChance)
        {
            CreatureVisual nearbyPrey = FindNearbyPrey();
            if (nearbyPrey != null)
            {
                huntTarget = nearbyPrey;
                pickNewTargetTimer = Random.Range(2f, 5f);
                return;
            }
        }

        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        targetPosition = homePosition + new Vector3(offset.x, offset.y, 0f);
        pickNewTargetTimer = Random.Range(2f, 5f);
    }

    private bool IsPredatorWithPrey()
    {
        return species != null
            && species.category == SpeciesDefinition.SpeciesCategory.Predator
            && species.preySpeciesIds != null
            && species.preySpeciesIds.Length > 0;
    }

    private CreatureVisual FindNearbyPrey()
    {
        CreatureVisual closest = null;
        float closestDistance = HuntDetectionRadius;

        foreach (CreatureVisual other in ActiveCreatures)
        {
            if (other == this || other.species == null)
            {
                continue;
            }

            if (System.Array.IndexOf(species.preySpeciesIds, other.species.id) < 0)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, other.transform.position);
            if (distance <= closestDistance)
            {
                closest = other;
                closestDistance = distance;
            }
        }

        return closest;
    }
}
