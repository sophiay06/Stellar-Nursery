using UnityEngine;
using SpaceGraphicsToolkit.Starfield;

[ExecuteAlways]
public class StarfieldColorScale : MonoBehaviour
{
    [Header("Target")]
    public SgtStarfieldInfinite starfield;

    [Header("Manual Scale (0..1)")]
    [Range(0f, 1f)] public float t = 0f;

    [Header("Color Endpoints")]
    public Color coolColor = new Color(0.75f, 0.90f, 1.00f, 1f); // slightly blue-white
    public Color warmColor = new Color(1.00f, 0.90f, 0.75f, 1f); // slightly warm-white

    [Header("Brightness Endpoints")]
    public float coolBrightness = 1.0f;
    public float warmBrightness = 1.2f;

    [Header("Smoothing")]
    [Tooltip("0 = no smoothing, higher = smoother")]
    public float lerpSpeed = 6f;

    private float smoothedT;

    void OnEnable()
    {
        if (starfield == null) starfield = GetComponent<SgtStarfieldInfinite>();
        smoothedT = t;
        ApplyImmediate(t);
    }

    void Update()
    {
        if (starfield == null) return;

        // Smooth in editor + play mode
        smoothedT = Mathf.Lerp(smoothedT, t, Time.deltaTime * Mathf.Max(0.01f, lerpSpeed));
        ApplyImmediate(smoothedT);
    }

    private void ApplyImmediate(float u)
    {
        u = Mathf.Clamp01(u);

        // Keep alpha = 1 so don’t accidentally “fade the whole starfield out”
        Color c = Color.Lerp(coolColor, warmColor, u);
        c.a = 1f;

        starfield.Color = c;
        starfield.Brightness = Mathf.Lerp(coolBrightness, warmBrightness, u);
    }
}
