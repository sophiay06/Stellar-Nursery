using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandDistanceStabilityXRHands : MonoBehaviour
{
    [Header("XR Hands")]
    [Tooltip("If null, we auto-find a running XRHandSubsystem.")]
    public XRHandSubsystem handSubsystem;

    [Header("Which joint to use for distance")]
    public XRHandJointID joint = XRHandJointID.Wrist;

    [Header("Distance band (meters)")]
    [Tooltip("Minimum comfortable hand separation for 'in range'.")]
    public float minDistance = 0.30f;
    [Tooltip("Maximum comfortable hand separation for 'in range'.")]
    public float maxDistance = 0.60f;

    [Header("Filtering")]
    [Tooltip("Higher = smoother distance (less jitter).")]
    public float distanceLerpSpeed = 12f;

    [Header("Stability window")]
    [Tooltip("How many seconds of distance samples to evaluate stability over.")]
    public float windowSeconds = 2.0f;

    [Tooltip("If stdDev <= this (meters), treat as 'very stable'. Typical: 0.005~0.02")]
    public float stableStdDevMeters = 0.01f;

    [Header("Outputs (read-only)")]
    [Range(0f, 1f)] public float distance01;     // normalized to [minDistance,maxDistance]
    [Range(0f, 1f)] public float stability01;    // 1 = stable, 0 = unstable
    public float rawDistanceMeters;
    public float filteredDistanceMeters;
    public bool inRange;
    public bool tracked;

    struct Sample
    {
        public float t;
        public float d;
        public Sample(float t, float d) { this.t = t; this.d = d; }
    }

    private readonly List<Sample> samples = new List<Sample>(512);

    void OnEnable()
    {
        if (handSubsystem == null) handSubsystem = FindRunningHandSubsystem();
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running)
        {
            handSubsystem = FindRunningHandSubsystem();
            tracked = false;
            return;
        }

        XRHand left = handSubsystem.leftHand;
        XRHand right = handSubsystem.rightHand;

        if (!left.isTracked || !right.isTracked)
        {
            tracked = false;
            return;
        }

        XRHandJoint lj = left.GetJoint(joint);
        XRHandJoint rj = right.GetJoint(joint);

        if (!lj.TryGetPose(out Pose lp) || !rj.TryGetPose(out Pose rp))
        {
            tracked = false;
            return;
        }

        tracked = true;

        rawDistanceMeters = Vector3.Distance(lp.position, rp.position);

        // Smooth distance to reduce micro jitter
        if (distanceLerpSpeed <= 0f)
            filteredDistanceMeters = rawDistanceMeters;
        else
            filteredDistanceMeters = Mathf.Lerp(filteredDistanceMeters, rawDistanceMeters, Time.deltaTime * distanceLerpSpeed);

        // Range gating
        inRange = (filteredDistanceMeters >= minDistance && filteredDistanceMeters <= maxDistance);

        // Normalize distance to 0..1 (expanded->compressed can be inverted later)
        distance01 = Mathf.InverseLerp(minDistance, maxDistance, filteredDistanceMeters);
        distance01 = Mathf.Clamp01(distance01);

        // Collect samples for stability estimate (use filtered distance)
        float now = Time.time;
        samples.Add(new Sample(now, filteredDistanceMeters));

        // Drop old samples outside the window
        float cutoff = now - Mathf.Max(0.05f, windowSeconds);
        int removeCount = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].t < cutoff) removeCount++;
            else break;
        }
        if (removeCount > 0) samples.RemoveRange(0, removeCount);

        // Compute std deviation over window -> stability score
        stability01 = ComputeStability01(samples, stableStdDevMeters);
    }

    private float ComputeStability01(List<Sample> s, float stableStd)
    {
        if (s == null || s.Count < 5) return 0f;

        float mean = 0f;
        for (int i = 0; i < s.Count; i++) mean += s[i].d;
        mean /= s.Count;

        float var = 0f;
        for (int i = 0; i < s.Count; i++)
        {
            float e = s[i].d - mean;
            var += e * e;
        }
        var /= (s.Count - 1);
        float std = Mathf.Sqrt(var);

        // Map stdDev -> [0..1] where <= stableStd => near 1
        // Use a soft falloff rather than hard threshold.
        float x = std / Mathf.Max(1e-6f, stableStd);
        float score = 1f / (1f + x * x); // nice smooth curve
        return Mathf.Clamp01(score);
    }

    private XRHandSubsystem FindRunningHandSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        for (int i = 0; i < subsystems.Count; i++)
            if (subsystems[i] != null && subsystems[i].running)
                return subsystems[i];

        return (subsystems.Count > 0) ? subsystems[0] : null;
    }
}
