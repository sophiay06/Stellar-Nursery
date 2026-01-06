using UnityEngine;
using UnityEngine.Events;
using SpaceGraphicsToolkit.Nebula;
using SpaceGraphicsToolkit.Starfield;

public class ReleasePhaseController : MonoBehaviour
{
    [Header("References")]
    public MeditationStateController meditation;
    public SgtNebula nebula;
    public SgtStarfieldInfinite starfield;

    [Tooltip("sync our release duration to this reveal duration")]
    public NebulaGalaxyReveal galaxyReveal;

    [Header("Release timing")]
    public float releaseDuration = 12f; // used if galaxyReveal is null
    public UnityEvent onSessionEnded;

    [Header("End Behavior")]
    public bool endSessionWhenFinished = false;
    public bool keepNebulaActiveAtEnd = true;

    [Header("Nebula soften (geometry only)")]
    public float targetDisplacement = 10f;
    public float targetFrequency = 0.7f;
    public float targetFlattening = 0.05f;

    [Header("Nebula fade-out (intensity)")]
    [Range(0f, 1f)] public float nebulaFadeStart01 = 0.35f;
    [Range(0f, 1f)] public float nebulaFadeEnd01 = 0.95f;
    [Range(0f, 1f)] public float nebulaFinalIntensity = 0.06f;

    public bool disableNebulaAtEnd = true;

    [Header("Starfield starlight look (apply when galaxy starts)")]
    public float releaseStarBrightness = 0.25f;
    public int releaseStarCount = 6000;
    public int updateStep = 500;

    [Header("Smoothing")]
    public float nebulaLerpSpeed = 0.5f;
    public float starBrightnessLerpSpeed = 0.8f;

    private bool releaseActive;
    private float t;

    private float startDisp, startFreq, startFlat;

    private Material nebulaMat;
    private Color startTint;
    private bool nebulaWasActiveAtStart;

    private static readonly int SGT_Tint = Shader.PropertyToID("_SGT_Tint");

    // starfield simplification is delayed until galaxy fade-in begins
    private bool starfieldSimplificationArmed = false;
    private bool starfieldSimplificationActive = false;

    void Update()
    {
        if (meditation == null) return;

        if (!releaseActive &&
            meditation.currentPhase == MeditationPhase.Release &&
            meditation.releaseLatched)
        {
            meditation.releaseLatched = false;

            BeginRelease();

            if (galaxyReveal != null)
                galaxyReveal.BeginReveal();
        }

        if (!releaseActive) return;

        t += Time.deltaTime;

        float duration = GetDuration();
        float u = Mathf.Clamp01(t / Mathf.Max(0.001f, duration));

        //nebula geometry soften
        if (nebula != null)
        {
            float s = Mathf.Clamp01(u * nebulaLerpSpeed + (1f - nebulaLerpSpeed) * u);

            nebula.Displacement = Mathf.Lerp(startDisp, targetDisplacement, s);
            nebula.Frequency = Mathf.Lerp(startFreq, targetFrequency, s);
            nebula.Flattening = Mathf.Lerp(startFlat, targetFlattening, s);
        }

        // nebula tint fade to faint
        if (nebulaMat != null && nebulaMat.HasProperty(SGT_Tint))
        {
            float fadeU = Mathf.InverseLerp(nebulaFadeStart01, nebulaFadeEnd01, u);

            var toFaint = startTint;
            toFaint.r *= nebulaFinalIntensity;
            toFaint.g *= nebulaFinalIntensity;
            toFaint.b *= nebulaFinalIntensity;

            var c = Color.Lerp(startTint, toFaint, fadeU);
            c.a = startTint.a;
            nebulaMat.SetColor(SGT_Tint, c);
        }

        //Starfield brightness only AFTER simplification is activated
        if (starfield != null && starfieldSimplificationActive)
        {
            starfield.Brightness = Mathf.Lerp(
                starfield.Brightness,
                releaseStarBrightness,
                Time.deltaTime * starBrightnessLerpSpeed
            );
        }

        if (t >= duration)
        {
            if (!keepNebulaActiveAtEnd &&
                disableNebulaAtEnd &&
                nebula != null &&
                nebulaWasActiveAtStart)
            {
                nebula.gameObject.SetActive(false);
            }

            releaseActive = false;

            if (endSessionWhenFinished)
            {
                onSessionEnded?.Invoke();
                Debug.Log("Release finished → session ended");
            }
            else
            {
                Debug.Log("Release finished → holding end-state (galaxy stays)");
            }
        }
    }

    private float GetDuration()
    {
        if (galaxyReveal != null) return galaxyReveal.revealDuration;
        return releaseDuration;
    }

    private void BeginRelease()
    {
        releaseActive = true;
        t = 0f;

        // allow galaxy script to decide WHEN to simplify starfield
        ArmStarfieldSimplification();

        if (nebula != null)
        {
            nebulaWasActiveAtStart = nebula.gameObject.activeInHierarchy;

            startDisp = nebula.Displacement;
            startFreq = nebula.Frequency;
            startFlat = nebula.Flattening;

            var model = nebula.GetComponentInChildren<SgtNebulaModel>(true);
            if (model != null)
            {
                var r = model.GetComponent<Renderer>();
                if (r != null)
                {
                    nebulaMat = r.material;
                    if (nebulaMat != null && nebulaMat.HasProperty(SGT_Tint))
                        startTint = nebulaMat.GetColor(SGT_Tint);
                }
            }
        }

        Debug.Log("Release started → soften nebula + fade to faint (starfield simplification delayed)");
    }

    public void ArmStarfieldSimplification()
    {
        starfieldSimplificationArmed = true;
        starfieldSimplificationActive = false;
    }

    public void ApplyReleaseStarfieldLookNow()
    {
        if (!starfieldSimplificationArmed || starfield == null) return;

        int targetCount = Mathf.RoundToInt(releaseStarCount / (float)updateStep) * updateStep;
        targetCount = Mathf.Max(0, targetCount);

        starfield.StarCount = targetCount;
        starfieldSimplificationActive = true; // start brightness lerp now

        //clear the "freeze" so the starfield controller won't fight us
        if (meditation != null)
            meditation.hugeStarsActive = false;

        Debug.Log($"Starfield simplified at galaxy start: StarCount={targetCount}, Brightness→{releaseStarBrightness}");
    }
}
