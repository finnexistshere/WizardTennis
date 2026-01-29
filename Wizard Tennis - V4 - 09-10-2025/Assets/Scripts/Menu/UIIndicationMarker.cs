using UnityEngine;

/// <summary>
/// Marks a UI element with a custom indicator position
/// and optional navigation exclusion.
/// </summary>
public class UIIndicatorMarker : MonoBehaviour
{
    [Header("Navigation")]
    [Tooltip("If true, this UI element will be ignored by UI navigation.")]
    public bool excludeFromNavigation = false;

    [Header("Indicator Position")]
    public RectTransform indicatorPosition;
    public bool useThisTransform = false;

    [Header("Optional Offset")]
    public Vector2 customOffset = Vector2.zero;

    public Vector2 GetLocalIndicatorPosition()
    {
        RectTransform root = GetComponent<RectTransform>();

        if (useThisTransform && root != null)
            return customOffset;

        if (indicatorPosition != null && root != null)
            return root.InverseTransformPoint(indicatorPosition.position) + (Vector3)customOffset;

        return customOffset;
    }

    public bool HasCustomPosition()
    {
        return indicatorPosition != null || useThisTransform;
    }

    public bool IsExcludedFromNavigation()
    {
        return excludeFromNavigation;
    }
}
