using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class CameraPanController : MonoBehaviour
{
    private const float TapMaxDragScreenDistance = 20f;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private TreePlanter treePlanter;
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
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
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
                        if (!tappedTree.IsMature && treeBoostUI != null)
                        {
                            treeBoostUI.Open(tappedTree);
                        }
                    }
                    else if (treePlanter != null)
                    {
                        treePlanter.PlantTree(worldPoint);
                    }
                }
            }
        }

        isPressed = false;
        isDragging = false;
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
