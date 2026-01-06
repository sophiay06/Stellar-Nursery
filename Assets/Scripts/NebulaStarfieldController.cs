using UnityEngine;
using SpaceGraphicsToolkit.Starfield;

public class NebulaStarfieldController : MonoBehaviour
{
    [Header("Inputs")]
    public NebulaCompression nebulaCompression;
    public SgtStarfieldInfinite starfield;

    [Header("Phase")]
    public MeditationStateController meditation;

    [Header("Star count range")]
    public int minStarCount = 5000;
    public int maxStarCount = 50000;

    [Header("Calm band on compression")]
    [Range(0f, 1f)] public float idealMin = 0.3f;
    [Range(0f, 1f)] public float idealMax = 0.7f;

    // to avoid regenerating the mesh every frame
    public int updateStep = 500;
    private int currentStarCount;

    void Start()
    {
        if (starfield != null)
            currentStarCount = starfield.StarCount;
    }

    void Update()
    {
        if (nebulaCompression == null || starfield == null) return;

        //dont shrink starfield during huge stars or Release
        if (meditation != null)
        {
            if (meditation.currentPhase == MeditationPhase.Release)
                return;

            if (meditation.hugeStarsActive)
                return;
        }

        float c = nebulaCompression.compression;

        float center = (idealMin + idealMax) * 0.5f;
        float halfWidth = (idealMax - idealMin) * 0.5f;
        float dist = Mathf.Abs(c - center);

        float calmAmount = 0f;
        if (halfWidth > 0f)
            calmAmount = Mathf.Clamp01(1f - dist / halfWidth);

        int targetCount = Mathf.RoundToInt(
            Mathf.Lerp(minStarCount, maxStarCount, calmAmount)
        );

        targetCount = Mathf.RoundToInt(targetCount / (float)updateStep) * updateStep;

        if (targetCount != currentStarCount)
        {
            currentStarCount = targetCount;
            starfield.StarCount = currentStarCount;
        }
    }
}
