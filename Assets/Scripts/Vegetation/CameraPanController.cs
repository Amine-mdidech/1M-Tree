using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class CameraPanController : MonoBehaviour
{
    private const float TapMaxDragScreenDistance = 20f;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private LaboratoryBuilding laboratoryBuilding;
    [SerializeField] private TreeBoostUI treeBoostUI;
    [SerializeField] private List<UIDocument> blockingUIDocuments = new List<UIDocument>();
    [SerializeField] private float mapHalfWidth = 25f;
    [SerializeField] private float mapHalfHeight = 25f;
    [SerializeField] private float treeClickRadius = 0.6f;
    [SerializeField] private float creatureClickRadius = 0.5f;

    private bool isPressed;
    private bool isDragging;
    private Vector2 pressScreenPosition;
    private Vector2 lastScreenPosition;
    private PlantedTree treeBeingMoved;

    public static CameraPanController Instance { get; private set; }

    public float MapHalfWidth => mapHalfWidth;
    public float MapHalfHeight => mapHalfHeight;
    public Camera TargetCamera => targetCamera;
    public Vector3 CameraWorldPosition => targetCamera.transform.position;
    public float ViewportHalfWidth => targetCamera.orthographicSize * targetCamera.aspect;
    public float ViewportHalfHeight => targetCamera.orthographicSize;

    public void JumpToWorldPosition(Vector2 worldPosition)
    {
        Vector3 target = new Vector3(worldPosition.x, worldPosition.y, targetCamera.transform.position.z);
        targetCamera.transform.position = ClampToMapBounds(target);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Arms move mode: the next tap that lands on a valid spot relocates this tree there instead of
    // triggering the usual tap-a-tree/tap-empty-ground behavior. Called by TreeBoostUI's Move
    // button.
    public void BeginMoveTree(PlantedTree tree)
    {
        if (tree == null)
        {
            return;
        }

        treeBeingMoved = tree;
        HudUI.Instance?.ShowMoveTreeBanner(tree.Vegetation.displayName);
    }

    public void CancelMoveTree()
    {
        treeBeingMoved = null;
        HudUI.Instance?.HideMoveTreeBanner();
    }

    private void Update()
    {
        if (Pointer.current == null)
        {
            return;
        }

        if (Pointer.current.press.wasPressedThisFrame)
        {
            BeginPress();
        }
        else if (isPressed && Pointer.current.press.isPressed)
        {
            ContinuePress();
        }
        else if (isPressed && Pointer.current.press.wasReleasedThisFrame)
        {
            EndPress();
        }
    }

    private void BeginPress()
    {
        Vector2 screenPosition = Pointer.current.position.ReadValue();

        if (IsPointerOverBlockingUI(screenPosition))
        {
            isPressed = false;
            return;
        }

        isPressed = true;
        isDragging = false;
        pressScreenPosition = screenPosition;
        lastScreenPosition = screenPosition;
    }

    private void ContinuePress()
    {
        Vector2 currentScreenPosition = Pointer.current.position.ReadValue();

        if (!isDragging)
        {
            Vector2 totalDelta = currentScreenPosition - pressScreenPosition;
            if (totalDelta.magnitude >= TapMaxDragScreenDistance)
            {
                isDragging = true;
                lastScreenPosition = currentScreenPosition;
            }
        }

        if (isDragging)
        {
            Vector2 frameDelta = currentScreenPosition - lastScreenPosition;
            PanCamera(frameDelta);
            lastScreenPosition = currentScreenPosition;
        }
    }

    private void EndPress()
    {
        if (!isDragging)
        {
            Vector2 releaseScreenPosition = Pointer.current.position.ReadValue();
            Vector3 worldPoint = targetCamera.ScreenToWorldPoint(releaseScreenPosition);
            worldPoint.z = 0f;

            if (treeBeingMoved != null)
            {
                ResolveMoveTreeTap(worldPoint);
            }
            else
            {
                bool handledByLab = laboratoryBuilding != null && laboratoryBuilding.TryHandleTap(worldPoint);

                if (!handledByLab)
                {
                    CreatureVisual tappedCreature = FindCreatureNear(worldPoint);
                    if (tappedCreature != null)
                    {
                        AudioManager.Instance?.PlayTap();
                        tappedCreature.ShowNamePopup();
                    }
                    else
                    {
                        PlantedTree tappedTree = FindTreeNear(worldPoint);
                        if (tappedTree != null)
                        {
                            treeBoostUI?.Open(tappedTree);
                        }
                        else
                        {
                            HandleEmptyGroundTap(worldPoint);
                        }
                    }
                }
            }
        }

        isPressed = false;
        isDragging = false;
    }

    // Routes an empty-ground tap based on the HUD's active picker category: Plant places a tree
    // (unchanged behavior); Herbivore/Predator releases whatever species the picker is currently
    // offering for that category at the tapped position.
    private void HandleEmptyGroundTap(Vector3 worldPoint)
    {
        HudUI.PickerCategory category = HudUI.Instance != null
            ? HudUI.Instance.CurrentPickerCategory
            : HudUI.PickerCategory.Plant;

        if (category == HudUI.PickerCategory.Plant)
        {
            treePlanter?.PlantTree(worldPoint);
            return;
        }

        SpeciesDefinition species = HudUI.Instance?.CurrentPickerSpecies();
        if (species != null)
        {
            emergenceSystem?.TryReleaseSpeciesAt(species, worldPoint);
        }
    }

    // A tap on the tree currently being moved cancels the move (a quick way to back out without
    // hunting for the banner's Cancel button); any other tap is treated as a destination attempt.
    private void ResolveMoveTreeTap(Vector3 worldPoint)
    {
        if (Vector2.Distance(treeBeingMoved.transform.position, worldPoint) <= treeClickRadius)
        {
            CancelMoveTree();
            return;
        }

        if (treePlanter != null && treePlanter.MoveTree(treeBeingMoved, worldPoint))
        {
            treeBeingMoved = null;
            HudUI.Instance?.HideMoveTreeBanner();
        }
        else
        {
            AudioManager.Instance?.PlayError();
        }
    }

    private PlantedTree FindTreeNear(Vector3 worldPosition)
    {
        PlantedTree closest = null;
        float closestDistance = treeClickRadius;

        foreach (PlantedTree tree in PlantedTree.ActiveTrees)
        {
            // ActiveTrees can transiently hold a destroyed entry (Destroy()/DestroyImmediate()
            // don't always invoke OnDestroy synchronously) - this is now called every frame a
            // press is held for the placement preview, not just once per tap, so a stale entry
            // here was throwing on every held press and aborting tap-to-plant entirely.
            if (tree == null)
            {
                continue;
            }

            float distance = Vector2.Distance(tree.transform.position, worldPosition);
            if (distance <= closestDistance)
            {
                closest = tree;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private CreatureVisual FindCreatureNear(Vector3 worldPosition)
    {
        CreatureVisual closest = null;
        float closestDistance = creatureClickRadius;

        foreach (CreatureVisual creature in CreatureVisual.ActiveCreatures)
        {
            if (creature == null)
            {
                continue;
            }

            float distance = Vector2.Distance(creature.transform.position, worldPosition);
            if (distance <= closestDistance)
            {
                closest = creature;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void PanCamera(Vector2 screenDelta)
    {
        float worldUnitsPerPixel = (targetCamera.orthographicSize * 2f) / Screen.height;
        Vector3 worldDelta = new Vector3(-screenDelta.x * worldUnitsPerPixel, -screenDelta.y * worldUnitsPerPixel, 0f);

        Vector3 newPosition = ClampToMapBounds(targetCamera.transform.position + worldDelta);
        targetCamera.transform.position = newPosition;
    }

    private Vector3 ClampToMapBounds(Vector3 position)
    {
        float halfViewportHeight = targetCamera.orthographicSize;
        float halfViewportWidth = halfViewportHeight * targetCamera.aspect;

        float minX = -mapHalfWidth + halfViewportWidth;
        float maxX = mapHalfWidth - halfViewportWidth;
        float minY = -mapHalfHeight + halfViewportHeight;
        float maxY = mapHalfHeight - halfViewportHeight;

        float clampedX = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : 0f;
        float clampedY = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : 0f;

        return new Vector3(clampedX, clampedY, position.z);
    }

    private bool IsPointerOverBlockingUI(Vector2 screenPosition)
    {
        foreach (UIDocument document in blockingUIDocuments)
        {
            if (document == null)
            {
                continue;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null || root.panel == null)
            {
                continue;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, screenPosition);
            if (root.panel.Pick(panelPosition) != null)
            {
                return true;
            }
        }

        return false;
    }
}
