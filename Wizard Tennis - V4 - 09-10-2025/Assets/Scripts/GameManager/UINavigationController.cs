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

    private Canvas currentCanvas;

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
            // If user presses keyboard, switch to keyboard mode
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

                // Ensure indicator has a canvas parent or add one
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
        // Detect mouse movement
        if (Input.mousePosition != lastMousePosition)
        {
            lastMousePosition = Input.mousePosition;

            // Switch to mouse mode if mouse moved
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

        // Quick check if number of active selectables changed
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

            // Prefer canvases with more selectables or higher sort order
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

            // Sort selectables by Y position (top to bottom), then X (left to right)
            currentSelectables = bestSelectables.OrderByDescending(s => s.transform.position.y)
                                                .ThenBy(s => s.transform.position.x)
                                                .ToList();

            lastSelectableCount = currentSelectables.Count;

            // Try to maintain selection if possible
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
        if (!includeInactive && !s.gameObject.activeInHierarchy) return false;
        if (!s.interactable) return false;
        if (!s.IsActive()) return false;

        // Check if any parent is disabled
        Transform current = s.transform;
        while (current != null)
        {
            if (!current.gameObject.activeInHierarchy)
                return false;

            CanvasGroup cg = current.GetComponent<CanvasGroup>();
            if (cg != null && (!cg.interactable || cg.alpha == 0))
                return false;

            current = current.parent;
        }

        return true;
    }

    private void MoveSelection(int direction)
    {
        if (currentSelectables.Count == 0) return;

        if (currentIndex >= 0)
        {
            DeselectElement(currentSelectables[currentIndex]);
        }

        currentIndex += direction;
        if (currentIndex < 0) currentIndex = currentSelectables.Count - 1;
        if (currentIndex >= currentSelectables.Count) currentIndex = 0;

        SelectElement(currentIndex);
    }

    private void SelectElement(int index)
    {
        if (index < 0 || index >= currentSelectables.Count) return;

        // Verify the element is still valid
        if (!IsSelectableValid(currentSelectables[index]))
        {
            UpdateActivePanel();
            return;
        }

        currentIndex = index;
        Selectable selectable = currentSelectables[currentIndex];

        // Set EventSystem selection
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);

        // Trigger hover effect
        TriggerPointerEnter(selectable);

        // Position indicator
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

        // Handle Slider
        if (current is Slider slider)
        {
            float step = (slider.maxValue - slider.minValue) * 0.05f;
            slider.value = Mathf.Clamp(slider.value + direction * step, slider.minValue, slider.maxValue);
            return;
        }

        // Handle Scrollbar
        if (current is Scrollbar scrollbar)
        {
            scrollbar.value = Mathf.Clamp01(scrollbar.value + direction * 0.1f);
            return;
        }

        // Handle TMP_Dropdown
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

        // Handle standard Dropdown
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

        // Handle Toggle
        if (current is Toggle toggle)
        {
            toggle.isOn = !toggle.isOn;
            return;
        }

        // Handle InputField
        if (current is TMP_InputField || current is InputField)
        {
            // For input fields, left/right doesn't navigate, it's for text editing
            return;
        }

        // For other elements, left/right does horizontal navigation
        MoveSelection(direction);
    }

    private void ConfirmSelection()
    {
        if (currentIndex < 0 || currentIndex >= currentSelectables.Count) return;

        Selectable selectable = currentSelectables[currentIndex];

        // Trigger press visual
        TriggerPointerDown(selectable);

        // Handle different types
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
            tmpDropdown.Show();
        }
        else if (selectable is Dropdown dropdown)
        {
            dropdown.Show();
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

        // Trigger release
        StartCoroutine(DelayedPointerUp(selectable));
    }

    private void HandleCancel()
    {
        // Handle dropdown closing
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

        RectTransform selectableRect = selectable.GetComponent<RectTransform>();
        if (selectableRect == null) return;

        // Get the canvas for this indicator
        Canvas indicatorCanvas = indicatorRect.GetComponentInParent<Canvas>();
        if (indicatorCanvas == null)
        {
            Debug.LogWarning("[UINavigation] Indicator must be child of a Canvas!");
            return;
        }

        // Get world corners of the selectable
        Vector3[] corners = new Vector3[4];
        selectableRect.GetWorldCorners(corners);

        Vector3 worldAnchorPosition = Vector3.zero;

        switch (indicatorPosition)
        {
            case IndicatorPosition.Left:
                worldAnchorPosition = (corners[0] + corners[1]) / 2f;
                break;
            case IndicatorPosition.Right:
                worldAnchorPosition = (corners[2] + corners[3]) / 2f;
                break;
            case IndicatorPosition.Top:
                worldAnchorPosition = (corners[1] + corners[2]) / 2f;
                break;
            case IndicatorPosition.Bottom:
                worldAnchorPosition = (corners[0] + corners[3]) / 2f;
                break;
            case IndicatorPosition.Center:
                worldAnchorPosition = (corners[0] + corners[2]) / 2f;
                break;
        }

        // Convert to canvas space
        Camera cam = indicatorCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : indicatorCanvas.worldCamera;

        Vector2 canvasPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            indicatorCanvas.transform as RectTransform,
            RectTransformUtility.WorldToScreenPoint(cam, worldAnchorPosition),
            cam,
            out canvasPosition
        );

        // Apply offset
        canvasPosition += indicatorOffset;

        // Set indicator position
        indicatorRect.anchoredPosition = canvasPosition;

        if (debugLog)
            Debug.Log($"[UINavigation] Positioned indicator at {indicatorPosition} of {selectable.name}, offset: {indicatorOffset}");
    }

    private void AnimateIndicator()
    {
        if (indicatorRect == null) return;

        animationTimer += Time.unscaledDeltaTime * animationSpeed;

        // Pulse animation
        float scale = 1f + Mathf.Sin(animationTimer) * 0.1f;
        indicatorRect.localScale = indicatorBaseScale * scale;

        // Horizontal bounce based on position
        float bounceOffset = Mathf.Sin(animationTimer * 2f) * 3f;

        Vector2 currentPos = indicatorRect.anchoredPosition;
        Vector2 basePos = currentPos;

        // Remove previous bounce
        if (indicatorPosition == IndicatorPosition.Left || indicatorPosition == IndicatorPosition.Right)
        {
            basePos.x = currentPos.x - bounceOffset;
            currentPos.x = basePos.x + bounceOffset;
        }
        else if (indicatorPosition == IndicatorPosition.Top || indicatorPosition == IndicatorPosition.Bottom)
        {
            basePos.y = currentPos.y - bounceOffset;
            currentPos.y = basePos.y + bounceOffset;
        }

        indicatorRect.anchoredPosition = currentPos;
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

    /// <summary>
    /// Force refresh the navigation system
    /// </summary>
    public void RefreshNavigation()
    {
        UpdateActivePanel();
    }

    /// <summary>
    /// Manually select a specific UI element
    /// </summary>
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

    /// <summary>
    /// Get currently selected element
    /// </summary>
    public Selectable GetCurrentSelection()
    {
        if (currentIndex >= 0 && currentIndex < currentSelectables.Count)
            return currentSelectables[currentIndex];
        return null;
    }

    /// <summary>
    /// Enable or disable keyboard navigation
    /// </summary>
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