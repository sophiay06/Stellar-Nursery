using UnityEngine;

public class HandDistance01FromTransforms : MonoBehaviour
{
    [Header("Assign the two hands (wrist/palm transforms)")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Map meters -> 0..1")]
    [Tooltip("Shoulder-width-ish distance (minimum expected).")]
    public float minDistanceMeters = 0.25f;

    [Tooltip("Wider than shoulders (maximum expected).")]
    public float maxDistanceMeters = 0.70f;

    [Header("Smoothing")]
    public float lerpSpeed = 12f;

    [Header("Outputs")]
    [Range(0f, 1f)] public float handsDistance01;
    public float rawDistanceMeters;
    public bool tracked;

    void Update()
    {
        if (leftHand == null || rightHand == null)
        {
            tracked = false;
            handsDistance01 = 0f;
            rawDistanceMeters = 0f;
            return;
        }

        tracked = leftHand.gameObject.activeInHierarchy && rightHand.gameObject.activeInHierarchy;

        rawDistanceMeters = Vector3.Distance(leftHand.position, rightHand.position);

        float target01 = Mathf.Clamp01(Mathf.InverseLerp(minDistanceMeters, maxDistanceMeters, rawDistanceMeters));

        if (lerpSpeed <= 0f) handsDistance01 = target01;
        else handsDistance01 = Mathf.Lerp(handsDistance01, target01, Time.deltaTime * lerpSpeed);
    }
}
