using UnityEngine;
using SpaceGraphicsToolkit.Nebula;

public class NebulaColorShiftController : MonoBehaviour
{
    [Header("References")]
    public MeditationStateController meditation;
    public NebulaCompression nebulaCompression;
    public SgtNebula nebula; 

    [Header("ColorShift targets per phase")]
    [Range(0f, 1f)] public float arrivalShift01  = 0.05f;
    [Range(0f, 1f)] public float balanceShift01  = 0.50f;
    [Range(0f, 1f)] public float releaseShift01  = 0.80f;

    [Header("Balance subtle modulation by compression")]
    public bool modulateInBalance = true;
    public float calmMin = 0.30f;
    public float calmMax = 0.70f;
    [Range(0f, 1f)] public float balanceModRange01 = 0.05f;

    [Header("Smoothing")]
    public float lerpSpeed = 0.8f;

    static readonly int ColorShiftID = Shader.PropertyToID("_SGT_ColorShift");
    Material mat;
    float currentRadians;

    void Start()
    {
        if (nebula == null) nebula = GetComponent<SgtNebula>();
        if (nebula != null) mat = nebula.SourceMaterial;

        if (mat != null)
            currentRadians = mat.GetFloat(ColorShiftID);
    }

    void Update()
    {
        if (meditation == null || mat == null) return;

        float target01 = arrivalShift01;

        if (meditation.currentPhase == MeditationPhase.Balance)
        {
            target01 = balanceShift01;

            if (modulateInBalance && nebulaCompression != null)
            {
                float c = nebulaCompression.compression;
                float center = (calmMin + calmMax) * 0.5f;
                float half   = (calmMax - calmMin) * 0.5f;

                if (half > 0f)
                {
                    float calm01 = Mathf.Clamp01(1f - Mathf.Abs(c - center) / half);
                    target01 += Mathf.Lerp(-balanceModRange01, balanceModRange01, calm01);
                }
            }
        }
        else if (meditation.currentPhase == MeditationPhase.Release)
        {
            target01 = releaseShift01;
        }

        //map 0..1 -> 0..2π radians
        float targetRadians = Mathf.Repeat(target01, 1f) * (Mathf.PI * 2f);

        currentRadians = Mathf.Lerp(currentRadians, targetRadians, Time.deltaTime * lerpSpeed);
        mat.SetFloat(ColorShiftID, currentRadians);
    }
}
