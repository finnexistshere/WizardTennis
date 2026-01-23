using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class UINavigationController : MonoBehaviour
{
    [Header("Input Settings")]
    public KeyCode upKey = KeyCode.UpArrow;
    public KeyCode downKey = KeyCode.DownArrow;
    public KeyCode leftKey = KeyCode.LeftArrow;
    public KeyCode rightKey = KeyCode.RightArrow;
    public KeyCode confirmKey = KeyCode.E;

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
    [Tooltip("Search depth for finding UI elements (higher = more thorough)")]
    public int searchDepth = 10;

    [Tooltip("Include inactive selectables in search")]
    public bool includeInactive = false;

    [Header("Debug")]
    public bool debugLog = false;

    private List<Selectable> currentSelectables = new List<Selectable>();
    private int currentIndex = -1;
    private GameObject currentPanel;
    private bool selectionActivated = false;
    private bool isMouseMode = false;
    private float lastMouseMoveTime = -1f;
    private Vector3 lastMousePosition;

    private RectTransform indicatorRect;
    private Vector3 indicatorBaseScale;
    private float animationTimer = 0f;

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

                // Select first element
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
            lastMouseMoveTime = Time.unscaledTime;

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

    private void UpdateActivePanel()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.activeInHierarchy)
            {
                Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
                List<Selectable> activeSelectables = new List<Selectable>();

                foreach (Selectable s in selectables)
                {
                    if (s.gameObject.activeInHierarchy && s.interactable && s.IsActive())
                    {
                        activeSelectables.Add(s);
                    }
                }

                if (activeSelectables.Count > 0)
                {
                    currentPanel = canvas.gameObject;
                    currentSelectables = activeSelectables;
                    currentIndex = -1;

                    if (!isMouseMode)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }

                    if (debugLog)
                        Debug.Log($"[UINavigation] Found {activeSelectables.Count} interactive elements");

                    return;
                }
            }
        }

        currentPanel = null;
        currentSelectables.Clear();
        currentIndex = -1;
        HideIndicator();
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
            float step = (slider.maxValue - slider.minValue) * 0.05f; // 5% steps
            slider.value += direction * step;
            return;
        }

        // Handle Scrollbar
        if (current is Scrollbar scrollbar)
        {
            scrollbar.value += direction * 0.1f;
            return;
        }

        // Handle Dropdown
        if (current is TMP_Dropdown dropdown)
        {
            int newValue = dropdown.value + direction;
            if (newValue >= 0 && newValue < dropdown.options.Count)
            {
                dropdown.value = newValue;
            }
            return;
        }

        // Handle Toggle (left/right toggles it)
        if (current is Toggle toggle)
        {
            toggle.isOn = !toggle.isOn;
            return;
        }

        // For other elements, left/right does vertical navigation
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
        else if (selectable is TMP_Dropdown dropdown)
        {
            dropdown.Show();
        }

        if (debugLog)
            Debug.Log($"[UINavigation] Confirmed: {selectable.gameObject.name}");

        // Trigger release
        StartCoroutine(DelayedPointerUp(selectable));
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

        // Get world corners of the selectable
        Vector3[] corners = new Vector3[4];
        selectableRect.GetWorldCorners(corners);
        // corners[0] = bottom-left, corners[1] = top-left, corners[2] = top-right, corners[3] = bottom-right

        Vector3 anchorPosition = Vector3.zero;

        switch (indicatorPosition)
        {
            case IndicatorPosition.Left:
                // Left-middle
                anchorPosition = (corners[0] + corners[1]) / 2f;
                break;

            case IndicatorPosition.Right:
                // Right-middle
                anchorPosition = (corners[2] + corners[3]) / 2f;
                break;

            case IndicatorPosition.Top:
                // Top-center
                anchorPosition = (corners[1] + corners[2]) / 2f;
                break;

            case IndicatorPosition.Bottom:
                // Bottom-center
                anchorPosition = (corners[0] + corners[3]) / 2f;
                break;

            case IndicatorPosition.Center:
                // Center of element
                anchorPosition = (corners[0] + corners[2]) / 2f;
                break;
        }

        // Apply offset
        Vector3 finalPosition = anchorPosition + (Vector3)indicatorOffset;

        // Set indicator position
        indicatorRect.position = finalPosition;

        if (debugLog)
            Debug.Log($"[UINavigation] Positioned indicator at {indicatorPosition} of {selectable.name}");
    }

    private void AnimateIndicator()
    {
        if (indicatorRect == null) return;

        animationTimer += Time.unscaledDeltaTime * animationSpeed;

        // Pulse animation
        float scale = 1f + Mathf.Sin(animationTimer) * 0.1f;
        indicatorRect.localScale = indicatorBaseScale * scale;

        // Optional: Horizontal bounce
        float bounce = Mathf.Sin(animationTimer * 2f) * 3f;
        Vector3 pos = indicatorRect.localPosition;
        pos.x = bounce;
        indicatorRect.localPosition = pos;
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

    #endregion
}