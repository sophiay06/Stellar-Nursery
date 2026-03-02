using System.Collections.Generic;
using UnityEngine;
using Signals;

namespace MappingTool
{
    public class MappingController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Assign a component that implements ISignalProvider (e.g., SignalProvider).")]
        public MonoBehaviour signalProviderComponent;

        [SerializeField] private StellarOutputAdapter outputAdapter;

        [Header("Study condition")]
        public MappingCondition activeCondition = MappingCondition.DiminishedSelf;

        [Tooltip("Assign preset assets for DS, NaiveLLM, Expert.")]
        public List<MappingPreset> presets = new List<MappingPreset>();

        [Header("Editable bindings (the designer tool)")]
        public List<MappingBinding> bindings = new List<MappingBinding>();

        [Header("Runtime")]
        public bool runMapping = true;

        [Tooltip("If true, automatically load the preset that matches activeCondition.")]
        public bool autoLoadPreset = true;

        [Tooltip("If true, re-load preset whenever activeCondition changes (works in Play Mode).")]
        public bool reloadPresetOnConditionChange = true;

        [Header("Legacy exact behavior (NebulaCompressionFromHandDistance)")]
        public bool useLegacyNebulaCompression = true;

        [Tooltip("Uses HandDistanceStabilityXRHands.distance01 and .tracked exactly like the old script.")]
        public HandDistanceStabilityXRHands legacyHandDistance;

        [Tooltip("If true: smaller distance01 => higher compression (same as old script).")]
        public bool legacyInvert = true;

        [Tooltip("Same meaning as NebulaCompressionFromHandDistance.lerpSpeed.")]
        public float legacyLerpSpeed = 2.5f;

        [Tooltip("Same meaning as NebulaCompressionFromHandDistance.freezeWhenNotTracked.")]
        public bool legacyFreezeWhenNotTracked = true;

        // -----------------------------
        // Dust boost (makes breathing feel bigger)
        // -----------------------------
        [Header("Dust boost")]
        [Tooltip("Extra nonlinear boost applied ONLY to DustOscAmplitude after mapping (0 = off).")]
        [Range(0f, 3f)] public float dustPostGain = 1.8f;

        [Tooltip("<1 boosts small values; 1 = linear; >1 reduces small values.")]
        [Range(0.25f, 2f)] public float dustPostGamma = 0.65f;

        [Tooltip("Minimum dust amplitude sent when mapped value is >0 (helps subtle breathing show up).")]
        [Range(0f, 1f)] public float dustPostMin = 0.08f;

        private ISignalProvider _signals;

        // Store per-output smoothing state
        private readonly Dictionary<OutputParam, float> _prevOutput = new();
        private readonly Dictionary<OutputParam, float> _outputThisFrame = new();

        private MappingCondition _lastCondition;

        void Awake()
        {
            _signals = signalProviderComponent as ISignalProvider;
            if (_signals == null && signalProviderComponent != null)
                Debug.LogError("[MappingController] signalProviderComponent does not implement ISignalProvider.", this);

            if (!outputAdapter)
                outputAdapter = GetComponent<StellarOutputAdapter>();

            if (!outputAdapter)
                Debug.LogError("[MappingController] StellarOutputAdapter not found on the same GameObject.", this);

            if (!legacyHandDistance)
                legacyHandDistance = FindBestHandDistanceSource();

            _lastCondition = activeCondition;
        }

        void Start()
        {
            if (autoLoadPreset)
            {
                LoadActivePreset();
                AutoConfigureLegacyForCondition(activeCondition);
            }
        }

        private void Reset()
        {
            outputAdapter = GetComponent<StellarOutputAdapter>();
        }

        private void OnValidate()
        {
            if (!outputAdapter)
                outputAdapter = GetComponent<StellarOutputAdapter>();

            if (!Application.isPlaying && autoLoadPreset)
            {
                LoadActivePreset();
                AutoConfigureLegacyForCondition(activeCondition);
                _lastCondition = activeCondition;
            }
        }

        private HandDistanceStabilityXRHands FindBestHandDistanceSource()
        {
            var all = Resources.FindObjectsOfTypeAll<HandDistanceStabilityXRHands>();

            foreach (var h in all)
            {
                if (h == null) continue;
                if (!h.enabled) continue;
                if (!h.gameObject.scene.IsValid()) continue;
                if (!h.gameObject.activeInHierarchy) continue;
                return h;
            }

            foreach (var h in all)
            {
                if (h == null) continue;
                if (!h.gameObject.scene.IsValid()) continue;
                return h;
            }

            Debug.LogError("[MappingController] No HandDistanceStabilityXRHands found in scene.", this);
            return null;
        }

        private void AutoConfigureLegacyForCondition(MappingCondition condition)
        {
            if (condition == MappingCondition.NaiveLLM)
                useLegacyNebulaCompression = false;
        }

        private void ApplyLegacyNebulaCompression()
        {
            if (!useLegacyNebulaCompression || outputAdapter == null || legacyHandDistance == null) return;

            float dt = Time.deltaTime;

            if (!legacyHandDistance.tracked)
            {
                if (!legacyFreezeWhenNotTracked)
                {
                    float current = outputAdapter.nebulaCompression01;
                    float next = Mathf.Lerp(current, 0f, dt * Mathf.Max(0.01f, legacyLerpSpeed));
                    outputAdapter.ApplyNebulaCompression(next);
                }
                return;
            }

            float x = legacyHandDistance.distance01;
            float target = legacyInvert ? (1f - x) : x;

            float prev = outputAdapter.nebulaCompression01;
            float smoothed = Mathf.Lerp(prev, target, dt * Mathf.Max(0.01f, legacyLerpSpeed));

            outputAdapter.ApplyNebulaCompression(smoothed);
        }

        void Update()
        {
            // Reload preset when activeCondition changes in play mode
            if (reloadPresetOnConditionChange && activeCondition != _lastCondition)
            {
                _lastCondition = activeCondition;

                if (autoLoadPreset)
                    LoadActivePreset();

                AutoConfigureLegacyForCondition(activeCondition);

                _prevOutput.Clear();
                _outputThisFrame.Clear();

                Debug.Log($"[MappingController] Condition changed -> {activeCondition}. Preset reloaded.");
            }

            ApplyLegacyNebulaCompression();

            if (!runMapping || outputAdapter == null || _signals == null) return;

            _outputThisFrame.Clear();

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (!_signals.TryGetSignal(b.inputSignal, out float raw)) continue;

                b.debugRaw = raw;

                float center = (b.inputMin + b.inputMax) * 0.5f;
                raw = MappingMath.ApplyDeadzone(raw, center, b.deadzone);

                float t = MappingMath.Remap01(raw, b.inputMin, b.inputMax);
                if (b.invert) t = 1f - t;

                t = MappingMath.ApplyPreset(t, b.curvePreset);

                float mapped = MappingMath.Lerp(b.outputMin, b.outputMax, t);

                float prev = _prevOutput.TryGetValue(b.outputParam, out float p) ? p : mapped;
                float smoothed = MappingMath.LowPass(prev, mapped, b.smoothing);

                // Rate limit
                float maxDeltaPerSec = 1.0f;
                float maxDelta = maxDeltaPerSec * Time.deltaTime;
                smoothed = Mathf.Clamp(smoothed, prev - maxDelta, prev + maxDelta);

                // -----------------------------
                // dust boost
                // -----------------------------
                // if (b.outputParam == OutputParam.DustOscAmplitude)
                //     smoothed = BoostDust(smoothed);

                b.debugMapped = smoothed;

                _outputThisFrame[b.outputParam] = smoothed;
                _prevOutput[b.outputParam] = smoothed;
            }

            // Apply outputs; if legacy nebula is on, don't overwrite it
            if (!useLegacyNebulaCompression)
                ApplyOutput(OutputParam.NebulaCompression, 0.5f);

            ApplyOutput(OutputParam.StarColorHue, 0.5f);

            // fallback: higher so dust isn't tiny by default
            ApplyOutput(OutputParam.DustOscAmplitude, 0.6f);
        }

        private float BoostDust(float v01)
        {
            v01 = Mathf.Clamp01(v01);

            // If the value is basically zero, keep it zero (avoid constant motion unless you want it)
            if (v01 <= 1e-5f) return 0f;

            // Gamma (<1 boosts small)
            float x = Mathf.Pow(v01, Mathf.Max(1e-3f, dustPostGamma));

            // Gain then clamp
            x = Mathf.Clamp01(x * Mathf.Max(0f, dustPostGain));

            // Ensure minimum visible motion once nonzero
            x = Mathf.Max(x, dustPostMin);

            return Mathf.Clamp01(x);
        }

        private void ApplyOutput(OutputParam param, float fallback)
        {
            float v = _outputThisFrame.TryGetValue(param, out float val) ? val : fallback;

            switch (param)
            {
                case OutputParam.NebulaCompression:
                    outputAdapter.ApplyNebulaCompression(v);
                    break;

                case OutputParam.StarColorHue:
                    outputAdapter.ApplyStarColorHue(v);
                    break;

                case OutputParam.DustOscAmplitude:
                    outputAdapter.ApplyDustOscAmplitude(v);
                    break;
            }
        }

        public void LoadActivePreset()
        {
            var preset = presets.Find(p => p && p.condition == activeCondition);
            if (!preset)
            {
                Debug.LogWarning($"[MappingController] No preset found for {activeCondition}.", this);
                return;
            }

            bindings = new List<MappingBinding>();
            foreach (var b in preset.bindings)
            {
                var copy = new MappingBinding
                {
                    inputSignal = b.inputSignal,
                    outputParam = b.outputParam,
                    inputMin = b.inputMin,
                    inputMax = b.inputMax,
                    outputMin = b.outputMin,
                    outputMax = b.outputMax,
                    invert = b.invert,
                    curvePreset = b.curvePreset,
                    smoothing = b.smoothing,
                    deadzone = b.deadzone
                };
                bindings.Add(copy);
            }

            Debug.Log($"[MappingController] Loaded preset: {preset.name} ({activeCondition})", this);
        }

        public void SetRecommendedDefaultsAll()
        {
            switch (activeCondition)
            {
                case MappingCondition.DiminishedSelf:
                    bindings = new List<MappingBinding>
                    {
                        new MappingBinding
                        {
                            outputParam = OutputParam.NebulaCompression,
                            inputSignal = InputSignal.HandsHorizontalDistance01,
                            inputMin = 0f,
                            inputMax = 1f,
                            outputMin = 0f,
                            outputMax = 1f,
                            curvePreset = CurvePreset.EaseInOut,
                            smoothing = 0.95f,
                            deadzone = 0f,
                            invert = true
                        },
                        new MappingBinding
                        {
                            outputParam = OutputParam.StarColorHue,
                            inputSignal = InputSignal.RightWristPronation01,
                            inputMin = 0f,
                            inputMax = 1f,
                            outputMin = 0f,
                            outputMax = 1f,
                            curvePreset = CurvePreset.Linear,
                            smoothing = 0.90f,
                            deadzone = 0.02f,
                            invert = false
                        },
                        new MappingBinding
                        {
                            outputParam = OutputParam.DustOscAmplitude,
                            inputSignal = InputSignal.TorsoRespFreq01,
                            inputMin = 0f,
                            inputMax = 1f,
                            outputMin = 0.10f,
                            outputMax = 1.00f,
                            curvePreset = CurvePreset.EaseOut,
                            smoothing = 0.80f,
                            deadzone = 0.01f,
                            invert = false
                        }
                    };
                    break;

                case MappingCondition.NaiveLLM:
                    bindings = new List<MappingBinding>
                    {
                        new MappingBinding
                        {
                            outputParam = OutputParam.NebulaCompression,
                            inputSignal = InputSignal.HandsHorizontalDistance01,
                            inputMin = 0f,
                            inputMax = 1f,
                            outputMin = 0f,
                            outputMax = 1f,
                            curvePreset = CurvePreset.EaseInOut,
                            smoothing = 0.95f,
                            deadzone = 0f,
                            invert = true
                        },
                        new MappingBinding
                        {
                            outputParam = OutputParam.DustOscAmplitude,
                            inputSignal = InputSignal.HeadPitchDeg,
                            inputMin = -25f,
                            inputMax =  25f,
                            outputMin = 0.20f,
                            outputMax = 0.90f,
                            curvePreset = CurvePreset.EaseOut,
                            smoothing = 0.95f,
                            deadzone = 2.0f,
                            invert = false
                        }
                    };
                    break;

                case MappingCondition.Expert:
                    bindings = new List<MappingBinding>
                    {
                        new MappingBinding
                        {
                            outputParam = OutputParam.NebulaCompression,
                            inputSignal = InputSignal.TorsoRespFreq01,
                            inputMin = 0f,
                            inputMax = 1f,
                            outputMin = 0f,
                            outputMax = 1f,
                            curvePreset = CurvePreset.EaseInOut,
                            smoothing = 0.95f,
                            deadzone = 0f,
                            invert = true
                        },
                        new MappingBinding
                        {
                            outputParam = OutputParam.StarColorHue,
                            inputSignal = InputSignal.ArmsRaise01,
                            inputMin = -25f,
                            inputMax =  25f,
                            outputMin = 0.20f,
                            outputMax = 0.90f,
                            curvePreset = CurvePreset.EaseOut,
                            smoothing = 0.95f,
                            deadzone = 2.0f,
                            invert = false
                        }
                    };
                    break;
            }

            Debug.Log($"[MappingController] Set recommended defaults for {activeCondition}");
        }

    }
}
