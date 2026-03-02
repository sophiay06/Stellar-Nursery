// Assets/Scripts/Mapping/MappingMath.cs
using UnityEngine;

namespace MappingTool
{
    public enum CurvePreset
    {
        Linear,
        EaseInOut,
        EaseOut
    }

    public static class MappingMath
    {
        public static float Remap01(float x, float inMin, float inMax)
        {
            if (Mathf.Approximately(inMin, inMax)) return 0f;
            return Mathf.Clamp01((x - inMin) / (inMax - inMin));
        }

        public static float ApplyPreset(float t, CurvePreset preset)
        {
            t = Mathf.Clamp01(t);

            switch (preset)
            {
                case CurvePreset.EaseInOut:
                    return t * t * (3f - 2f * t);
                case CurvePreset.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                default:
                    return t;
            }
        }

        public static float Lerp(float outMin, float outMax, float t)
        {
            return Mathf.Lerp(outMin, outMax, Mathf.Clamp01(t));
        }

        public static float LowPass(float prev, float current, float smoothing)
        {
            smoothing = Mathf.Clamp01(smoothing);
            float alpha = 1f - smoothing;       
            return Mathf.Lerp(prev, current, alpha); 
        }


        public static float ApplyDeadzone(float x, float center, float deadzone)
        {
            if (deadzone <= 0f) return x;
            return (Mathf.Abs(x - center) <= deadzone) ? center : x;
        }
    }
}
