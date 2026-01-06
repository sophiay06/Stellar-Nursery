using System.Collections;
using UnityEngine;

public class HugeStarsController : MonoBehaviour
{
    [Header("Startup")]
    public bool hideAtStart = true;

    [Header("Fade In")]
    public float fadeInDuration = 5.0f;

    public Color startColor = new Color(0.55f, 0.70f, 0.95f, 1f);
    public Color endColor = Color.white;

    public float startBrightness = 0.0f;
    public float endBrightness = 1.0f;

    public bool useSmoothStep = true;

    [Header("Also tint SpriteRenderer.color")]
    public bool driveSpriteRendererColor = true;

    private SpriteRenderer[] spriteRenderers;
    private MaterialPropertyBlock mpb;
    private Coroutine routine;

    private static readonly int SGT_Color = Shader.PropertyToID("_SGT_Color");
    private static readonly int SGT_Brightness = Shader.PropertyToID("_SGT_Brightness");

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (hideAtStart)
            gameObject.SetActive(false);
        else
            ApplyToAll(startColor, startBrightness);
    }

    public void Show()
    {
        if (routine != null) StopCoroutine(routine);

        gameObject.SetActive(true);
        ApplyToAll(startColor, startBrightness);

        routine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float d = Mathf.Max(0.01f, fadeInDuration);
        float t = 0f;

        while (t < d)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            if (useSmoothStep) u = Mathf.SmoothStep(0f, 1f, u);

            Color c = Color.Lerp(startColor, endColor, u);
            c.a = 1f; // additive: keep alpha stable

            float b = Mathf.Lerp(startBrightness, endBrightness, u);

            ApplyToAll(c, b);
            yield return null;
        }

        Color finalC = endColor; finalC.a = 1f;
        ApplyToAll(finalC, endBrightness);

        routine = null;
    }

    private void ApplyToAll(Color c, float brightness)
    {
        if (spriteRenderers == null || mpb == null) return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr == null) continue;

            // SpriteRenderer tint  multiplies the final output
            if (driveSpriteRendererColor)
                sr.color = c;

            sr.GetPropertyBlock(mpb);
            mpb.SetColor(SGT_Color, c);
            mpb.SetFloat(SGT_Brightness, brightness);
            sr.SetPropertyBlock(mpb);
        }
    }
}
