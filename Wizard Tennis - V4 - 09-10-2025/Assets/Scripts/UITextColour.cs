using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
public class UITextColour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Text Colors")]
    public Color normalColor = Color.white;
    public Color highlightedColor = Color.yellow;
    public Color pressedColor = Color.gray;
    public Color disabledColor = Color.grey;

    [Header("Hover Animation")]
    [Tooltip("How much to rotate the button when hovered.")]
    public float hoverTwistAngle = 8f;
    [Tooltip("How fast the twist animation plays.")]
    public float twistSpeed = 10f;

    [Header("Hover Sound Effects")]
    public AudioSource audioSource;
    public AudioClip creakSFX;
    public AudioClip sparkleSFX;

    private TMP_Text txt;
    private Button btn;
    private bool lastInteractable;
    private bool isHovered;
    private Quaternion originalRotation;
    private Quaternion targetRotation;

    void Awake()
    {
        btn = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();

        // Find TMP_Text in children
        txt = GetComponentInChildren<TMP_Text>(true);

        if (txt == null)
            Debug.LogWarning($"UITextColour: No TMP_Text found on {gameObject.name}!");

        lastInteractable = btn.interactable;
        originalRotation = transform.localRotation;
        targetRotation = originalRotation;

        UpdateTextColor();

        // Optional: hook ResetButton to button click
        btn.onClick.AddListener(ResetButton);
    }

    void Update()
    {
        // Smooth rotation
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.unscaledDeltaTime * twistSpeed);

        // Detect interactable change
        if (btn.interactable != lastInteractable)
        {
            UpdateTextColor();
            lastInteractable = btn.interactable;
        }
    }

    void UpdateTextColor()
    {
        if (txt == null) return;

        if (!btn.interactable)
            txt.color = disabledColor;
        else if (isHovered)
            txt.color = highlightedColor;
        else
            txt.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!btn.interactable || txt == null) return;

        isHovered = true;
        txt.color = highlightedColor;
        targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle);
        if (creakSFX != null)
            audioSource.PlayOneShot(creakSFX);
        if (sparkleSFX != null)
            audioSource.PlayOneShot(sparkleSFX);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!btn.interactable || txt == null) return;

        txt.color = pressedColor;
        targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle * 1.5f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!btn.interactable || txt == null) return;

        txt.color = highlightedColor;
        targetRotation = originalRotation * Quaternion.Euler(0f, 0f, hoverTwistAngle);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (txt == null) return;

        isHovered = false;
        UpdateTextColor();
        targetRotation = originalRotation;
    }

    /// <summary>
    /// Resets the button to its normal color and rotation.
    /// Can be called from the button's OnClick event.
    /// </summary>
    public void ResetButton()
    {
        isHovered = false;
        targetRotation = originalRotation;
        if (txt != null)
            txt.color = normalColor;
    }
}
