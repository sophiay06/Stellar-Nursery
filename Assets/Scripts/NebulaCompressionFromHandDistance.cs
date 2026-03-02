using UnityEngine;

public class NebulaCompressionFromHandDistance : MonoBehaviour
{
    [Header("Input")]
    public HandDistanceStabilityXRHands handDistance;

    [Header("Output")]
    public NebulaCompression nebulaCompression;

    [Header("Mapping")]
    [Tooltip("If true: smaller distance => higher compression.")]
    public bool invert = true;

    [Tooltip("How quickly compression follows the mapped value.")]
    public float lerpSpeed = 2.5f;

    [Header("Optional: freeze when not tracked")]
    public bool freezeWhenNotTracked = true;

    void Update()
    {
        if (handDistance == null || nebulaCompression == null) return;

        if (!handDistance.tracked)
        {
            if (!freezeWhenNotTracked)
                nebulaCompression.compression = Mathf.Lerp(nebulaCompression.compression, 0f, Time.deltaTime * lerpSpeed);
            return;
        }

        float x = handDistance.distance01; // 0..1 within [minDistance,maxDistance]
        float target = invert ? (1f - x) : x;

        nebulaCompression.compression = Mathf.Lerp(
            nebulaCompression.compression,
            target,
            Time.deltaTime * Mathf.Max(0.01f, lerpSpeed)
        );
    }
}
