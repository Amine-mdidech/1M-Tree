using System.Collections.Generic;
using UnityEngine;

public class CreatureVisual : MonoBehaviour
{
    private const float HuntChance = 0.1f;
    private const float HuntDetectionRadius = 3f;

    // Over-bright multiply tints - values above 1 read as an over-bright/"fluorescent" glow
    // against the sprite's baked color even without an HDR/bloom pipeline. Herbivore vs. predator
    // so the highlight itself hints at what you tapped, not just that you tapped something.
    private static readonly Color HerbivoreHighlightColor = new Color(0.5f, 2.2f, 1.1f);
    private static readonly Color PredatorHighlightColor = new Color(2.2f, 0.5f, 0.7f);

    private const float WalkAnimationInterval = 0.2f;
    private const float MovementEpsilon = 0.001f;

    private const float NamePopupDurationSeconds = 1.6f;
    private const float NamePopupWorldHeightOffset = 0.55f;
    private const int NamePopupBackgroundWidth = 140;
    private const int NamePopupBackgroundHeight = 38;
    private const int NamePopupSortingOrder = 10;

    // Maintained registry of every live creature, avoiding a scene-wide FindObjectsByType scan
    // every time a predator looks for nearby prey.
    public static readonly List<CreatureVisual> ActiveCreatures = new List<CreatureVisual>();

    // Only one animal's name popup shows at a time; tapping a new animal hides whichever one
    // currently owns it.
    private static CreatureVisual currentPopupCreature;

    [SerializeField] private float moveSpeed = 0.5f;

    private Vector3 homePosition;
    private Vector3 targetPosition;
    private float wanderRadius = 2f;
    private float pickNewTargetTimer;
    private SpeciesDefinition species;
    private CreatureVisual huntTarget;
    private SpriteRenderer spriteRenderer;
    private Sprite[] walkFrames;
    private int currentFrameIndex;
    private float walkAnimationTimer;
    private Vector3 previousPosition;
    private bool awakeStateReady;

    private GameObject namePopupRoot;
    private TextMesh namePopupTextMesh;
    private float namePopupRemainingSeconds;

    public string SpeciesDisplayName => species != null ? species.displayName : "Unknown Animal";
    public string SpeciesId => species != null ? species.id : null;

    public void Initialize(SpeciesDefinition speciesDefinition, Vector3 home, float radius, Sprite[] frames)
    {
        EnsureAwakeState();

        species = speciesDefinition;
        homePosition = home;
        wanderRadius = radius;
        walkFrames = frames;
        previousPosition = home;
        PickNewTarget();
    }

    private void Awake()
    {
        EnsureAwakeState();
    }

    // Mirrors PlantedTree.EnsureAwakeState: AddComponent doesn't synchronously invoke Awake outside
    // a running Player/PlayMode loop - editor-driven code and EditMode tests can call Initialize
    // before Awake has run at all, leaving spriteRenderer null and this creature missing from
    // ActiveCreatures. Calling this from both paths, guarded by awakeStateReady, makes setup correct
    // regardless of which one runs first.
    private void EnsureAwakeState()
    {
        if (awakeStateReady)
        {
            return;
        }

        awakeStateReady = true;
        spriteRenderer = GetComponent<SpriteRenderer>();
        ActiveCreatures.Add(this);
    }

    private void OnDestroy()
    {
        ActiveCreatures.Remove(this);

        if (currentPopupCreature == this)
        {
            currentPopupCreature = null;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (!highlighted)
        {
            spriteRenderer.color = Color.white;
            return;
        }

        spriteRenderer.color = species != null && species.category == SpeciesDefinition.SpeciesCategory.Predator
            ? PredatorHighlightColor
            : HerbivoreHighlightColor;
    }

    // World-space name popup: a child object parented directly to this creature, so it follows
    // automatically via ordinary Transform parenting instead of being recomputed every frame from
    // a world->screen->panel conversion (that conversion chain was the source of a real, hard-to-
    // pin-down bug where the popup drifted or ended up on the wrong side of the animal after
    // panning the camera - a world-space child sidesteps it entirely, since it renders through the
    // exact same camera/projection as the creature sprite itself, guaranteeing they agree).
    public void ShowNamePopup()
    {
        if (currentPopupCreature != null && currentPopupCreature != this)
        {
            currentPopupCreature.HideNamePopupImmediate();
        }

        currentPopupCreature = this;
        namePopupRemainingSeconds = NamePopupDurationSeconds;
        SetHighlighted(true);

        if (namePopupRoot == null)
        {
            BuildNamePopup();
        }

        namePopupTextMesh.text = SpeciesDisplayName;
        namePopupRoot.SetActive(true);
    }

    private void BuildNamePopup()
    {
        namePopupRoot = new GameObject("NamePopup");
        namePopupRoot.transform.SetParent(transform, worldPositionStays: false);
        namePopupRoot.transform.localRotation = Quaternion.identity;

        // Counter-scale so the popup's own on-screen size and its offset above the creature stay
        // constant regardless of this creature's own scale (elephants and rabbits get the same
        // fixed spacer and readable text size, not a scaled-up/down one).
        float inverseScale = transform.localScale.y != 0f ? 1f / transform.localScale.y : 1f;
        namePopupRoot.transform.localScale = new Vector3(inverseScale, inverseScale, 1f);
        namePopupRoot.transform.localPosition = Vector3.up * (NamePopupWorldHeightOffset * inverseScale);

        GameObject backgroundGo = new GameObject("Background");
        backgroundGo.transform.SetParent(namePopupRoot.transform, worldPositionStays: false);
        backgroundGo.transform.localPosition = Vector3.zero;
        SpriteRenderer backgroundRenderer = backgroundGo.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = PixelArtUtility.ToSprite(
            PixelArtUtility.CreateTexturedPillBackground(NamePopupBackgroundWidth, NamePopupBackgroundHeight, new Color(0.094f, 0.078f, 0.071f, 0.9f)),
            200f);
        backgroundRenderer.sortingOrder = NamePopupSortingOrder;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(namePopupRoot.transform, worldPositionStays: false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        namePopupTextMesh = textGo.AddComponent<TextMesh>();
        namePopupTextMesh.anchor = TextAnchor.MiddleCenter;
        namePopupTextMesh.alignment = TextAlignment.Center;
        namePopupTextMesh.fontSize = 32;
        namePopupTextMesh.characterSize = 0.0675f; // 0.045 * 1.5 - 50% bigger
        namePopupTextMesh.color = Color.white;
        namePopupTextMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MeshRenderer textRenderer = textGo.GetComponent<MeshRenderer>();
        textRenderer.sharedMaterial = namePopupTextMesh.font.material;
        textRenderer.sortingOrder = NamePopupSortingOrder + 1;
    }

    private void HideNamePopupImmediate()
    {
        if (namePopupRoot != null)
        {
            namePopupRoot.SetActive(false);
        }

        SetHighlighted(false);

        if (currentPopupCreature == this)
        {
            currentPopupCreature = null;
        }
    }

    private void UpdateNamePopup()
    {
        if (namePopupRoot == null || !namePopupRoot.activeSelf)
        {
            return;
        }

        namePopupRemainingSeconds -= Time.deltaTime;
        if (namePopupRemainingSeconds <= 0f)
        {
            HideNamePopupImmediate();
        }
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

        UpdateWalkAnimation();
        UpdateNamePopup();
    }

    private void UpdateWalkAnimation()
    {
        if (spriteRenderer == null || walkFrames == null || walkFrames.Length < 2)
        {
            return;
        }

        bool isMoving = Vector3.Distance(transform.position, previousPosition) > MovementEpsilon;
        previousPosition = transform.position;

        if (!isMoving)
        {
            if (currentFrameIndex != 0)
            {
                currentFrameIndex = 0;
                spriteRenderer.sprite = walkFrames[0];
            }
            return;
        }

        walkAnimationTimer -= Time.deltaTime;
        if (walkAnimationTimer <= 0f)
        {
            walkAnimationTimer = WalkAnimationInterval;
            currentFrameIndex = 1 - currentFrameIndex;
            spriteRenderer.sprite = walkFrames[currentFrameIndex];
        }
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

        targetPosition = PickWanderTarget();
        pickNewTargetTimer = Random.Range(2f, 5f);
    }

    // Rejects candidates whose straight-line path from here would cross the river, retrying a
    // few times before giving up and just holding position — a light substitute for real
    // pathfinding that's enough to stop animals wandering straight across the water.
    private Vector3 PickWanderTarget()
    {
        const int maxAttempts = 6;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = homePosition + new Vector3(offset.x, offset.y, 0f);

            if (!DoesPathCrossRiver(transform.position, candidate))
            {
                return candidate;
            }
        }

        return transform.position;
    }

    private static bool DoesPathCrossRiver(Vector3 from, Vector3 to)
    {
        if (RiverManager.Instance == null)
        {
            return false;
        }

        const int sampleCount = 6;
        for (int i = 0; i <= sampleCount; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)sampleCount);
            if (RiverManager.Instance.IsBlocked(point))
            {
                return true;
            }
        }

        return false;
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
