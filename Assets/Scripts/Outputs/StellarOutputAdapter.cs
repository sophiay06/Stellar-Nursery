using UnityEngine;
using SpaceGraphicsToolkit;

[DisallowMultipleComponent]
public class StellarOutputAdapter : MonoBehaviour
{
    [Header("Runtime outputs (debug)")]
    [Range(0f, 1f)] public float nebulaCompression01 = 0.5f;
    [Range(0f, 1f)] public float starColor01 = 0.5f;

    [Range(0f, 1f)] public float dustAmplitude01 = 0.3f;

    [Header("Existing systems (auto-found if left empty)")]
    public NebulaCompression nebulaCompressionController;
    public StarfieldColorScale starfieldColorScale;

    public SgtBackgroundMesh dustBackgroundMesh;

    [Header("Dust response boost")]
    [Range(0.2f, 2f)] public float dustGamma = 0.65f;
    public float dustGain = 2.5f;
    [Range(0f, 1f)] public float dustMinOut = 0.15f;
    [Range(0f, 1f)] public float dustMaxOut = 1.00f;

    void Reset() => AutoFind();
    void OnValidate() => AutoFind();

    private void AutoFind()
    {
        if (!nebulaCompressionController)
            nebulaCompressionController = FindObjectOfType<NebulaCompression>(true);

        if (!starfieldColorScale)
            starfieldColorScale = FindObjectOfType<StarfieldColorScale>(true);

        // if (!dustSquashController)
        //     dustSquashController = FindObjectOfType<DustSquashFromH10BreathOSC>(true);

        if (!dustBackgroundMesh)
            dustBackgroundMesh = FindObjectOfType<SgtBackgroundMesh>(true); // <-- add this
    }

    public void ApplyNebulaCompression(float v01)
    {
        nebulaCompression01 = Mathf.Clamp01(v01);
        if (nebulaCompressionController != null)
            nebulaCompressionController.compression = nebulaCompression01;
    }

    public void ApplyStarColorHue(float v01)
    {
        starColor01 = Mathf.Clamp01(v01);
        if (starfieldColorScale != null)
            starfieldColorScale.t = starColor01;
    }

    public void ApplyDustOscAmplitude(float v01)
    {
        dustAmplitude01 = Mathf.Clamp01(v01);

        if (dustBackgroundMesh == null)
        {
            Debug.LogWarning("[StellarOutputAdapter] dustBackgroundMesh is NULL (assign the SgtBackgroundMesh that renders your dust).");
            return;
        }

        // float t = dustAmplitude01;
        // t = Mathf.Pow(t, Mathf.Max(1e-3f, dustGamma));

        // float out01 = Mathf.Lerp(dustMinOut, dustMaxOut, t);
        // out01 = Mathf.Clamp(out01, dustMinOut, dustMaxOut);

        // dustBackgroundMesh.Squash = out01;
        dustBackgroundMesh.Squash = dustAmplitude01;
    }
}