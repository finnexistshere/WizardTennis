using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class UITextColour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Text Colors")]
    public Color normalColor = Color.white;
    public Color highlightedColor = Color.yellow;
    public Color pressedColor = Color.gray;
    public Color disabledColor = Color.grey;
    public Color toggledOnColor = Color.green;

    [Header("Hover Animation")]
    [Tooltip("How much to rotate the button when hovered.")]
    public float hoverTwistAngle = 8f;
    [Tooltip("How fast the twist animation plays.")]
    public float twistSpeed = 10f;

    [Header("Hover Sound Effects")]
    public AudioSource audioSource;
    public AudioClip creakSFX;
    public AudioClip sparkleSFX;

    [Header("Toggle Settings")]
    [Tooltip("Use toggle-specific colors when toggle is on")]
    public bool useToggleColors = true;

    [Header("Selection Settings")]
    [Tooltip("Maintain highlighted state when selected via keyboard")]
    public bool maintainSelectionHighlight = true;

    private TMP_Text txt;
    private Button btn;
    private Toggle toggle;
    private bool lastInteractable;
    private bool isHovered;
    private bool isKeyboardSelected;
    private Quaternion originalRotation;
    private Quaternion targetRotation;

    private float hoverCooldown = 0.05f; // 50ms stability window
    private float lastHoverEventTime = -1f;
    private float forceResetTimer = 0f;
    private const float FORCE_RESET_DELAY = 0.3f; // Reset after 300ms if stuck

    // Track which component we're using
    private enum ComponentType { None, Button, Toggle }
    private ComponentType componentType = ComponentType.None;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // Check for Button first, then Toggle
        btn = GetComponent<Button>();
        toggle = GetComponent<Toggle>();

        if (btn != null)
        {
            componentType = ComponentType.Button;
            lastInteractable = btn.interactable;
            btn.onClick.AddListener(ResetButton);
        }
        else if (toggle != null)
        {
            componentType = ComponentType.Toggle;
            lastInteractable = toggle.interactable;
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
        else
        {
            Debug.LogWarning($"UITextColour: No Button or Toggle found on {gameObject.name}!");
        }

        // Find TMP_Text in children
        txt = GetComponentInChildren<TMP_Text>(true);

        if (txt == null)
            Debug.LogWarning($"UITextColour: No TMP_Text found on {gameObject.name}!");

        originalRotation = transform.localRotation;
        targetRotation = originalRotation;

        UpdateTextColor();
    }

    void OnDestroy()
    {
        // Clean up listeners
        if (btn != null)
            btn.onClick.RemoveListener(ResetButton);
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
    }

    void Update()
    {
        // Smooth rotation
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.unscaledDeltaTime * twistSpeed);

        // Detect interactable change
        bool currentInteractable = IsInteractable();
        if (currentInteractable != lastInteractable)
        {
            UpdateTextColor();
            lastInteractable = currentInteractable;
        }

        // Check if this is the currently selected UI element
        bool isCurrentlySelected = EventSystem.current != null &&
                                   EventSystem.current.currentSelectedGameObject == gameObject;

        // Update keyboard selection state
        if (isCurrentlySelected != isKeyboardSelected)
        {
            isKeyboardSelected = isCurrentlySelected;

            if (isKeyboardSelected && maintainSelectionHighlight)
            {
                // Apply highlight when selected via keyboard
                txt.color = highlightedColor;
                targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle);
            }
            else if (!isHovered)
            {
                // Remove highlight when deselected (and not hovered)
                UpdateTextColor();

                if (IsToggleOn())
                {
                    targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 0.5f);
                }
                else
                {
                    targetRotation = originalRotation;
                }
            }
        }

        // Force reset if hovered state is stuck
        if (isHovered && !isKeyboardSelected)
        {
            forceResetTimer += Time.unscaledDeltaTime;
            if (forceResetTimer > FORCE_RESET_DELAY)
            {
                // Check if mouse is actually over this object
                if (!IsMouseOverUI())
                {
                    // Mouse is not over UI - force reset
                    ForceReset();
                }
            }
        }
        else
        {
            forceResetTimer = 0f;
        }
    }

    /// <summary>
    /// Check if mouse is actually over this UI element
    /// </summary>
    private bool IsMouseOverUI()
    {
        // Use EventSystem to check if pointer is over this object
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject == gameObject)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Force reset the hover state (used when stuck)
    /// </summary>
    private void ForceReset()
    {
        isHovered = false;
        forceResetTimer = 0f;
        UpdateTextColor();

        if (IsToggleOn())
        {
            targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 0.5f);
        }
        else
        {
            targetRotation = originalRotation;
        }
    }

    private bool IsInteractable()
    {
        switch (componentType)
        {
            case ComponentType.Button:
                return btn != null && btn.interactable;
            case ComponentType.Toggle:
                return toggle != null && toggle.interactable;
            default:
                return false;
        }
    }

    private bool IsToggleOn()
    {
        return componentType == ComponentType.Toggle && toggle != null && toggle.isOn;
    }

    void UpdateTextColor()
    {
        if (txt == null) return;

        if (!IsInteractable())
        {
            txt.color = disabledColor;
        }
        else if (isKeyboardSelected && maintainSelectionHighlight)
        {
            // Keyboard selected - always highlighted
            txt.color = highlightedColor;
        }
        else if (useToggleColors && IsToggleOn())
        {
            // Toggle is on - use special color
            txt.color = isHovered ? highlightedColor : toggledOnColor;
        }
        else if (isHovered)
        {
            txt.color = highlightedColor;
        }
        else
        {
            txt.color = normalColor;
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        // Update color when toggle state changes
        UpdateTextColor();

        // Optional: Add a subtle rotation pulse when toggled
        if (isOn)
        {
            targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 0.5f);
        }
        else
        {
            targetRotation = originalRotation;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable() || txt == null) return;

        // Prevent edge jitter from spamming enter/exit events
        if (Time.unscaledTime - lastHoverEventTime < hoverCooldown)
            return;

        lastHoverEventTime = Time.unscaledTime;

        isHovered = true;
        forceResetTimer = 0f; // Reset the stuck timer
        txt.color = highlightedColor;
        targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle);

        if (creakSFX != null)
            audioSource.PlayOneShot(creakSFX);
        if (sparkleSFX != null)
            audioSource.PlayOneShot(sparkleSFX);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable() || txt == null) return;

        forceResetTimer = 0f; // Reset the stuck timer
        txt.color = pressedColor;
        targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 1.5f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsInteractable() || txt == null) return;

        forceResetTimer = 0f; // Reset the stuck timer
        txt.color = highlightedColor;
        targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (txt == null) return;

        // Prevent exit spam on boundary jitter
        if (Time.unscaledTime - lastHoverEventTime < hoverCooldown)
            return;

        lastHoverEventTime = Time.unscaledTime;

        isHovered = false;
        forceResetTimer = 0f; // Reset the stuck timer
        UpdateTextColor();

        // For toggles, maintain slight rotation if toggled on
        if (IsToggleOn())
        {
            targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 0.5f);
        }
        else
        {
            targetRotation = originalRotation;
        }
    }

    /// <summary>
    /// Resets the button/toggle to its normal color and rotation.
    /// Can be called from the button's OnClick event.
    /// </summary>
    public void ResetButton()
    {
        isHovered = false;

        if (IsToggleOn())
        {
            targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 0.5f);
        }
        else
        {
            targetRotation = originalRotation;
        }

        UpdateTextColor();
    }
}