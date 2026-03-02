using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class StarfieldColorFromWristXRHands : MonoBehaviour
{
    [Header("Target (drives StarfieldColorScale.t 0..1)")]
    public StarfieldColorScale target;

    [Header("XR Hands")]
    [Tooltip("Optional. If left null, the script will try to find a running XRHandSubsystem automatically.")]
    public XRHandSubsystem handSubsystem;

    [Header("Angle -> t mapping")]
    [Tooltip("Degrees of wrist 'roll' that map to t=0. Example: -60")]
    public float minAngleDeg = -60f;

    [Tooltip("Degrees of wrist 'roll' that map to t=1. Example: +60")]
    public float maxAngleDeg = 60f;

    [Tooltip("Extra deadzone around 0 degrees (reduces jitter near neutral). 0 = off.")]
    public float deadzoneDeg = 5f;

    [Header("Smoothing")]
    [Tooltip("0 = no smoothing. Higher = smoother.")]
    public float lerpSpeed = 8f;

    [Header("Debug")]
    public bool debugLogs = true;
    [Tooltip("How often to log (seconds).")]
    public float debugInterval = 0.25f;

    private float smoothedT = 0f;
    private float nextDebugTime = 0f;

    void OnEnable()
    {
        if (target == null) target = GetComponent<StarfieldColorScale>();
        if (target != null) smoothedT = target.t;

        if (handSubsystem == null) handSubsystem = FindRunningHandSubsystem();
        nextDebugTime = Time.time;
    }

    void Update()
    {
        if (target == null)
        {
            DebugLogRateLimited("[ColorMapping] target is NULL (assign StarfieldColorScale).");
            return;
        }

        if (handSubsystem == null || !handSubsystem.running)
        {
            handSubsystem = FindRunningHandSubsystem();
            DebugLogRateLimited("[ColorMapping] XRHandSubsystem not running (yet).");
            return;
        }

        XRHand rightHand = handSubsystem.rightHand;
        if (!rightHand.isTracked)
        {
            DebugLogRateLimited("[ColorMapping] Right hand NOT tracked.");
            return;
        }

        XRHandJoint wrist = rightHand.GetJoint(XRHandJointID.Wrist);
        if (!wrist.TryGetPose(out Pose wristPose))
        {
            DebugLogRateLimited("[ColorMapping] Wrist pose missing.");
            return;
        }

        // --- Compute a roll-like angle (approx pronation/supination proxy) ---
        float rollDeg = wristPose.rotation.eulerAngles.z; // 0..360
        if (rollDeg > 180f) rollDeg -= 360f;             // -180..180

        // Deadzone around neutral to reduce flicker.
        if (deadzoneDeg > 0f && Mathf.Abs(rollDeg) < deadzoneDeg)
            rollDeg = 0f;

        // Map angle -> [0..1]
        float rawT = Mathf.InverseLerp(minAngleDeg, maxAngleDeg, rollDeg);
        rawT = Mathf.Clamp01(rawT);

        // Smooth
        if (lerpSpeed <= 0f) smoothedT = rawT;
        else smoothedT = Mathf.Lerp(smoothedT, rawT, Time.deltaTime * Mathf.Max(0.01f, lerpSpeed));

        // Apply
        target.t = smoothedT;

        // Debug output (pose + mapping)
        if (debugLogs && Time.time >= nextDebugTime)
        {
            nextDebugTime = Time.time + debugInterval;

            Vector3 p = wristPose.position;
            Vector3 e = wristPose.rotation.eulerAngles;

            Debug.Log(
                $"[ColorMapping] tracked={rightHand.isTracked} " +
                $"wristPos=({p.x:F3},{p.y:F3},{p.z:F3}) " +
                $"wristEuler=({e.x:F1},{e.y:F1},{e.z:F1}) " +
                $"rollZ={rollDeg:F1} rawT={rawT:F2} smoothT={smoothedT:F2}"
            );
        }
    }

    private void DebugLogRateLimited(string msg)
    {
        if (!debugLogs) return;
        if (Time.time < nextDebugTime) return;
        nextDebugTime = Time.time + debugInterval;
        Debug.Log(msg);
    }

    private XRHandSubsystem FindRunningHandSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        for (int i = 0; i < subsystems.Count; i++)
            if (subsystems[i] != null && subsystems[i].running)
                return subsystems[i];

        // If none running, return the first (may start later)
        return (subsystems.Count > 0) ? subsystems[0] : null;
    }
}
