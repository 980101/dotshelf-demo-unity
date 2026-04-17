using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    public enum ExperienceFocusArea
    {
        QuarterView,
        Bookshelf,
        Guestbook,
        Notebook
    }

    public class BookshelfExperienceManager : MonoBehaviour
    {
        [SerializeField] private BookshelfState state = null;
        [SerializeField] private CameraController cameraController = null;
        [SerializeField] private BookInteractionController interactionController = null;
        [SerializeField] private MaterialFactory materialFactory = null;
        [SerializeField] private BookshelfRuntimeDataStore dataStore = null;
        [SerializeField] private bool isEditMode;
        [SerializeField] private NotebookScreenController notebookScreenController = null;

        private Canvas hudCanvas;
        private Button editModeButton;
        private GameObject editModePanel;
        private Text editSelectionText;

        private GameObject guestbookPanel;
        private Text guestbookDetailText;
        private readonly Button[] guestbookMessageButtons = new Button[3];
        private int selectedGuestbookMessageIndex;

        private GameObject bookDetailPanel;
        private Text bookTitleText;
        private Text bookMetaText;
        private Button readToggleButton;
        private Button visibilityToggleButton;

        private readonly Button[] themeButtons = new Button[3];
        private GameObject hamburgerMenuPanel;
        private Button hamburgerButton;
        private bool isHamburgerMenuOpen;

        public ExperienceFocusArea CurrentFocusArea { get; private set; } = ExperienceFocusArea.QuarterView;
        public bool IsEditMode => isEditMode;
        public bool IsBookshelfFocused => CurrentFocusArea == ExperienceFocusArea.Bookshelf;
        public BookEntity SelectedEditBook { get; private set; }
        public BookEntity ActiveDetailBook { get; private set; }

        public void Configure(
            BookshelfState bookshelfState,
            CameraController controller,
            BookInteractionController interaction,
            MaterialFactory factory,
            BookshelfRuntimeDataStore runtimeDataStore)
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged -= HandleCameraModeChanged;
                cameraController.TransitionCompleted -= HandleCameraTransitionCompleted;
            }

            state = bookshelfState;
            cameraController = controller;
            interactionController = interaction;
            materialFactory = factory;
            dataStore = runtimeDataStore;

            if (isActiveAndEnabled && cameraController != null)
            {
                cameraController.ModeChanged += HandleCameraModeChanged;
                cameraController.TransitionCompleted += HandleCameraTransitionCompleted;
                SyncFromCameraMode(cameraController.CurrentMode);
            }
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureHud();
        }

        private void Start()
        {
            ConfigureNotebookScreen();
            RefreshHud();
        }

        private void Update()
        {
            if (HandleFocusShortcuts())
            {
                return;
            }

            if (cameraController != null && cameraController.IsTransitioning)
            {
                return;
            }

            if (Mouse.current == null || IsPointerOverUi())
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (TryHitNotebook())
            {
                FocusNotebook();
                return;
            }

            if (TryHitGuestbookBoard())
            {
                FocusGuestbook();
                return;
            }
        }

        private void OnEnable()
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged += HandleCameraModeChanged;
                cameraController.TransitionCompleted += HandleCameraTransitionCompleted;
                SyncFromCameraMode(cameraController.CurrentMode);
            }
        }

        private void OnDisable()
        {
            if (cameraController != null)
            {
                cameraController.ModeChanged -= HandleCameraModeChanged;
                cameraController.TransitionCompleted -= HandleCameraTransitionCompleted;
            }
        }

        public void FocusQuarterView()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Overview);
        }

        public void FocusBookshelf()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Frontal);
        }

        public void FocusGuestbook()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Whiteboard);
        }

        public void FocusNotebook()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            ConfigureNotebookScreen();
            if (CurrentFocusArea == ExperienceFocusArea.Notebook && notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(true);
                notebookScreenController.ActivateDashboard();
                return;
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.SetImmersiveMode(false);
                notebookScreenController.Show();
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.QueueDashboardActivation();
            }

            if (notebookScreenController != null && notebookScreenController.ScreenTransform != null)
            {
                cameraController.MoveToNotebookScreen(notebookScreenController.ScreenTransform);
                return;
            }

            cameraController.MoveToMode(CameraMode.Notebook);
        }

        public void CloseNotebookFocus()
        {
            if (!CanStartCameraFocus())
            {
                return;
            }

            cameraController.MoveToMode(CameraMode.Overview);
        }

        private bool HandleFocusShortcuts()
        {
            if (cameraController != null && cameraController.IsTransitioning)
            {
                return true;
            }

            if (Keyboard.current == null)
            {
                return false;
            }

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                FocusQuarterView();
                return true;
            }

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                FocusBookshelf();
                return true;
            }

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                FocusGuestbook();
                return true;
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                FocusNotebook();
                return true;
            }

            return false;
        }

        private bool CanStartCameraFocus()
        {
            return cameraController != null && !cameraController.IsTransitioning;
        }

        public void ToggleEditMode()
        {
            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                return;
            }

            isEditMode = !isEditMode;

            if (isEditMode)
            {
                CloseOverlayPanels();
                if (state != null && state.IsViewingBook && interactionController != null)
                {
                    interactionController.CloseViewedBook(true);
                }
            }
            else
            {
                ClearEditSelection();
            }

            RefreshHud();
        }

        public void SelectEditBook(BookEntity book)
        {
            if (!isEditMode)
            {
                return;
            }

            SelectedEditBook = book;
            RefreshHud();
        }

        public void ClearEditSelection()
        {
            SelectedEditBook = null;
            RefreshHud();
        }

        public void ApplyTheme(BookshelfThemeStyle theme)
        {
            if (materialFactory == null)
            {
                return;
            }

            materialFactory.ApplyTheme(theme);
            RefreshHud();
        }

        public void HandleBookViewed(BookEntity book)
        {
            ActiveDetailBook = book;
        }

        public void ToggleFocusedBookDetail(BookEntity book)
        {
            if (isEditMode)
            {
                return;
            }

            if (bookDetailPanel != null && bookDetailPanel.activeSelf && ActiveDetailBook == book)
            {
                CloseBookDetailPanel();
                return;
            }

            OpenBookDetailPanel(book);
        }

        public void HandleBookClosed()
        {
            ActiveDetailBook = null;
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }
        }

        public BookshelfRuntimeSnapshotData CreateRuntimeSnapshot()
        {
            return dataStore != null
                ? dataStore.CreateSnapshot(CurrentFocusArea, isEditMode, SelectedEditBook != null ? SelectedEditBook : ActiveDetailBook)
                : new BookshelfRuntimeSnapshotData();
        }

        private void OpenBookDetailPanel(BookEntity book)
        {
            if (book == null || dataStore == null)
            {
                return;
            }

            CloseOverlayPanels(bookDetailPanel);
            ActiveDetailBook = book;
            bookDetailPanel.SetActive(true);
            RefreshBookDetailPanel();
        }

        private void CloseBookDetailPanel()
        {
            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }
        }

        private void ToggleReadState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            meta.isRead = !meta.isRead;
            RefreshBookDetailPanel();
        }

        private void ToggleVisibilityState()
        {
            if (TryCloseBookDetailFromFocusedBookPointer())
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            meta.isPublic = !meta.isPublic;
            RefreshBookDetailPanel();
        }

        private bool TryCloseBookDetailFromFocusedBookPointer()
        {
            if (bookDetailPanel == null ||
                !bookDetailPanel.activeSelf ||
                ActiveDetailBook == null ||
                Mouse.current == null ||
                Camera.main == null)
            {
                return false;
            }

            var pointer = Mouse.current.position.ReadValue();
            var ray = Camera.main.ScreenPointToRay(pointer);
            var hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var hitArea = hits[i].collider.GetComponent<BookInteractionHitArea>();
                if (hitArea != null)
                {
                    if (hitArea.Owner == ActiveDetailBook)
                    {
                        CloseBookDetailPanel();
                        return true;
                    }

                    continue;
                }

                var book = hits[i].collider.GetComponentInParent<BookEntity>();
                if (book == null)
                {
                    continue;
                }

                if (book == ActiveDetailBook)
                {
                    CloseBookDetailPanel();
                    return true;
                }

                return false;
            }

            return false;
        }

        public void OpenGuestbookPanel()
        {
            CloseOverlayPanels(guestbookPanel);
            guestbookPanel.SetActive(true);
            RefreshGuestbookPanel();
        }

        public void CloseGuestbookPanel()
        {
            if (guestbookPanel != null)
            {
                guestbookPanel.SetActive(false);
            }
        }

        private void HandleCameraModeChanged(CameraMode mode)
        {
            if (mode != CameraMode.Frontal &&
                state != null &&
                state.IsViewingBook &&
                interactionController != null)
            {
                interactionController.CloseViewedBook(true);
            }

            SyncFromCameraMode(mode);
        }

        private void HandleCameraTransitionCompleted(CameraMode mode)
        {
            if (mode == CameraMode.Notebook && CurrentFocusArea == ExperienceFocusArea.Notebook)
            {
                if (notebookScreenController != null)
                {
                    notebookScreenController.EnterImmersiveDashboard();
                    return;
                }

                ShowNotebookScreen();
            }
        }

        private void SyncFromCameraMode(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Frontal:
                    CurrentFocusArea = ExperienceFocusArea.Bookshelf;
                    break;
                case CameraMode.Whiteboard:
                    CurrentFocusArea = ExperienceFocusArea.Guestbook;
                    break;
                case CameraMode.Notebook:
                    CurrentFocusArea = ExperienceFocusArea.Notebook;
                    break;
                default:
                    CurrentFocusArea = ExperienceFocusArea.QuarterView;
                    break;
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf && isEditMode)
            {
                isEditMode = false;
            }

            if (CurrentFocusArea != ExperienceFocusArea.Bookshelf)
            {
                SelectedEditBook = null;
            }

            if (CurrentFocusArea == ExperienceFocusArea.Notebook)
            {
                CloseOverlayPanels();
                isHamburgerMenuOpen = false;
            }
            else
            {
                if (notebookScreenController != null)
                {
                    notebookScreenController.SetImmersiveMode(false);
                }

                ShowNotebookScreen();
            }

            RefreshHud();
        }

        private void EnsureHud()
        {
            var canvasObject = new GameObject("ExperienceHud");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            hamburgerButton = CreateButton(canvasObject.transform, "HamburgerButton", "Menu", new Vector2(-20f, -20f), ToggleHamburgerMenu, new Vector2(72f, 44f), new Vector2(1f, 1f));
            hamburgerButton.GetComponentInChildren<Text>().fontSize = 26;

            hamburgerMenuPanel = CreatePanel(canvasObject.transform, "HamburgerMenuPanel", new Vector2(-20f, -74f), new Vector2(190f, 204f), new Vector2(1f, 1f));
            CreateButton(hamburgerMenuPanel.transform, "QuarterViewButton", "Quarter View", new Vector2(-10f, -10f), () => RunMenuAction(FocusQuarterView));
            CreateButton(hamburgerMenuPanel.transform, "BookshelfButton", "Bookshelf", new Vector2(-10f, -58f), () => RunMenuAction(FocusBookshelf));
            CreateButton(hamburgerMenuPanel.transform, "GuestbookButton", "Guestbook", new Vector2(-10f, -106f), () => RunMenuAction(FocusGuestbook));
            editModeButton = CreateButton(hamburgerMenuPanel.transform, "EditModeButton", "Edit Mode: Off", new Vector2(-10f, -154f), () => RunMenuAction(ToggleEditMode));

            editModePanel = CreatePanel(canvasObject.transform, "EditModePanel", new Vector2(-20f, -236f), new Vector2(260f, 210f), new Vector2(1f, 1f));
            editSelectionText = CreateLabel(editModePanel.transform, "EditSelectionText", new Vector2(12f, -12f), new Vector2(236f, 48f), TextAnchor.UpperLeft);
            editSelectionText.fontSize = 16;
            themeButtons[0] = CreateButton(editModePanel.transform, "ThemeDefaultButton", "Theme: Default", new Vector2(-12f, -72f), () => ApplyTheme(BookshelfThemeStyle.Default));
            themeButtons[1] = CreateButton(editModePanel.transform, "ThemeWarmButton", "Theme: Warm", new Vector2(-12f, -124f), () => ApplyTheme(BookshelfThemeStyle.Warm));
            themeButtons[2] = CreateButton(editModePanel.transform, "ThemeDarkButton", "Theme: Dark", new Vector2(-12f, -176f), () => ApplyTheme(BookshelfThemeStyle.Dark));

            bookDetailPanel = CreatePanel(canvasObject.transform, "BookDetailPanel", new Vector2(20f, -20f), new Vector2(360f, 250f), new Vector2(0f, 1f));
            bookDetailPanel.SetActive(false);
            bookTitleText = CreateLabel(bookDetailPanel.transform, "BookTitleText", new Vector2(16f, -16f), new Vector2(320f, 28f), TextAnchor.UpperLeft);
            bookTitleText.fontSize = 20;
            bookMetaText = CreateLabel(bookDetailPanel.transform, "BookMetaText", new Vector2(16f, -56f), new Vector2(320f, 96f), TextAnchor.UpperLeft);
            bookMetaText.fontSize = 15;
            readToggleButton = CreateButton(bookDetailPanel.transform, "ReadToggleButton", "Read", new Vector2(-16f, -166f), ToggleReadState);
            visibilityToggleButton = CreateButton(bookDetailPanel.transform, "VisibilityToggleButton", "Visibility", new Vector2(-16f, -214f), ToggleVisibilityState);
            CreateButton(bookDetailPanel.transform, "CloseBookDetailButton", "Close", new Vector2(-196f, -214f), CloseBookDetailPanel, new Vector2(120f, 36f), new Vector2(1f, 1f));

            guestbookPanel = CreatePanel(canvasObject.transform, "GuestbookPanel", new Vector2(20f, -20f), new Vector2(360f, 250f), new Vector2(0f, 1f));
            guestbookPanel.SetActive(false);
            CreateButton(guestbookPanel.transform, "CloseGuestbookButton", "Close", new Vector2(-12f, -12f), CloseGuestbookPanel);
            guestbookMessageButtons[0] = CreateGuestbookMessageButton(guestbookPanel.transform, "GuestbookMessageA", 0, new Vector2(12f, -60f));
            guestbookMessageButtons[1] = CreateGuestbookMessageButton(guestbookPanel.transform, "GuestbookMessageB", 1, new Vector2(12f, -108f));
            guestbookMessageButtons[2] = CreateGuestbookMessageButton(guestbookPanel.transform, "GuestbookMessageC", 2, new Vector2(12f, -156f));
            guestbookDetailText = CreateLabel(guestbookPanel.transform, "GuestbookDetailText", new Vector2(12f, -204f), new Vector2(330f, 64f), TextAnchor.UpperLeft);
            guestbookDetailText.fontSize = 15;

            RefreshHud();
        }

        private void ToggleHamburgerMenu()
        {
            isHamburgerMenuOpen = !isHamburgerMenuOpen;
            RefreshHud();
        }

        private void RunMenuAction(UnityEngine.Events.UnityAction action)
        {
            action?.Invoke();
            isHamburgerMenuOpen = false;
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (hamburgerMenuPanel != null)
            {
                hamburgerMenuPanel.SetActive(isHamburgerMenuOpen);
            }

            if (hamburgerButton != null)
            {
                hamburgerButton.gameObject.SetActive(CurrentFocusArea != ExperienceFocusArea.Notebook);
            }

            if (editModeButton != null)
            {
                editModeButton.GetComponentInChildren<Text>().text = $"Edit Mode: {(isEditMode ? "On" : "Off")}";
                editModeButton.interactable = CurrentFocusArea == ExperienceFocusArea.Bookshelf;
            }

            if (editModePanel != null)
            {
                editModePanel.SetActive(isEditMode && CurrentFocusArea == ExperienceFocusArea.Bookshelf);
            }

            if (editSelectionText != null)
            {
                editSelectionText.text = SelectedEditBook == null
                    ? "Edit Mode\nSelect a book, then drag it or click a highlighted slot."
                    : $"Edit Mode\nSelected: {SelectedEditBook.bookId}";
            }

            RefreshThemeButtons();
            RefreshGuestbookPanel();
            RefreshBookDetailPanel();
        }

        private void RefreshBookDetailPanel()
        {
            if (bookDetailPanel == null || !bookDetailPanel.activeSelf || ActiveDetailBook == null || dataStore == null)
            {
                return;
            }

            var meta = dataStore.GetOrCreateBookMeta(ActiveDetailBook);
            if (meta == null)
            {
                return;
            }

            bookTitleText.text = meta.title;
            bookMetaText.text =
                $"Status: {(meta.isRead ? "Read" : "Unread")}\n" +
                $"Visibility: {(meta.isPublic ? "Public" : "Private")}\n\n" +
                $"Memo\n{meta.memoPreview}";

            readToggleButton.GetComponentInChildren<Text>().text = meta.isRead ? "Mark Unread" : "Mark Read";
            visibilityToggleButton.GetComponentInChildren<Text>().text = meta.isPublic ? "Set Private" : "Set Public";
        }

        private Button CreateGuestbookMessageButton(Transform parent, string name, int index, Vector2 anchoredPosition)
        {
            return CreateButton(parent, name, $"Message {index + 1}", anchoredPosition, () => SelectGuestbookMessage(index), new Vector2(150f, 34f), new Vector2(0f, 1f));
        }

        private void SelectGuestbookMessage(int index)
        {
            selectedGuestbookMessageIndex = index;
            RefreshGuestbookPanel();
        }

        private void RefreshGuestbookPanel()
        {
            if (guestbookPanel == null || dataStore == null)
            {
                return;
            }

            var entries = dataStore.GetGuestbookEntries();
            if (entries.Count == 0)
            {
                return;
            }

            selectedGuestbookMessageIndex = Mathf.Clamp(selectedGuestbookMessageIndex, 0, entries.Count - 1);
            var selectedEntry = entries[selectedGuestbookMessageIndex];
            guestbookDetailText.text = $"{selectedEntry.authorName} • {selectedEntry.timestampLabel}\n{selectedEntry.message}";

            for (var i = 0; i < guestbookMessageButtons.Length; i++)
            {
                if (guestbookMessageButtons[i] == null || i >= entries.Count)
                {
                    continue;
                }

                guestbookMessageButtons[i].GetComponentInChildren<Text>().text = $"{entries[i].authorName}";
                var isSelected = i == selectedGuestbookMessageIndex;
                var image = guestbookMessageButtons[i].GetComponent<Image>();
                image.color = isSelected
                    ? new Color(0.78f, 0.55f, 0.28f, 0.92f)
                    : new Color(0.11f, 0.12f, 0.14f, 0.86f);
            }
        }

        private void RefreshThemeButtons()
        {
            if (materialFactory == null)
            {
                return;
            }

            UpdateThemeButton(themeButtons[0], materialFactory.CurrentTheme == BookshelfThemeStyle.Default);
            UpdateThemeButton(themeButtons[1], materialFactory.CurrentTheme == BookshelfThemeStyle.Warm);
            UpdateThemeButton(themeButtons[2], materialFactory.CurrentTheme == BookshelfThemeStyle.Dark);
        }

        private void CloseOverlayPanels(GameObject except = null)
        {
            if (bookDetailPanel != null && bookDetailPanel != except)
            {
                bookDetailPanel.SetActive(false);
            }

            if (guestbookPanel != null && guestbookPanel != except)
            {
                guestbookPanel.SetActive(false);
            }

        }

        private static void UpdateThemeButton(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.GetComponent<Image>().color = isSelected
                ? new Color(0.72f, 0.52f, 0.24f, 0.92f)
                : new Color(0.11f, 0.12f, 0.14f, 0.86f);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(parent, name, label, anchoredPosition, onClick, new Vector2(170f, 40f), new Vector2(1f, 1f));
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.14f, 0.86f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var labelText = CreateLabel(buttonObject.transform, "Label", Vector2.zero, size, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.fontSize = 18;

            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return panelObject;
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = alignment;
            text.text = string.Empty;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.one;
            rect.pivot = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            return text;
        }

        private bool TryHitGuestbookBoard()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return TryHitClosest<GuestbookBoardHitArea>(ray);
        }

        private bool TryHitNotebook()
        {
            var sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return TryHitClosest<NotebookHitArea>(ray);
        }

        private void ConfigureNotebookScreen()
        {
            if (notebookScreenController == null)
            {
                notebookScreenController = FindFirstObjectByType<NotebookScreenController>();
            }

            if (notebookScreenController == null || Camera.main == null)
            {
                return;
            }

            var screenTransform = notebookScreenController.transform.Find("Screen");
            notebookScreenController.Configure(screenTransform, Camera.main, () => notebookScreenController.ReturnToScreenSaver());
        }

        private void ShowNotebookScreen()
        {
            if (notebookScreenController == null)
            {
                ConfigureNotebookScreen();
            }

            if (notebookScreenController != null)
            {
                notebookScreenController.Show();
            }
        }

        private void HideNotebookScreen()
        {
            if (notebookScreenController != null)
            {
                notebookScreenController.Hide();
            }
        }

        private static bool TryHitClosest<T>(Ray ray) where T : Component
        {
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits.Length == 0)
            {
                return false;
            }

            var closestDistance = float.MaxValue;
            Collider closestCollider = null;
            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hits[i].distance;
                closestCollider = hits[i].collider;
            }

            return closestCollider != null && closestCollider.GetComponent<T>() != null;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
    }
}
