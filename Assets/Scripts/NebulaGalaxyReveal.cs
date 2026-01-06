using UnityEngine;

public class NebulaGalaxyReveal : MonoBehaviour
{
    [Header("Galaxy")]
    public GameObject galaxyRoot;

    [Header("Startup")]
    public bool hideGalaxyAtStart = true;

    [Header("Reveal Timing (seconds)")]
    public float revealDuration = 12f;

    [Header("Fade Window (0..1 of revealDuration)")]
    [Range(0f, 1f)] public float galaxyFadeStart01 = 0.30f;
    [Range(0f, 1f)] public float galaxyFadeEnd01 = 1.00f;

    [Header("Tint Fade (multiplier on _SGT_Tint RGB)")]
    [Tooltip("Start multiplier on the galaxy tint RGB. Use 0 for fully invisible.")]
    [Range(0f, 1f)] public float startTintMultiplier = 0.0f;

    [Tooltip("End multiplier on the galaxy tint RGB. 1 = original galaxy appearance.")]
    [Range(0f, 2f)] public float endTintMultiplier = 1.0f;

    [Tooltip("Smooth easing")]
    public bool useSmoothStep = true;

    [Header("Rotation")]
    public float galaxyRotateDegPerSec = 0.0f;

    [Header("Starfield Handoff")]
    public ReleasePhaseController releasePhase;

    // Galaxy.shader uses this property
    private static readonly int SGT_Tint = Shader.PropertyToID("_SGT_Tint");

    private Renderer[] galaxyRenderers;
    private MaterialPropertyBlock mpb;

    private bool running;
    private float t;
    private bool handoffDone;

    // Base tint read from the galaxy material
    private Color baseTint = Color.white;
    private bool hasBaseTint = false;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        CacheRenderersAndBaseTint();

        if (galaxyRoot != null && hideGalaxyAtStart)
            galaxyRoot.SetActive(false);

        // If galaxy is visible at start, force it to faint state to avoid pop
        if (!hideGalaxyAtStart)
            ApplyTintMultiplier(0f);
    }

    void Update()
    {
        if (!running) return;

        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / Mathf.Max(0.001f, revealDuration));

        if (!handoffDone && u >= galaxyFadeStart01)
        {
            handoffDone = true;

            if (releasePhase != null)
                releasePhase.ApplyReleaseStarfieldLookNow();
        }

        float ug = Mathf.InverseLerp(galaxyFadeStart01, galaxyFadeEnd01, u);
        if (useSmoothStep) ug = Mathf.SmoothStep(0f, 1f, ug);

        ApplyTintMultiplier(ug);

        if (galaxyRoot != null && Mathf.Abs(galaxyRotateDegPerSec) > 0.0001f)
            galaxyRoot.transform.Rotate(Vector3.up, galaxyRotateDegPerSec * Time.deltaTime, Space.World);

        if (u >= 1f)
            running = false;
    }

    public void BeginReveal()
    {
        running = true;
        t = 0f;
        handoffDone = false;

        if (galaxyRoot != null && !galaxyRoot.activeSelf)
            galaxyRoot.SetActive(true);

        CacheRenderersAndBaseTint();

        //prevent first-frame pop by forcing the faint state immediately
        ApplyTintMultiplier(0f);

        if (!hasBaseTint)
        {
            Debug.LogWarning("[NebulaGalaxyReveal] Could not find _SGT_Tint on galaxy materials. " +
                             "Galaxy may not fade as expected.");
        }
    }

    private void CacheRenderersAndBaseTint()
    {
        galaxyRenderers = null;
        hasBaseTint = false;

        if (galaxyRoot == null) return;

        galaxyRenderers = galaxyRoot.GetComponentsInChildren<Renderer>(true);

        // Read base tint from the first renderer that has _SGT_Tint
        foreach (var r in galaxyRenderers)
        {
            if (r == null) continue;
            var m = r.sharedMaterial;
            if (m == null) continue;

            if (m.HasProperty(SGT_Tint))
            {
                baseTint = m.GetColor(SGT_Tint);
                hasBaseTint = true;
                break;
            }
        }

        if (!hasBaseTint)
            baseTint = Color.white;
    }

    private void ApplyTintMultiplier(float ug01)
    {
        if (galaxyRenderers == null || mpb == null) return;

        // Multiplier over time
        float mul = Mathf.Lerp(startTintMultiplier, endTintMultiplier, ug01);

        // Scale RGB, preserve alpha from the material
        Color c = baseTint;
        c.r *= mul;
        c.g *= mul;
        c.b *= mul;

        foreach (var r in galaxyRenderers)
        {
            if (r == null) continue;
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty(SGT_Tint)) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(SGT_Tint, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
