using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class UINavigationController : MonoBehaviour
{
    [Header("Input Settings")]
    public KeyCode upKey = KeyCode.UpArrow;
    public KeyCode downKey = KeyCode.DownArrow;
    public KeyCode leftKey = KeyCode.LeftArrow;
    public KeyCode rightKey = KeyCode.RightArrow;
    public KeyCode confirmKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Selection Indicator")]
    [Tooltip("Indicator object that appears next to selected element")]
    public GameObject selectionIndicator;

    [Tooltip("Where to position the indicator relative to the UI element")]
    public IndicatorPosition indicatorPosition = IndicatorPosition.Right;

    [Tooltip("Offset from the anchor position")]
    public Vector2 indicatorOffset = new Vector2(10f, 0f);

    [Tooltip("Should the indicator animate?")]
    public bool animateIndicator = true;

    [Tooltip("Animation speed (bounce/pulse)")]
    public float animationSpeed = 2f;

    public enum IndicatorPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Center
    }

    [Header("Manual Exclusions")]
    [Tooltip("Selectables that should never be included in keyboard navigation")]
    public List<Selectable> manuallyExcludedSelectables = new List<Selectable>();

    [Tooltip("GameObjects that should never be included in navigation")]
    public List<GameObject> excludedObjects = new List<GameObject>();

    [Header("Visual Feedback")]
    [Tooltip("Keep selected UI visually highlighted")]
    public bool maintainSelectionHighlight = true;

    [Header("Detection Settings")]
    [Tooltip("Include inactive selectables in search")]
    public bool includeInactive = false;

    [Tooltip("Auto-refresh when UI changes")]
    public bool autoRefresh = true;

    [Tooltip("Refresh check interval (seconds)")]
    public float refreshInterval = 0.5f;

    [Header("Dropdown Settings")]
    [Tooltip("Automatically navigate dropdown items when dropdown is open")]
    public bool navigateDropdownItems = true;

    [Tooltip("Close dropdown when navigating away")]
    public bool closeDropdownOnNavigateAway = true;

    [Header("Debug")]
    public bool debugLog = false;

    private List<Selectable> currentSelectables = new List<Selectable>();
    private int currentIndex = -1;
    private GameObject currentPanel;
    private bool selectionActivated = false;
    private bool isMouseMode = false;
    private Vector3 lastMousePosition;

    private RectTransform indicatorRect;
    private Vector3 indicatorBaseScale;
    private float animationTimer = 0f;
    private float lastRefreshTime = 0f;
    private int lastSelectableCount = 0;
    private Vector2 storedBasePosition;

    private Canvas currentCanvas;

    // Dropdown navigation state
    private bool dropdownOpen = false;
    private List<Selectable> dropdownOptions = new List<Selectable>();
    private int dropdownIndex = -1;
    private Selectable dropdownOwner;
    private ScrollRect dropdownScrollRect;

    private void Awake()
    {
        SetupIndicator();
        lastMousePosition = Input.mousePosition;
    }

    private void OnEnable()
    {
        selectionActivated = false;
        isMouseMode = false;
        HideIndicator();
        UpdateActivePanel();
    }

    private void Update()
    {
        DetectInputMode();

        if (dropdownOpen && navigateDropdownItems)
        {
            HandleDropdownNavigation();
            return;
        }

        // Auto-refresh check
        if (autoRefresh && Time.unscaledTime - lastRefreshTime > refreshInterval)
        {
            CheckForUIChanges();
            lastRefreshTime = Time.unscaledTime;
        }

        if (currentPanel == null || !currentPanel.activeInHierarchy)
        {
            UpdateActivePanel();
            return;
        }

        if (currentSelectables.Count == 0) return;

        // Mouse mode: Don't navigate, just allow mouse hover
        if (isMouseMode)
        {
            if (Input.GetKeyDown(upKey) || Input.GetKeyDown(downKey) ||
                Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey) ||
                Input.GetKeyDown(confirmKey))
            {
                isMouseMode = false;
                selectionActivated = true;
                SelectElement(0);
            }
            return;
        }

        // Keyboard mode navigation
        bool up = Input.GetKeyDown(upKey);
        bool down = Input.GetKeyDown(downKey);
        bool left = Input.GetKeyDown(leftKey);
        bool right = Input.GetKeyDown(rightKey);
        bool confirm = Input.GetKeyDown(confirmKey);
        bool cancel = Input.GetKeyDown(cancelKey);

        // First input activates selection
        if (!selectionActivated)
        {
            if (up || down || left || right || confirm)
            {
                selectionActivated = true;
                SelectElement(0);
            }
            return;
        }

        // Normal navigation
        if (up) MoveSelection(-1);
        else if (down) MoveSelection(1);
        else if (left) HandleLeftRight(-1);
        else if (right) HandleLeftRight(1);
        else if (confirm) ConfirmSelection();
        else if (cancel) HandleCancel();

        // Animate indicator
        if (animateIndicator && selectionIndicator != null && selectionIndicator.activeSelf)
        {
            AnimateIndicator();
        }
    }

    private void SetupIndicator()
    {
        if (selectionIndicator != null)
        {
            indicatorRect = selectionIndicator.GetComponent<RectTransform>();
            if (indicatorRect != null)
            {
                indicatorBaseScale = indicatorRect.localScale;

                if (indicatorRect.GetComponentInParent<Canvas>() == null)
                {
                    Debug.LogWarning("[UINavigation] Selection indicator must be a child of a Canvas!");
                }
            }
            selectionIndicator.SetActive(false);
        }
    }

    private void DetectInputMode()
    {
        if (Input.mousePosition != lastMousePosition)
        {
            lastMousePosition = Input.mousePosition;

            if (!isMouseMode && selectionActivated)
            {
                isMouseMode = true;
                HideIndicator();
                ClearKeyboardSelection();

                if (debugLog)
                    Debug.Log("[UINavigation] Switched to mouse mode");
            }
        }
    }

    private void CheckForUIChanges()
    {
        if (currentPanel == null) return;

        Selectable[] allSelectables = currentPanel.GetComponentsInChildren<Selectable>(includeInactive);
        int activeCount = 0;

        foreach (Selectable s in allSelectables)
        {
            if (IsSelectableValid(s))
            {
                activeCount++;
            }
        }

        if (activeCount != lastSelectableCount)
        {
            if (debugLog)
                Debug.Log($"[UINavigation] UI changed: {lastSelectableCount} -> {activeCount} elements");

            UpdateActivePanel();
        }
    }

    private void UpdateActivePanel()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        Canvas bestCanvas = null;
        List<Selectable> bestSelectables = new List<Selectable>();

        foreach (Canvas canvas in canvases)
        {
            if (!canvas.gameObject.activeInHierarchy) continue;

            List<Selectable> activeSelectables = new List<Selectable>();
            Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(includeInactive);

            foreach (Selectable s in selectables)
            {
                if (IsSelectableValid(s))
                {
                    activeSelectables.Add(s);
                }
            }

            if (activeSelectables.Count > bestSelectables.Count ||
                (activeSelectables.Count == bestSelectables.Count &&
                 bestCanvas != null && canvas.sortingOrder > bestCanvas.sortingOrder))
            {
                bestCanvas = canvas;
                bestSelectables = activeSelectables;
            }
        }

        if (bestSelectables.Count > 0)
        {
            currentPanel = bestCanvas.gameObject;
            currentCanvas = bestCanvas;

            currentSelectables = bestSelectables
                .OrderByDescending(s => s.transform.position.y)
                .ThenBy(s => s.transform.position.x)
                .ToList();

            lastSelectableCount = currentSelectables.Count;

            if (currentIndex >= currentSelectables.Count)
            {
                currentIndex = -1;
            }

            if (!isMouseMode && selectionActivated && currentIndex >= 0)
            {
                SelectElement(currentIndex);
            }
            else if (!isMouseMode)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            if (debugLog)
                Debug.Log($"[UINavigation] Found {currentSelectables.Count} interactive elements on {currentPanel.name}");
        }
        else
        {
            currentPanel = null;
            currentCanvas = null;
            currentSelectables.Clear();
            currentIndex = -1;
            lastSelectableCount = 0;
            HideIndicator();
        }
    }

    private bool IsSelectableValid(Selectable s)
    {
        if (s == null) return false;

        // Manual exclusion list
        if (manuallyExcludedSelectables != null && manuallyExcludedSelectables.Contains(s))
            return false;

        // Per-element exclusion via marker
        UIIndicatorMarker marker = s.GetComponent<UIIndicatorMarker>();
        if (marker != null && marker.excludeFromNavigation)
            return false;

        // GameObject-level exclusions
        if (excludedObjects != null)
        {
            foreach (GameObject go in excludedObjects)
            {
                if (go == null) continue;
                if (s.gameObject == go || s.transform.IsChildOf(go.transform))
                    return false;
            }
        }

        // Exclude dropdown blockers and template objects
        if (s.name.Contains("Blocker") || s.name.Contains("Template"))
            return false;

        if (!s.IsActive() || !s.interactable) return false;

        RectTransform rect = s.GetComponent<RectTransform>();
        if (rect == null) return false;

        // Ignore dropdown items during normal navigation
        if (!dropdownOpen && s is Toggle && s.GetComponentInParent<TMP_Dropdown>() != null)
            return false;

        if (!dropdownOpen && s is Toggle && s.GetComponentInParent<Dropdown>() != null)
            return false;

        // Reject zero-size elements
        if (rect.rect.width <= 1f || rect.rect.height <= 1f)
            return false;

        // CanvasGroup checks
        CanvasGroup[] groups = s.GetComponentsInParent<CanvasGroup>(true);
        foreach (var cg in groups)
        {
            if (!cg.interactable || cg.alpha <= 0f)
                return false;
        }

        return true;
    }

    private void MoveSelection(int direction)
    {
        if (currentSelectables.Count == 0) return;

        // Close dropdown if navigating away
        if (closeDropdownOnNavigateAway && dropdownOpen)
        {
            CloseDropdown();
        }

        if (currentIndex >= 0)
        {
            DeselectElement(currentSelectables[currentIndex]);
        }

        currentIndex += direction;
        if (currentIndex < 0) currentIndex = currentSelectables.Count - 1;
        if (currentIndex >= currentSelectables.Count) currentIndex = 0;

        SelectElement(currentIndex);
    }

    private void HandleDropdownNavigation()
    {
        bool up = Input.GetKeyDown(upKey);
        bool down = Input.GetKeyDown(downKey);
        bool confirm = Input.GetKeyDown(confirmKey);
        bool cancel = Input.GetKeyDown(cancelKey);

        if (up) MoveDropdown(-1);
        else if (down) MoveDropdown(1);
        else if (confirm) ConfirmDropdown();
        else if (cancel) CloseDropdown();
    }

    private void MoveDropdown(int direction)
    {
        dropdownIndex += direction;

        if (dropdownIndex < 0) dropdownIndex = dropdownOptions.Count - 1;
        if (dropdownIndex >= dropdownOptions.Count) dropdownIndex = 0;

        SelectDropdownOption(dropdownIndex);
    }

    private void SelectDropdownOption(int index)
    {
        if (index < 0 || index >= dropdownOptions.Count) return;

        Selectable option = dropdownOptions[index];
        EventSystem.current.SetSelectedGameObject(option.gameObject);

        PositionIndicator(option);
        ShowIndicator();

        // Scroll to the selected option
        ScrollToDropdownOption(index);
    }

    private void ConfirmDropdown()
    {
        if (dropdownIndex < 0 || dropdownIndex >= dropdownOptions.Count) return;

        Toggle toggle = dropdownOptions[dropdownIndex] as Toggle;
        if (toggle != null)
            toggle.isOn = true;

        CloseDropdown();
    }

    private void CloseDropdown()
    {
        if (dropdownOwner is TMP_Dropdown tmp)
            tmp.Hide();
        else if (dropdownOwner is Dropdown d)
            d.Hide();

        dropdownOpen = false;
        dropdownOptions.Clear();
        dropdownIndex = -1;
        dropdownScrollRect = null;

        // Restore selection to dropdown control
        int ownerIndex = currentSelectables.IndexOf(dropdownOwner);
        if (ownerIndex >= 0)
        {
            SelectElement(ownerIndex);
        }

        if (debugLog)
            Debug.Log("[UINavigation] Exited dropdown mode");
    }

    private void SelectElement(int index)
    {
        if (index < 0 || index >= currentSelectables.Count) return;

        if (!IsSelectableValid(currentSelectables[index]))
        {
            UpdateActivePanel();
            return;
        }

        currentIndex = index;
        Selectable selectable = currentSelectables[currentIndex];

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        TriggerPointerEnter(selectable);

        PositionIndicator(selectable);
        ShowIndicator();

        if (debugLog)
            Debug.Log($"[UINavigation] Selected: {selectable.gameObject.name} ({selectable.GetType().Name})");
    }

    private void DeselectElement(Selectable selectable)
    {
        if (!maintainSelectionHighlight)
        {
            TriggerPointerExit(selectable);
        }
    }

    private void ClearKeyboardSelection()
    {
        if (currentIndex >= 0 && currentIndex < currentSelectables.Count)
        {
            TriggerPointerExit(currentSelectables[currentIndex]);
        }
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void HandleLeftRight(int direction)
    {
        if (currentIndex < 0 || currentIndex >= currentSelectables.Count) return;

        Selectable current = currentSelectables[currentIndex];

        if (current is Slider slider)
        {
            float step = (slider.maxValue - slider.minValue) * 0.05f;
            slider.value = Mathf.Clamp(slider.value + direction * step, slider.minValue, slider.maxValue);
            return;
        }

        if (current is Scrollbar scrollbar)
        {
            scrollbar.value = Mathf.Clamp01(scrollbar.value + direction * 0.1f);
            return;
        }

        if (current is TMP_Dropdown tmpDropdown)
        {
            int newValue = tmpDropdown.value + direction;
            if (newValue >= 0 && newValue < tmpDropdown.options.Count)
            {
                tmpDropdown.value = newValue;
                tmpDropdown.RefreshShownValue();
            }
            return;
        }

        if (current is Dropdown dropdown)
        {
            int newValue = dropdown.value + direction;
            if (newValue >= 0 && newValue < dropdown.options.Count)
            {
                dropdown.value = newValue;
                dropdown.RefreshShownValue();
            }
            return;
        }

        if (current is Toggle toggle)
        {
            toggle.isOn = !toggle.isOn;
            return;
        }

        if (current is TMP_InputField || current is InputField)
        {
            return;
        }

        MoveSelection(direction);
    }

    private void ConfirmSelection()
    {
        if (currentIndex < 0 || currentIndex >= currentSelectables.Count) return;

        Selectable selectable = currentSelectables[currentIndex];

        TriggerPointerDown(selectable);

        if (selectable is Button button)
        {
            button.onClick.Invoke();
        }
        else if (selectable is Toggle toggle)
        {
            toggle.isOn = !toggle.isOn;
        }
        else if (selectable is TMP_Dropdown tmpDropdown)
        {
            OpenTMPDropdown(tmpDropdown);
        }
        else if (selectable is Dropdown dropdown)
        {
            OpenDropdown(dropdown);
        }
        else if (selectable is TMP_InputField tmpInputField)
        {
            tmpInputField.ActivateInputField();
        }
        else if (selectable is InputField inputField)
        {
            inputField.ActivateInputField();
        }

        if (debugLog)
            Debug.Log($"[UINavigation] Confirmed: {selectable.gameObject.name}");

        StartCoroutine(DelayedPointerUp(selectable));
    }

    private void HandleCancel()
    {
        if (currentIndex >= 0 && currentIndex < currentSelectables.Count)
        {
            Selectable current = currentSelectables[currentIndex];

            if (current is TMP_Dropdown tmpDropdown)
            {
                tmpDropdown.Hide();
            }
            else if (current is Dropdown dropdown)
            {
                dropdown.Hide();
            }
        }
    }

    private System.Collections.IEnumerator DelayedPointerUp(Selectable selectable)
    {
        yield return new WaitForSecondsRealtime(0.1f);
        TriggerPointerUp(selectable);
    }

    private void PositionIndicator(Selectable selectable)
    {
        if (selectionIndicator == null || indicatorRect == null) return;

        RectTransform targetRect = selectable.GetComponent<RectTransform>();
        if (targetRect == null) return;

        // Get the main canvas (not dropdown blocker!)
        Canvas mainCanvas = GetMainCanvas(selectable);
        if (mainCanvas == null)
        {
            if (debugLog)
                Debug.LogWarning("[UINavigation] Could not find main canvas");
            return;
        }

        // Parent to main canvas to prevent destruction with dropdown blockers
        if (indicatorRect.parent != mainCanvas.transform)
        {
            indicatorRect.SetParent(mainCanvas.transform, false);
        }

        indicatorRect.localScale = indicatorBaseScale;
        indicatorRect.localRotation = Quaternion.identity;

        Vector3 worldPos;

        UIIndicatorMarker marker = selectable.GetComponent<UIIndicatorMarker>();
        if (marker != null && marker.HasCustomPosition())
        {
            if (marker.indicatorPosition != null)
            {
                worldPos = marker.indicatorPosition.position;
                worldPos += (Vector3)marker.customOffset;
            }
            else if (marker.useThisTransform)
            {
                worldPos = targetRect.position + (Vector3)marker.customOffset;
            }
            else
            {
                worldPos = targetRect.position;
            }
        }
        else
        {
            Rect r = targetRect.rect;
            Vector2 localPos;

            switch (indicatorPosition)
            {
                case IndicatorPosition.Left:
                    localPos = new Vector2(r.xMin, r.center.y);
                    break;
                case IndicatorPosition.Right:
                    localPos = new Vector2(r.xMax, r.center.y);
                    break;
                case IndicatorPosition.Top:
                    localPos = new Vector2(r.center.x, r.yMax);
                    break;
                case IndicatorPosition.Bottom:
                    localPos = new Vector2(r.center.x, r.yMin);
                    break;
                default:
                    localPos = r.center;
                    break;
            }

            worldPos = targetRect.TransformPoint(localPos);
            worldPos += (Vector3)indicatorOffset;
        }

        // Convert to canvas local position
        RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
        Vector2 canvasLocalPos;

        Camera cam = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            RectTransformUtility.WorldToScreenPoint(cam, worldPos),
            cam,
            out canvasLocalPos
        );

        indicatorRect.anchoredPosition = canvasLocalPos;
        storedBasePosition = canvasLocalPos;
    }

    private Canvas GetMainCanvas(Selectable selectable)
    {
        Canvas[] canvases = selectable.GetComponentsInParent<Canvas>(true);

        foreach (Canvas canvas in canvases)
        {
            if (canvas.name.Contains("Blocker") || canvas.name.Contains("Dropdown List"))
                continue;

            if (canvas.GetComponentInParent<TMP_Dropdown>() != null ||
                canvas.GetComponentInParent<Dropdown>() != null)
                continue;

            return canvas;
        }

        if (currentCanvas != null)
            return currentCanvas;

        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas.isRootCanvas && !canvas.name.Contains("Blocker"))
                return canvas;
        }

        return null;
    }

    private void OpenTMPDropdown(TMP_Dropdown dropdown)
    {
        dropdown.Show();
        StartCoroutine(CaptureDropdownOptions(dropdown.gameObject));
        dropdownOwner = dropdown;
    }

    private void OpenDropdown(Dropdown dropdown)
    {
        dropdown.Show();
        StartCoroutine(CaptureDropdownOptions(dropdown.gameObject));
        dropdownOwner = dropdown;
    }

    private System.Collections.IEnumerator CaptureDropdownOptions(GameObject dropdownRoot)
    {
        yield return null;

        dropdownOptions.Clear();
        dropdownScrollRect = null;

        Canvas dropdownCanvas = dropdownRoot.GetComponentInChildren<Canvas>();
        if (dropdownCanvas == null) yield break;

        Selectable[] options = dropdownCanvas.GetComponentsInChildren<Selectable>(true);

        foreach (Selectable s in options)
        {
            if (s is Toggle)
                dropdownOptions.Add(s);
        }

        if (dropdownOptions.Count == 0) yield break;

        // Find the ScrollRect for scrolling
        if (dropdownOptions.Count > 0)
        {
            dropdownScrollRect = dropdownOptions[0].GetComponentInParent<ScrollRect>();
        }

        // Sort options by Y position (top to bottom)
        dropdownOptions = dropdownOptions
            .OrderByDescending(s => s.transform.position.y)
            .ToList();

        dropdownOpen = true;
        dropdownIndex = 0;

        SelectDropdownOption(dropdownIndex);

        if (debugLog)
            Debug.Log($"[UINavigation] Entered dropdown mode with {dropdownOptions.Count} options, ScrollRect: {(dropdownScrollRect != null ? "Found" : "None")}");
    }

    private void ScrollToDropdownOption(int index)
    {
        if (dropdownScrollRect == null || index < 0 || index >= dropdownOptions.Count)
            return;

        RectTransform content = dropdownScrollRect.content;
        RectTransform viewport = dropdownScrollRect.viewport;
        RectTransform itemRect = dropdownOptions[index].GetComponent<RectTransform>();

        if (content == null || viewport == null || itemRect == null)
            return;

        // Calculate the normalized position for this item
        // Get the position of the item relative to the content
        Canvas.ForceUpdateCanvases();

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        // Only scroll if content is larger than viewport
        if (contentHeight <= viewportHeight)
            return;

        // Get item's position in content
        Vector2 itemPosInContent = (Vector2)content.InverseTransformPoint(itemRect.position);
        float itemHeight = itemRect.rect.height;

        // Calculate normalized scroll position (0 = bottom, 1 = top for vertical scroll)
        float normalizedPosition = 1f - ((float)index / Mathf.Max(1, dropdownOptions.Count - 1));

        // Apply some smoothing - center the item in viewport if possible
        float itemCenter = -itemPosInContent.y;
        float scrollRange = contentHeight - viewportHeight;

        if (scrollRange > 0)
        {
            float targetScroll = (itemCenter - (viewportHeight / 2f)) / scrollRange;
            dropdownScrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - targetScroll);
        }

        if (debugLog)
            Debug.Log($"[UINavigation] Scrolled to dropdown option {index}, normalized pos: {dropdownScrollRect.verticalNormalizedPosition:F2}");
    }

    private void AnimateIndicator()
    {
        if (indicatorRect == null) return;

        animationTimer += Time.unscaledDeltaTime * animationSpeed;

        float scale = 1f + Mathf.Sin(animationTimer) * 0.1f;
        indicatorRect.localScale = indicatorBaseScale * scale;

        float bounceOffset = Mathf.Sin(animationTimer * 2f) * 3f;
        Vector2 animatedPosition = storedBasePosition;

        if (indicatorPosition == IndicatorPosition.Left || indicatorPosition == IndicatorPosition.Right)
        {
            animatedPosition.x += bounceOffset;
        }
        else if (indicatorPosition == IndicatorPosition.Top || indicatorPosition == IndicatorPosition.Bottom)
        {
            animatedPosition.y += bounceOffset;
        }

        indicatorRect.anchoredPosition = animatedPosition;
    }

    private void ShowIndicator()
    {
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(true);
            animationTimer = 0f;
        }
    }

    private void HideIndicator()
    {
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }
    }

    #region Event Triggers

    private void TriggerPointerEnter(Selectable selectable)
    {
        var pointerData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(selectable.gameObject, pointerData, ExecuteEvents.pointerEnterHandler);
    }

    private void TriggerPointerExit(Selectable selectable)
    {
        var pointerData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(selectable.gameObject, pointerData, ExecuteEvents.pointerExitHandler);
    }

    private void TriggerPointerDown(Selectable selectable)
    {
        var pointerData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(selectable.gameObject, pointerData, ExecuteEvents.pointerDownHandler);
    }

    private void TriggerPointerUp(Selectable selectable)
    {
        var pointerData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(selectable.gameObject, pointerData, ExecuteEvents.pointerUpHandler);
    }

    #endregion

    #region Public Methods

    public void RefreshNavigation()
    {
        UpdateActivePanel();
    }

    public void SelectElement(GameObject element)
    {
        Selectable selectable = element.GetComponent<Selectable>();
        if (selectable == null) return;

        int index = currentSelectables.IndexOf(selectable);
        if (index >= 0)
        {
            isMouseMode = false;
            selectionActivated = true;
            SelectElement(index);
        }
    }

    public Selectable GetCurrentSelection()
    {
        if (currentIndex >= 0 && currentIndex < currentSelectables.Count)
            return currentSelectables[currentIndex];
        return null;
    }

    public void SetNavigationEnabled(bool enabled)
    {
        this.enabled = enabled;
        if (!enabled)
        {
            HideIndicator();
            ClearKeyboardSelection();
        }
    }

    #endregion
}