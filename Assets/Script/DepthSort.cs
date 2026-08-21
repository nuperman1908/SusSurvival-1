using UnityEngine;

public static class DepthSort
{
    public const float FeetOffset = 0.45f; // matches the shadow child's local Y offset on Player/Enemy
    const float Precision = 50f;

    // Lower Y (closer to the bottom of the screen) sorts in front (higher order); higher Y sorts
    // behind. Computed relative to the player's Y so the value stays small regardless of how far
    // world-space position has drifted over a long run (sortingOrder is a short, easy to overflow).
    public static int ComputeOrder(float y, float referenceY)
    {
        return -Mathf.RoundToInt((y - FeetOffset - referenceY) * Precision);
    }
}
