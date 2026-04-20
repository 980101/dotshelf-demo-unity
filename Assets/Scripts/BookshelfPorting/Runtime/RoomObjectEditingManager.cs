using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    public class RoomObjectEditingManager : MonoBehaviour
    {
        private static readonly Vector2[] SpawnViewportSamples =
        {
            new Vector2(0.50f, 0.28f),
            new Vector2(0.50f, 0.22f),
            new Vector2(0.42f, 0.24f),
            new Vector2(0.58f, 0.24f),
            new Vector2(0.50f, 0.16f),
            new Vector2(0.35f, 0.18f),
            new Vector2(0.65f, 0.18f),
        };

        private readonly struct PointerState
        {
            public PointerState(bool isValid, Vector2 position, bool wasPressed, bool isPressed, bool wasReleased, int pointerId)
            {
                IsValid = isValid;
                Position = position;
                WasPressed = wasPressed;
                IsPressed = isPressed;
                WasReleased = wasReleased;
                PointerId = pointerId;
            }

            public bool IsValid { get; }
            public Vector2 Position { get; }
            public bool WasPressed { get; }
            public bool IsPressed { get; }
            public bool WasReleased { get; }
            public int PointerId { get; }
        }

        [SerializeField] private Camera sceneCamera = null;
        [SerializeField] private BookshelfGenerator generator = null;
        [SerializeField] private BookshelfExperienceManager experienceManager = null;
        [SerializeField] private float rotateStepDegrees = 15f;
        [SerializeField] private float spawnDistanceFromCamera = 1.25f;
        [SerializeField] private float minimumPlacementPadding = 0.12f;

        private Canvas hudCanvas;
        private Button inventoryButton;
        private GameObject inventoryPanel;
        private GameObject editPanel;
        private Text selectedNameText;
        private Text editHintText;
        private Button moveButton;
        private Button rotateLeftButton;
        private Button rotateRightButton;
        private Button deleteButton;
        private RoomEditableObject selectedObject;
        private Bounds roomFloorBounds;
        private bool hasRoomFloorBounds;
        private bool isInventoryOpen;
        private bool isMoveMode;
        private bool isDraggingSelection;
        private Vector3 dragOffset;

        public void Configure(BookshelfGenerator bookshelfGenerator, BookshelfExperienceManager manager, Camera targetCamera)
        {
            generator = bookshelfGenerator;
            experienceManager = manager;
            sceneCamera = targetCamera;
        }

        private void Awake()
        {
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            EnsureEventSystem();
            EnsureHud();
        }

        private void Start()
        {
            RefreshRoomFloorBounds();
            RefreshUi();
        }

        private void Update()
        {
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            if (!hasRoomFloorBounds)
            {
                RefreshRoomFloorBounds();
            }

            if (selectedObject == null && (isMoveMode || isDraggingSelection))
            {
                isMoveMode = false;
                isDraggingSelection = false;
                RefreshUi();
            }

            if (!IsRoomEditingAvailable())
            {
                ResetEditingState();
                return;
            }

            if (!TryGetPointerState(out var pointerState))
            {
                return;
            }

            var pointerOverUi = IsPointerOverUi(pointerState.PointerId);
            if (isInventoryOpen)
            {
                if (pointerState.WasPressed && !pointerOverUi)
                {
                    isInventoryOpen = false;
                    RefreshUi();
                }

                return;
            }

            if (pointerOverUi)
            {
                if (pointerState.WasReleased && isDraggingSelection)
                {
                    CompleteMove();
                }

                return;
            }

            if (isMoveMode && selectedObject != null)
            {
                HandleMoveInput(pointerState);
                return;
            }

            if (!pointerState.WasPressed)
            {
                return;
            }

            SetSelectedObject(RaycastEditableObject(pointerState.Position));
        }

        private void ToggleInventoryPanel()
        {
            if (!IsRoomEditingAvailable())
            {
                return;
            }

            isInventoryOpen = !isInventoryOpen;
            if (isInventoryOpen)
            {
                SetSelectedObject(null);
            }

            RefreshUi();
        }

        private void BeginMoveMode()
        {
            if (selectedObject == null)
            {
                return;
            }

            isMoveMode = true;
            isDraggingSelection = false;
            RefreshUi();
        }

        private void RotateSelected(float deltaDegrees)
        {
            if (selectedObject == null)
            {
                return;
            }

            selectedObject.transform.Rotate(Vector3.up, deltaDegrees, Space.World);
            selectedObject.transform.position = ClampToFloorBounds(selectedObject.transform.position, selectedObject);
            RefreshUi();
        }

        private void DeleteSelected()
        {
            if (selectedObject == null)
            {
                return;
            }

            var target = selectedObject.gameObject;
            SetSelectedObject(null);
            DestroyUnityObject(target);
        }

        private void AddPlaceableFromInventory(RoomPlaceableType type)
        {
            if (generator == null)
            {
                return;
            }

            RefreshRoomFloorBounds();

            var spawnRotation = GetSpawnRotation();
            var spawnPosition = GetSpawnPosition(type);
            var spawnedObject = generator.SpawnRoomPlaceable(type, spawnPosition, spawnRotation);
            if (spawnedObject == null)
            {
                return;
            }

            spawnedObject.transform.position = ClampToFloorBounds(spawnedObject.transform.position, spawnedObject);
            isInventoryOpen = false;
            SetSelectedObject(spawnedObject);
            BeginMoveMode();
        }

        private void HandleMoveInput(PointerState pointerState)
        {
            if (selectedObject == null)
            {
                return;
            }

            if (pointerState.WasPressed)
            {
                var hitObject = RaycastEditableObject(pointerState.Position);
                if (hitObject == null)
                {
                    SetSelectedObject(null);
                    return;
                }

                if (hitObject != selectedObject)
                {
                    SetSelectedObject(hitObject);
                    return;
                }

                if (!TryGetPlacementPoint(pointerState.Position, out var placementPoint))
                {
                    return;
                }

                dragOffset = selectedObject.transform.position - new Vector3(placementPoint.x, selectedObject.transform.position.y, placementPoint.z);
                isDraggingSelection = true;
                ApplyDraggedPosition(placementPoint);
                return;
            }

            if (pointerState.IsPressed && isDraggingSelection)
            {
                if (TryGetPlacementPoint(pointerState.Position, out var placementPoint))
                {
                    ApplyDraggedPosition(placementPoint);
                }

                return;
            }

            if (pointerState.WasReleased && isDraggingSelection)
            {
                CompleteMove();
            }
        }

        private void ApplyDraggedPosition(Vector3 placementPoint)
        {
            if (selectedObject == null)
            {
                return;
            }

            var targetPosition = new Vector3(
                placementPoint.x + dragOffset.x,
                selectedObject.transform.position.y,
                placementPoint.z + dragOffset.z);

            selectedObject.transform.position = ClampToFloorBounds(targetPosition, selectedObject);
        }

        private void CompleteMove()
        {
            isDraggingSelection = false;
            isMoveMode = false;
            RefreshUi();
        }

        private void SetSelectedObject(RoomEditableObject nextObject)
        {
            if (selectedObject == nextObject)
            {
                RefreshUi();
                return;
            }

            if (selectedObject != null)
            {
                selectedObject.SetHighlighted(false);
            }

            selectedObject = nextObject;
            isMoveMode = false;
            isDraggingSelection = false;

            if (selectedObject != null)
            {
                selectedObject.SetHighlighted(true);
            }

            RefreshUi();
        }

        private void ResetEditingState()
        {
            if (selectedObject == null && !isInventoryOpen && !isMoveMode && !isDraggingSelection)
            {
                RefreshUi();
                return;
            }

            SetSelectedObject(null);
            isInventoryOpen = false;
            isMoveMode = false;
            isDraggingSelection = false;
            RefreshUi();
        }

        private bool IsRoomEditingAvailable()
        {
            return sceneCamera != null &&
                   (experienceManager == null || experienceManager.CurrentFocusArea == ExperienceFocusArea.QuarterView);
        }

        private void RefreshRoomFloorBounds()
        {
            if (generator == null || generator.RoomRoot == null)
            {
                hasRoomFloorBounds = false;
                return;
            }

            var floor = generator.RoomRoot.Find("Floor");
            if (floor == null)
            {
                hasRoomFloorBounds = false;
                return;
            }

            var renderer = floor.GetComponent<Renderer>();
            if (renderer == null)
            {
                hasRoomFloorBounds = false;
                return;
            }

            roomFloorBounds = renderer.bounds;
            hasRoomFloorBounds = true;
        }

        private Vector3 GetSpawnPosition(RoomPlaceableType type)
        {
            var floorHeight = generator != null ? generator.GetDefaultPlacementHeight(type) : 0f;
            var targetPosition = hasRoomFloorBounds
                ? new Vector3(roomFloorBounds.center.x, floorHeight, roomFloorBounds.center.z)
                : new Vector3(0f, floorHeight, 0f);

            if (sceneCamera == null)
            {
                return targetPosition;
            }

            if (TryGetVisibleSpawnPosition(floorHeight, out var visibleSpawnPosition))
            {
                return visibleSpawnPosition;
            }

            targetPosition = ClampToFloorBounds(targetPosition, null);
            if (IsWorldPositionVisible(targetPosition))
            {
                return targetPosition;
            }

            var flatForward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude <= Mathf.Epsilon)
            {
                return targetPosition;
            }

            flatForward.Normalize();
            var candidate = sceneCamera.transform.position + flatForward * spawnDistanceFromCamera;
            targetPosition = new Vector3(candidate.x, floorHeight, candidate.z);
            targetPosition = ClampToFloorBounds(targetPosition, null);
            return IsWorldPositionVisible(targetPosition)
                ? targetPosition
                : hasRoomFloorBounds
                    ? new Vector3(roomFloorBounds.center.x, floorHeight, roomFloorBounds.center.z)
                    : targetPosition;
        }

        private Quaternion GetSpawnRotation()
        {
            if (sceneCamera == null)
            {
                return Quaternion.identity;
            }

            var flatForward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up);
            return flatForward.sqrMagnitude <= Mathf.Epsilon
                ? Quaternion.identity
                : Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        private bool TryGetPlacementPoint(Vector2 screenPosition, out Vector3 placementPoint)
        {
            if (sceneCamera == null)
            {
                placementPoint = Vector3.zero;
                return false;
            }

            var floorHeight = selectedObject != null ? selectedObject.FloorHeight : 0f;
            return TryGetPlacementPoint(sceneCamera.ScreenPointToRay(screenPosition), floorHeight, out placementPoint);
        }

        private bool TryGetVisibleSpawnPosition(float floorHeight, out Vector3 spawnPosition)
        {
            spawnPosition = Vector3.zero;
            if (sceneCamera == null)
            {
                return false;
            }

            for (var i = 0; i < SpawnViewportSamples.Length; i++)
            {
                if (!TryGetViewportPlacementPoint(SpawnViewportSamples[i], floorHeight, out var placementPoint))
                {
                    continue;
                }

                if (!IsWithinFloorBounds(placementPoint, null))
                {
                    continue;
                }

                spawnPosition = new Vector3(placementPoint.x, floorHeight, placementPoint.z);
                return true;
            }

            for (var i = 0; i < SpawnViewportSamples.Length; i++)
            {
                if (!TryGetViewportPlacementPoint(SpawnViewportSamples[i], floorHeight, out var placementPoint))
                {
                    continue;
                }

                var clampedPosition = ClampToFloorBounds(new Vector3(placementPoint.x, floorHeight, placementPoint.z), null);
                if (!IsWorldPositionVisible(clampedPosition))
                {
                    continue;
                }

                spawnPosition = clampedPosition;
                return true;
            }

            return false;
        }

        private bool TryGetViewportPlacementPoint(Vector2 viewportPosition, float floorHeight, out Vector3 placementPoint)
        {
            if (sceneCamera == null)
            {
                placementPoint = Vector3.zero;
                return false;
            }

            return TryGetPlacementPoint(
                sceneCamera.ViewportPointToRay(new Vector3(viewportPosition.x, viewportPosition.y, 0f)),
                floorHeight,
                out placementPoint);
        }

        private static bool TryGetPlacementPoint(Ray ray, float floorHeight, out Vector3 placementPoint)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, floorHeight, 0f));
            if (!plane.Raycast(ray, out var distance))
            {
                placementPoint = Vector3.zero;
                return false;
            }

            placementPoint = ray.GetPoint(distance);
            return true;
        }

        private Vector3 ClampToFloorBounds(Vector3 worldPosition, RoomEditableObject editableObject)
        {
            if (!hasRoomFloorBounds)
            {
                return worldPosition;
            }

            GetFloorBoundsLimits(editableObject, out var minX, out var maxX, out var minZ, out var maxZ);

            if (minX > maxX)
            {
                worldPosition.x = roomFloorBounds.center.x;
            }
            else
            {
                worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            }

            if (minZ > maxZ)
            {
                worldPosition.z = roomFloorBounds.center.z;
            }
            else
            {
                worldPosition.z = Mathf.Clamp(worldPosition.z, minZ, maxZ);
            }

            return worldPosition;
        }

        private bool IsWithinFloorBounds(Vector3 worldPosition, RoomEditableObject editableObject)
        {
            if (!hasRoomFloorBounds)
            {
                return true;
            }

            GetFloorBoundsLimits(editableObject, out var minX, out var maxX, out var minZ, out var maxZ);
            return worldPosition.x >= minX &&
                   worldPosition.x <= maxX &&
                   worldPosition.z >= minZ &&
                   worldPosition.z <= maxZ;
        }

        private void GetFloorBoundsLimits(RoomEditableObject editableObject, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            var paddingX = minimumPlacementPadding;
            var paddingZ = minimumPlacementPadding;

            if (editableObject != null && editableObject.InteractionCollider != null)
            {
                var extents = editableObject.InteractionCollider.bounds.extents;
                paddingX = Mathf.Max(paddingX, extents.x);
                paddingZ = Mathf.Max(paddingZ, extents.z);
            }

            minX = roomFloorBounds.min.x + paddingX;
            maxX = roomFloorBounds.max.x - paddingX;
            minZ = roomFloorBounds.min.z + paddingZ;
            maxZ = roomFloorBounds.max.z - paddingZ;
        }

        private bool IsWorldPositionVisible(Vector3 worldPosition)
        {
            if (sceneCamera == null)
            {
                return false;
            }

            var viewportPosition = sceneCamera.WorldToViewportPoint(worldPosition);
            return viewportPosition.z > 0f &&
                   viewportPosition.x >= 0.04f &&
                   viewportPosition.x <= 0.96f &&
                   viewportPosition.y >= 0.04f &&
                   viewportPosition.y <= 0.96f;
        }

        private RoomEditableObject RaycastEditableObject(Vector2 screenPosition)
        {
            if (sceneCamera == null)
            {
                return null;
            }

            var ray = sceneCamera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits == null || hits.Length == 0)
            {
                return null;
            }

            var closestDistance = float.MaxValue;
            RoomEditableObject closestObject = null;
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || hits[i].distance >= closestDistance)
                {
                    continue;
                }

                var editableObject = collider.GetComponentInParent<RoomEditableObject>();
                if (editableObject == null)
                {
                    continue;
                }

                closestDistance = hits[i].distance;
                closestObject = editableObject;
            }

            return closestObject;
        }

        private void EnsureHud()
        {
            if (hudCanvas != null)
            {
                return;
            }

            var canvasObject = new GameObject("RoomEditHud");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.overrideSorting = true;
            hudCanvas.sortingOrder = 20;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            inventoryButton = CreateButton(
                canvasObject.transform,
                "InventoryButton",
                "Inventory",
                new Vector2(-24f, 24f),
                ToggleInventoryPanel,
                new Vector2(154f, 46f),
                new Vector2(1f, 0f));

            editPanel = CreatePanel(canvasObject.transform, "RoomEditPanel", new Vector2(24f, 24f), new Vector2(288f, 240f), new Vector2(0f, 0f));
            var editTitle = CreateLabel(editPanel.transform, "EditTitleText", "Placed Object", new Vector2(16f, -14f), new Vector2(256f, 20f), TextAnchor.UpperLeft);
            editTitle.fontSize = 14;
            editTitle.fontStyle = FontStyle.Bold;
            editTitle.color = new Color32(255, 229, 196, 255);

            selectedNameText = CreateLabel(editPanel.transform, "SelectedNameText", string.Empty, new Vector2(16f, -38f), new Vector2(256f, 24f), TextAnchor.UpperLeft);
            selectedNameText.fontSize = 20;
            selectedNameText.fontStyle = FontStyle.Bold;

            editHintText = CreateLabel(editPanel.transform, "EditHintText", string.Empty, new Vector2(16f, -66f), new Vector2(256f, 30f), TextAnchor.UpperLeft);
            editHintText.fontSize = 12;
            editHintText.color = new Color32(219, 224, 229, 255);
            editHintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            moveButton = CreateButton(editPanel.transform, "MoveButton", "Move", new Vector2(16f, -106f), BeginMoveMode, new Vector2(256f, 34f), new Vector2(0f, 1f));
            rotateLeftButton = CreateButton(editPanel.transform, "RotateLeftButton", "Rotate Left", new Vector2(16f, -146f), () => RotateSelected(-rotateStepDegrees), new Vector2(122f, 34f), new Vector2(0f, 1f));
            rotateRightButton = CreateButton(editPanel.transform, "RotateRightButton", "Rotate Right", new Vector2(150f, -146f), () => RotateSelected(rotateStepDegrees), new Vector2(122f, 34f), new Vector2(0f, 1f));
            deleteButton = CreateButton(editPanel.transform, "DeleteButton", "Delete", new Vector2(16f, -186f), DeleteSelected, new Vector2(256f, 34f), new Vector2(0f, 1f));

            inventoryPanel = CreatePanel(canvasObject.transform, "RoomInventoryPanel", new Vector2(-24f, 82f), new Vector2(252f, 308f), new Vector2(1f, 0f));
            var inventoryTitle = CreateLabel(inventoryPanel.transform, "InventoryTitleText", "Object Inventory", new Vector2(16f, -14f), new Vector2(220f, 20f), TextAnchor.UpperLeft);
            inventoryTitle.fontSize = 16;
            inventoryTitle.fontStyle = FontStyle.Bold;
            inventoryTitle.color = new Color32(255, 229, 196, 255);

            var inventoryHint = CreateLabel(inventoryPanel.transform, "InventoryHintText", "Choose a new object to add to the room.", new Vector2(16f, -40f), new Vector2(220f, 34f), TextAnchor.UpperLeft);
            inventoryHint.fontSize = 12;
            inventoryHint.color = new Color32(219, 224, 229, 255);
            inventoryHint.horizontalOverflow = HorizontalWrapMode.Wrap;

            CreateButton(inventoryPanel.transform, "AddHamhamButton", "Hamham", new Vector2(16f, -92f), () => AddPlaceableFromInventory(RoomPlaceableType.Hamham), new Vector2(220f, 34f), new Vector2(0f, 1f));
            CreateButton(inventoryPanel.transform, "AddBookStackButton", "Book Stack", new Vector2(16f, -132f), () => AddPlaceableFromInventory(RoomPlaceableType.BookStack), new Vector2(220f, 34f), new Vector2(0f, 1f));
            CreateButton(inventoryPanel.transform, "AddFloorLampButton", "Floor Lamp", new Vector2(16f, -172f), () => AddPlaceableFromInventory(RoomPlaceableType.FloorLamp), new Vector2(220f, 34f), new Vector2(0f, 1f));
            CreateButton(inventoryPanel.transform, "AddRugButton", "Natural Rug", new Vector2(16f, -212f), () => AddPlaceableFromInventory(RoomPlaceableType.NaturalRug), new Vector2(220f, 34f), new Vector2(0f, 1f));
            CreateButton(inventoryPanel.transform, "CloseInventoryButton", "Close", new Vector2(16f, -252f), ToggleInventoryPanel, new Vector2(220f, 34f), new Vector2(0f, 1f));
        }

        private void RefreshUi()
        {
            var isAvailable = IsRoomEditingAvailable();
            if (inventoryButton != null)
            {
                inventoryButton.gameObject.SetActive(isAvailable);
                SetButtonLabel(inventoryButton, isInventoryOpen ? "Close Inventory" : "Inventory");
                StyleButton(
                    inventoryButton,
                    isInventoryOpen ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                    isInventoryOpen ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    16,
                    isInventoryOpen ? Color.white : new Color32(45, 50, 58, 255),
                    true);
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isAvailable && isInventoryOpen);
            }

            if (editPanel != null)
            {
                editPanel.SetActive(isAvailable && !isInventoryOpen && selectedObject != null);
            }

            if (selectedNameText != null)
            {
                selectedNameText.text = selectedObject != null ? selectedObject.DisplayName : string.Empty;
            }

            if (editHintText != null)
            {
                editHintText.text = isMoveMode
                    ? "Drag the selected object to reposition it in the room."
                    : "Move, rotate, or delete the selected object.";
            }

            var hasSelection = selectedObject != null;
            if (moveButton != null)
            {
                moveButton.interactable = hasSelection;
                SetButtonLabel(moveButton, isMoveMode ? "Moving..." : "Move");
                StyleButton(
                    moveButton,
                    isMoveMode ? new Color32(173, 122, 79, 255) : new Color32(255, 255, 255, 235),
                    isMoveMode ? new Color32(151, 104, 64, 255) : new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    15,
                    isMoveMode ? Color.white : new Color32(45, 50, 58, 255),
                    isMoveMode);
            }

            if (rotateLeftButton != null)
            {
                rotateLeftButton.interactable = hasSelection;
                StyleButton(
                    rotateLeftButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    14,
                    new Color32(45, 50, 58, 255));
            }

            if (rotateRightButton != null)
            {
                rotateRightButton.interactable = hasSelection;
                StyleButton(
                    rotateRightButton,
                    new Color32(255, 255, 255, 235),
                    new Color32(241, 235, 230, 255),
                    new Color32(224, 219, 214, 255),
                    14,
                    new Color32(45, 50, 58, 255));
            }

            if (deleteButton != null)
            {
                deleteButton.interactable = hasSelection;
                StyleButton(
                    deleteButton,
                    new Color32(187, 88, 78, 255),
                    new Color32(169, 77, 68, 255),
                    new Color32(224, 219, 214, 255),
                    15,
                    Color.white,
                    true);
            }
        }

        private static bool TryGetPointerState(out PointerState pointerState)
        {
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                var press = touch.press;
                if (press.isPressed || press.wasPressedThisFrame || press.wasReleasedThisFrame)
                {
                    pointerState = new PointerState(
                        true,
                        touch.position.ReadValue(),
                        press.wasPressedThisFrame,
                        press.isPressed,
                        press.wasReleasedThisFrame,
                        touch.touchId.ReadValue());
                    return true;
                }
            }

            if (Mouse.current != null)
            {
                var mouse = Mouse.current;
                var button = mouse.leftButton;
                pointerState = new PointerState(
                    true,
                    mouse.position.ReadValue(),
                    button.wasPressedThisFrame,
                    button.isPressed,
                    button.wasReleasedThisFrame,
                    -1);
                return true;
            }

            pointerState = default;
            return false;
        }

        private static bool IsPointerOverUi(int pointerId)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            var text = labelObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(45, 50, 58, 255);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.resizeTextForBestFit = false;

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            StyleButton(
                button,
                new Color32(255, 255, 255, 235),
                new Color32(241, 235, 230, 255),
                new Color32(224, 219, 214, 255),
                15,
                new Color32(45, 50, 58, 255));

            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            var image = panelObject.AddComponent<Image>();
            StylePanel(image, new Color(0.08f, 0.10f, 0.13f, 0.82f));

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static Text CreateLabel(Transform parent, string name, string content, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, false);

            var text = labelObject.AddComponent<Text>();
            text.text = content;
            text.alignment = alignment;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        private static void StylePanel(Image image, Color background)
        {
            if (image == null)
            {
                return;
            }

            image.color = background;
            image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
        }

        private static void StyleButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            StylePanel(image, background);

            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = highlightedBackground;
            colors.selectedColor = highlightedBackground;
            colors.pressedColor = highlightedBackground;
            colors.disabledColor = disabledBackground;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var label = button.GetComponentInChildren<Text>();
            if (label == null)
            {
                return;
            }

            label.fontSize = fontSize;
            label.color = textColor;
            label.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
