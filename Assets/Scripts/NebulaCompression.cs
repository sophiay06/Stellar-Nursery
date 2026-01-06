using UnityEngine;
using SpaceGraphicsToolkit.Nebula;

public class NebulaCompression : MonoBehaviour
{
    public SgtNebula nebula;

    [Range(0f, 1f)]
    public float compression = 0f; // 0 = expanded, 1 = compressed

    public float minRadius = 300f;
    public float maxRadius = 600f;

    public float minDisplacement = 20f;
    public float maxDisplacement = 100f;

    public float minFlatten = 0f;
    public float maxFlatten = 0.6f;

    void Update()
    {
        if (nebula == null) return;

        nebula.Radius       = Mathf.Lerp(maxRadius,      minRadius,      compression);
        nebula.Displacement = Mathf.Lerp(maxDisplacement,minDisplacement,compression);
        nebula.Flattening   = Mathf.Lerp(minFlatten,     maxFlatten,     compression);
    }
}
