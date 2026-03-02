using UnityEngine;
using SpaceGraphicsToolkit;

[ExecuteAlways]
public class DustSquashBreathController : MonoBehaviour
{
    [Header("References")]
    public MeditationStateController meditation;
    public BreathingManual breathing;
    public SgtBackgroundMesh dustMesh;

    [Header("Active Phases")]
    public bool applyInArrival = true;
    public bool applyInBalance = true;
    public bool applyInRelease = false;

    [Header("Squash Mapping (0..1)")]
    [Range(0f, 1f)] public float squashAtExhale = 0.75f;
    [Range(0f, 1f)] public float squashAtInhale = 0.95f;

    [Header("response curve")]
    [Tooltip("1 = linear. >1 makes changes stronger near inhale; <1 makes them stronger near exhale.")]
    [Range(0.25f, 3f)] public float curve = 1f;

    void Reset()
    {
        if (dustMesh == null) dustMesh = GetComponent<SgtBackgroundMesh>();
    }

    void Update()
    {
        if (breathing == null || dustMesh == null) return;
        if (!ShouldRun()) return;

        float b = Mathf.Clamp01(breathing.breath01);
        b = Mathf.Pow(b, Mathf.Max(0.01f, curve));

        float target = Mathf.Lerp(squashAtExhale, squashAtInhale, b);

        // Direct mapping (no smoothing)
        dustMesh.Squash = target;
        // Setting Squash calls DirtyMesh() internally when it changes. :contentReference[oaicite:3]{index=3}
    }

    bool ShouldRun()
    {
        if (meditation == null) return true;

        switch (meditation.currentPhase)
        {
            case MeditationPhase.Arrival: return applyInArrival;
            case MeditationPhase.Balance: return applyInBalance;
            case MeditationPhase.Release: return applyInRelease;
            default: return false;
        }
    }
}
