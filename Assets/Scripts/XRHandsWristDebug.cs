using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class XRHandsWristDebug : MonoBehaviour
{
    [Header("XR Hands")]
    [Tooltip("Optional. If null, auto-finds a running XRHandSubsystem.")]
    public XRHandSubsystem handSubsystem;

    [Header("Joint to debug")]
    public XRHandJointID joint = XRHandJointID.Wrist;

    [Header("Logging")]
    public bool logToConsole = true;
    [Tooltip("How often to log (seconds).")]
    public float logInterval = 0.25f;

    [Header("Visual Debug (Scene view)")]
    public bool drawAxes = true;
    public float axisLength = 0.08f;

    private float nextLogTime;

    void OnEnable()
    {
        if (handSubsystem == null) handSubsystem = FindRunningHandSubsystem();
        nextLogTime = Time.time;
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running)
        {
            handSubsystem = FindRunningHandSubsystem();
            if (Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + logInterval;
                if (logToConsole) Debug.Log("[XRHandsWristDebug] XRHandSubsystem NOT running (yet).");
            }
            return;
        }

        XRHand right = handSubsystem.rightHand;
        if (!right.isTracked)
        {
            if (Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + logInterval;
                if (logToConsole) Debug.Log("[XRHandsWristDebug] Right hand NOT tracked.");
            }
            return;
        }

        XRHandJoint j = right.GetJoint(joint);
        if (!j.TryGetPose(out Pose pose))
        {
            if (Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + logInterval;
                if (logToConsole) Debug.Log($"[XRHandsWristDebug] Joint pose missing: {joint}");
            }
            return;
        }

        // --- Compute roll-like angle (approx pronation/supination) ---
        // This is "rotation around the hand's forward axis" proxy using euler Z.
        float rollDeg = pose.rotation.eulerAngles.z; // 0..360
        if (rollDeg > 180f) rollDeg -= 360f;         // -180..180

        if (Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + logInterval;

            if (logToConsole)
            {
                Vector3 p = pose.position;
                Vector3 e = pose.rotation.eulerAngles;
                Debug.Log(
                    $"[XRHandsWristDebug] {joint} pos=({p.x:F3},{p.y:F3},{p.z:F3}) " +
                    $"rotEuler=({e.x:F1},{e.y:F1},{e.z:F1}) rollZ={rollDeg:F1}"
                );
            }
        }

        if (drawAxes)
        {
            // Draw local axes at the joint pose (Scene view)
            Vector3 origin = pose.position;
            Vector3 rightAxis = pose.rotation * Vector3.right;
            Vector3 upAxis = pose.rotation * Vector3.up;
            Vector3 fwdAxis = pose.rotation * Vector3.forward;

            Debug.DrawRay(origin, rightAxis * axisLength, Color.red);
            Debug.DrawRay(origin, upAxis * axisLength, Color.green);
            Debug.DrawRay(origin, fwdAxis * axisLength, Color.blue);
        }
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
