using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpellFloorImage : MonoBehaviour
{
    [Header("References")]
    public GameObject follow;                 // Object to follow
    public Spellcasting spellcasting;         // Reference to your Spellcasting component

    [Header("Visual Settings")]
    public float baseHeight = 0.941f;
    public float appearDuration = 0.4f;
    public float visibleDuration = 1.0f;
    public float fadeOutTime = 0.4f;
    public float spinSpeed = 180f;            // degrees per second
    public float maxScaleMultiplier = 1.4f;   // Final size when expanding

    private SpriteRenderer spriteRenderer;
    private Coroutine effectRoutine;
    private bool isVisible;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.zero;
    }

    void Start()
    {
        spriteRenderer.enabled = false;
    }

    void Update()
    {
        if (follow != null)
        {
            transform.position = new Vector3(follow.transform.position.x, baseHeight, follow.transform.position.z);
            transform.rotation = Quaternion.Euler(-90, 0, 0);
        }

        // Optional: add spin when visible
        if (isVisible)
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    public void ShowSpell(string spellName, Color spellColor)
    {
        if (spriteRenderer == null)
        {
            Debug.LogError("[SpellFloorImage] SpriteRenderer is missing!");
            return;
        }

        spriteRenderer.color = spellColor;
        spriteRenderer.enabled = true;

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
            effectRoutine = null;
        }

        effectRoutine = StartCoroutine(PlayEffect());
    }

    private IEnumerator PlayEffect()
    {
        if (spriteRenderer == null)
        {
            yield break;
        }

        isVisible = true;
        spriteRenderer.enabled = true;
        spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);

        float timer = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * maxScaleMultiplier;

        // Expand animation
        while (timer < appearDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / appearDuration);
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        transform.localScale = endScale;

        yield return new WaitForSeconds(visibleDuration);

        // Fade out
        timer = 0f;
        Color originalColor = spriteRenderer.color;

        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeOutTime);
            spriteRenderer.color = Color.Lerp(originalColor, new Color(originalColor.r, originalColor.g, originalColor.b, 0f), t);
            yield return null;
        }

        // Reset
        spriteRenderer.enabled = false;
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        transform.localScale = Vector3.zero;
        isVisible = false;
        effectRoutine = null;
    }
}
