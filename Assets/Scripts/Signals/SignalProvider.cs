using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using MappingTool;
using Unity.XR.CoreUtils;

namespace Signals
{
    public class SignalProvider : MonoBehaviour, ISignalProvider
    {
        [Header("Scene refs")]
        public XROrigin xrOrigin;
        public Camera hmdCamera;

        [Header("XR Hands")]
        [Tooltip("If null, will auto-find a running XRHandSubsystem.")]
        public XRHandSubsystem handSubsystem;

        [Header("Naive LLM: Transform sources (preferred if assigned)")]
        public Transform leftHand;
        public Transform rightHand;
        public Transform head;

        // ============================================================
        // Hands horizontal distance (XZ) -> 01
        // ============================================================
        [Header("Hands horizontal distance (XZ) normalization")]
        public float handsHorizMinM = 0.20f;
        public float handsHorizMaxM = 0.80f;

        [Header("HandsHorizontalDistance01 smoothing")]
        public bool smoothHandsHoriz = true;
        public float handsHorizDeadZone01 = 0.01f;
        public float handsHorizHysteresis01 = 0.005f;
        public float handsHorizEmaTau = 0.12f;
        public float handsHorizMaxDeltaPerSec = 3.0f;

        private bool _handsHorizSeeded;
        private float _handsHoriz01;

        // ============================================================
        // Right wrist pronation/supination -> 01
        // ============================================================
        [Header("Right wrist pronation (rotation around hand forward axis)")]
        [Tooltip("If true, first valid pronation angle becomes baseline (0 deg). Set this when palm faces down.")]
        public bool rightWristUseBaseline = true;

        [Tooltip("Extra bias added after baseline (deg). Usually 0.")]
        public float rightWristBaselineBiasDeg = 0f;

        [Header("Map pronation delta (deg) -> 0..1")]
        [Tooltip("Negative = rotate one way, Positive = the other. Tune in play mode.")]
        public float rightWristMinDeg = -90f;
        public float rightWristMaxDeg = 90f;

        [Tooltip("Invert mapping if desired.")]
        public bool rightWristInvert = false;

        [Header("RightWristPronation01 smoothing")]
        public bool smoothRightWrist = true;
        public float rightWristDeadZone01 = 0.01f;
        public float rightWristHysteresis01 = 0.005f;
        public float rightWristEmaTau = 0.12f;
        public float rightWristMaxDeltaPerSec = 3.0f;

        private bool _rightWristBaselineSeeded;
        private float _rightWristBaselineDeg;
        private bool _rightWristSeeded;
        private float _rightWrist01;

        // ============================================================
        // Head pitch (deg) raw
        // ============================================================
        [Header("Head pitch (deg)")]
        [Tooltip("If true, use XRNode CenterEye/Head rotation. If false, use the 'head' Transform if assigned.")]
        public bool headUseXRNode = true;

        // ============================================================
        // H10 respiration (OSC)
        // ============================================================
        // [Header("H10 Respiration (OSC)")]
        // public H10BreathOscReceiver h10Breath;

        // [Header("H10 amplitude shaping (AmpRaw01 -> Amp01)")]
        // public float h10AmpDeadzone = 0.02f;
        // public float h10AmpStrong = 0.25f;
        // [Range(0.2f, 2f)] public float h10AmpGamma = 0.60f;
        // public float h10AmpGain = 1.8f;

        // [Header("H10BreathAmp01 smoothing")]
        // public bool smoothH10Amp = true;
        // public float h10DeadZone01 = 0.01f;
        // public float h10Hysteresis01 = 0.005f;
        // public float h10EmaTau = 0.12f;
        // public float h10MaxDeltaPerSec = 3.0f;

        // private bool _h10Seeded;
        // private float _h10Amp01;

        // ----------------------------
        // Chest Respiration (SlimeVR)
        // ----------------------------
        [Header("Chest Respiration (SlimeVR)")]

        public Transform chestTracker;
        public Transform waistTracker;

        [Tooltip("Expected breathing frequency range (Hz)")]
        public float respMinHz = 0.05f;   // very slow breathing
        public float respMaxHz = 0.5f;    // fast breathing

        [Tooltip("Low-pass smoothing for chest motion")]
        [Range(0f, 1f)]
        public float respSmoothing = 0.1f;

        float _smoothedChestY;
        float _lastChestY;
        float _lastPeakTime;
        float _currentHz;

        float _prevVelocity;
        float _velocity;
        float _motionEnergySmoothed;

        float _baseline;
        float _envelope;
        float _envMaxObserved = 0f;

    
        //Frequency tracking state
        private bool _aboveZero = false;
        private float _lastCrossTime = -1f;

        private bool _torsoHzSeeded = false;
        private float _torsoHzSmoothed = 0.2f; // default ~12 BPM

        // ----------------------------
        // Arduino Pressure Sensor
        // ----------------------------
        [Header("Arduino Pressure")]
        public ArduinoSerialReader arduinoPressure;

        [Tooltip("Raw min/max from Arduino for normalization")]
        public float pressureRawMin = 0f;
        public float pressureRawMax = 1023f;

        [Header("ArduinoPressure01 smoothing")]
        public bool smoothArduinoPressure = true;
        public float pressureDeadZone01 = 0.01f;
        public float pressureHysteresis01 = 0.005f;
        public float pressureEmaTau = 0.10f;
        public float pressureMaxDeltaPerSec = 4.0f;

        private bool _pressureSeeded;
        private float _pressure01;


        [Header("Debug")]
        public bool debugChestResp = false;

        // ----------------------------
        // SlimeVR left/right ankle
        // ----------------------------
        [Header("Feet Distance (Star Color - Naive Mapping)")]

        public Transform leftAnkle;
        public Transform rightAnkle;

        [Tooltip("Minimum stance width in meters (narrow stance)")]
        public float feetMinM = 0.2f;

        [Tooltip("Maximum stance width in meters (wide stance)")]
        public float feetMaxM = 0.8f;

        public bool debugFeetDistance = false;

        float _smoothedFootDistance;

        [Range(0f, 1f)]
        public float footSmoothing = 0.2f;

        // ============================================================
        // Debug
        // ============================================================
        [Header("Debug")]
        public bool debugLogs = false;
        public float debugInterval = 0.5f;
        private float _nextDebug;

        private readonly Dictionary<InputSignal, float> _signals = new();

        void OnEnable()
        {
            if (handSubsystem == null) handSubsystem = FindRunningHandSubsystem();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            UpdateHandsHorizontalDistance(dt);
            UpdateRightWristPronation(dt);
            UpdateHeadPitchDeg();
            //dateH10(dt);
            //dateTorsoRespFreq(dt);
            UpdateChestRespiration(Time.deltaTime);
            UpdateFeetDistance();
            UpdateArmRaise(dt);
            UpdateArduinoPressure(dt);

            if (debugLogs && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + Mathf.Max(0.1f, debugInterval);

                _signals.TryGetValue(InputSignal.HandsHorizontalDistance01, out var hh01);
                _signals.TryGetValue(InputSignal.HandsHorizontalDistanceM, out var hhm);

                _signals.TryGetValue(InputSignal.RightWristPronation01, out var pr01);
                _signals.TryGetValue(InputSignal.RightWristPronationDeg, out var prDeg);

                _signals.TryGetValue(InputSignal.HeadPitchDeg, out var pitchDeg);

                _signals.TryGetValue(InputSignal.TorsoRespFreq01, out var freq01);

                _signals.TryGetValue(InputSignal.H10BreathAmp01, out var h10a01);
                _signals.TryGetValue(InputSignal.H10BreathAmpRaw01, out var h10raw);
                _signals.TryGetValue(InputSignal.H10BreathWave01, out var h10w);

                _signals.TryGetValue(InputSignal.ArmsRaise01, out var arms);

                _signals.TryGetValue(InputSignal.ArduinoPressure01, out var pressure01);
                _signals.TryGetValue(InputSignal.ArduinoPressureRaw, out var pressureRaw);

                Debug.Log(
                    $"ArmsRaise01 = {arms:F2} | " +
                    $"[SignalProvider_App2] HandsHoriz: {hh01:F2} (m={hhm:F3}) | " +
                    $"WristPron: {pr01:F2} (deg={prDeg:F1}) | " +
                    $"HeadPitchDeg={pitchDeg:F1} | " +
                    $"H10 Amp01={h10a01:F2} raw={h10raw:F3} wave={h10w:F2}"
                );
            }
        }

        public bool TryGetSignal(InputSignal signal, out float value)
            => _signals.TryGetValue(signal, out value);

        // ============================================================
        // 1) HandsHorizontalDistance01
        // ============================================================
        void UpdateHandsHorizontalDistance(float dt)
        {
            float dMeters;
            bool ok = TryGetHandsHorizontalDistanceMeters(out dMeters);

            // DebugOnly raw
            _signals[InputSignal.HandsHorizontalDistanceM] = ok ? dMeters : 0f;

            float target01 = 0f;
            if (ok)
                target01 = Mathf.Clamp01(Mathf.InverseLerp(handsHorizMinM, handsHorizMaxM, dMeters));

            if (!smoothHandsHoriz)
            {
                _handsHorizSeeded = true;
                _handsHoriz01 = Mathf.Clamp01(target01);
            }
            else
            {
                _handsHoriz01 = Filter01(ref _handsHorizSeeded, _handsHoriz01, target01, dt,
                    handsHorizDeadZone01, handsHorizHysteresis01, handsHorizEmaTau, handsHorizMaxDeltaPerSec);
            }

            _signals[InputSignal.HandsHorizontalDistance01] = _handsHoriz01;
        }

        bool TryGetHandsHorizontalDistanceMeters(out float dMeters)
{
    dMeters = 0f;

    if (!EnsureHandSubsystemRunning())
        return false;

    var lh = handSubsystem.leftHand;
    var rh = handSubsystem.rightHand;

    if (!lh.isTracked || !rh.isTracked)
        return false;

    var lj = lh.GetJoint(XRHandJointID.Wrist);
    var rj = rh.GetJoint(XRHandJointID.Wrist);

    if (!lj.TryGetPose(out Pose lp) || !rj.TryGetPose(out Pose rp))
        return false;

    Vector3 a = lp.position;
    Vector3 b = rp.position;

    // Remove vertical component (horizontal abduction only)
    a.y = 0f;
    b.y = 0f;

    dMeters = Vector3.Distance(a, b);

    return true;
}



        // ============================================================
        // 2) RightWristPronation01 (+ DebugOnly degrees)
        // Baseline: set when palm faces down
        // ============================================================
        void UpdateRightWristPronation(float dt)
        {
            float pronDeg;
            bool ok = TryGetRightWristPronationDeg(out pronDeg);

            // DebugOnly raw degrees
            _signals[InputSignal.RightWristPronationDeg] = ok ? pronDeg : 0f;

            if (!ok)
            {
                _signals[InputSignal.RightWristPronation01] = 0f;
                return;
            }

            if (rightWristUseBaseline)
            {
                if (!_rightWristBaselineSeeded)
                {
                    _rightWristBaselineSeeded = true;
                    _rightWristBaselineDeg = pronDeg; // palm-down becomes 0
                }

                pronDeg = Mathf.DeltaAngle(_rightWristBaselineDeg, pronDeg);
            }

            pronDeg += rightWristBaselineBiasDeg;

            float minD = Mathf.Min(rightWristMinDeg, rightWristMaxDeg);
            float maxD = Mathf.Max(rightWristMinDeg, rightWristMaxDeg);

            float clamped = Mathf.Clamp(pronDeg, minD, maxD);
            float t01 = Mathf.InverseLerp(minD, maxD, clamped);
            if (rightWristInvert) t01 = 1f - t01;

            float target01 = Mathf.Clamp01(t01);

            if (!smoothRightWrist)
            {
                _rightWristSeeded = true;
                _rightWrist01 = target01;
            }
            else
            {
                _rightWrist01 = Filter01(ref _rightWristSeeded, _rightWrist01, target01, dt,
                    rightWristDeadZone01, rightWristHysteresis01, rightWristEmaTau, rightWristMaxDeltaPerSec);
            }

            _signals[InputSignal.RightWristPronation01] = _rightWrist01;
        }

        // - Use wrist rotation, measure rotation around the wrist's forward axis relative to world up.
        // - Stable against yaw/pitch
        bool TryGetRightWristPronationDeg(out float deg)
        {
            deg = 0f;
            if (!EnsureHandSubsystemRunning()) return false;

            XRHand right = handSubsystem.rightHand;
            if (!right.isTracked) return false;

            XRHandJoint wrist = right.GetJoint(XRHandJointID.Wrist);
            if (!wrist.TryGetPose(out Pose wristPose)) return false;

            Quaternion rot = wristPose.rotation;

            Vector3 fwd = rot * Vector3.forward;
            Vector3 up  = rot * Vector3.up;

            // Reference "no twist": world up projected into plane orthogonal to fwd
            Vector3 upRef = Vector3.ProjectOnPlane(Vector3.up, fwd);
            if (upRef.sqrMagnitude < 1e-8f) return false;
            upRef.Normalize();

            // Actual wrist up projected into same plane
            Vector3 upAct = Vector3.ProjectOnPlane(up, fwd);
            if (upAct.sqrMagnitude < 1e-8f) return false;
            upAct.Normalize();

            deg = Vector3.SignedAngle(upRef, upAct, fwd); // [-180,180]
            return true;
        }

        // ============================================================
        // 3) HeadPitchDeg (raw)
        // Naive LLM: dust oscillation density <- head pitch angle
        // ============================================================
        void UpdateHeadPitchDeg()
        {
            float pitchDeg = 0f;

            if (headUseXRNode)
            {
                if (TryGetHmdRotation(out Quaternion rot))
                    pitchDeg = GetSignedPitchDeg(rot);
            }
            else
            {
                Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
                if (h != null) pitchDeg = GetSignedPitchDeg(h.rotation);
            }

            _signals[InputSignal.HeadPitchDeg] = pitchDeg;
        }

        static float GetSignedPitchDeg(Quaternion q)
        {
            float pitch = q.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            return pitch;
        }

        static bool TryGetHmdRotation(out Quaternion rot)
        {
            rot = Quaternion.identity;

            // 1) Try InputDevices
            var dev = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (dev.isValid && dev.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                return true;

            dev = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (dev.isValid && dev.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                return true;

            // 2) Fallback: XRNodeState (often works when above fails)
            var states = new List<XRNodeState>();
            InputTracking.GetNodeStates(states);

            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (s.nodeType == XRNode.CenterEye || s.nodeType == XRNode.Head)
                {
                    if (s.TryGetRotation(out rot))
                        return true;
                }
            }

            return false;
        }

        void UpdateChestRespiration(float dt)
        {
            if (chestTracker == null)
            {
                if (debugChestResp) Debug.LogWarning("[ChestResp] Chest tracker is NULL.");
                return;
            }

        // -----------------------------
        // 1) Raw relative chest-waist pitch (deg), wrap-safe
        // -----------------------------
        float chestPitch = chestTracker.eulerAngles.x;
        float waistPitch = (waistTracker != null) ? waistTracker.eulerAngles.x : 0f;
        float rawSignal = Mathf.DeltaAngle(waistPitch, chestPitch); // deg

        // -----------------------------
        // 2) Baseline removal (dt-correct "very slow")
        // -----------------------------
        // baselineTauSec: larger = slower baseline drift removal
        const float baselineTauSec = 8.0f; 
        float aBase = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, baselineTauSec));
        _baseline = Mathf.Lerp(_baseline, rawSignal, aBase);
        float centered = rawSignal - _baseline; // deg

        const float centeredDeadzoneDeg = 0.15f;
        float centeredDz = (Mathf.Abs(centered) < centeredDeadzoneDeg) ? 0f : centered;

        // -----------------------------
        // 3) Amplitude envelope (rectified centered)
        // -----------------------------
        float rectified = Mathf.Abs(centeredDz);

        // Attack/decay in seconds (dt-correct)
        const float envAttackTau = 0.25f;
        const float envDecayTau  = 0.60f;

        float aAtk = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, envAttackTau));
        float aDec = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, envDecayTau));

        if (rectified > _envelope) _envelope = Mathf.Lerp(_envelope, rectified, aAtk);
        else                       _envelope = Mathf.Lerp(_envelope, rectified, aDec);

        // Dynamic normalization for amplitude
        _envMaxObserved = Mathf.Max(_envMaxObserved * 0.999f, _envelope);
        float amp01 = (_envMaxObserved > 1e-4f)
            ? Mathf.Clamp01(_envelope / _envMaxObserved)
            : 0f;

        _signals[InputSignal.TorsoRespAmplitude01] = amp01;

        // -----------------------------
        // 4) Frequency estimate via rising zero-crossings
        // -----------------------------
        bool nowAbove = centeredDz > 0f;
        bool risingCross = (!_aboveZero) && nowAbove;
        _aboveZero = nowAbove;

        float hzRaw = _currentHz;

        if (risingCross)
        {
            float t = Time.time;
            if (_lastCrossTime > 0f)
            {
                float period = t - _lastCrossTime;

                if (period > 0.5f && period < 20f)
                {
                    hzRaw = 1f / period;
                    hzRaw = Mathf.Clamp(hzRaw, respMinHz, respMaxHz);
                    _currentHz = hzRaw;
                }
            }
            _lastCrossTime = t;
        }

        // Smooth frequency
        const float freqTauSec = 0.6f; // smaller = more responsive, larger = smoother
        float aHz = 1f - Mathf.Exp(-dt / Mathf.Max(1e-4f, freqTauSec));
        if (!_torsoHzSeeded)
        {
            _torsoHzSeeded = true;
            _torsoHzSmoothed = _currentHz > 1e-4f ? _currentHz : Mathf.Clamp(0.2f, respMinHz, respMaxHz);
        }
        else
        {
            // Only update smoothing when we have a nonzero estimate
            float hzTarget = (_currentHz > 1e-4f) ? _currentHz : _torsoHzSmoothed;
            _torsoHzSmoothed = Mathf.Lerp(_torsoHzSmoothed, hzTarget, aHz);
        }

        float outHz = Mathf.Clamp(_torsoHzSmoothed, respMinHz, respMaxHz);
        float freq01 = Mathf.Clamp01(Mathf.InverseLerp(respMinHz, respMaxHz, outHz));

        _signals[InputSignal.TorsoRespFreqHz] = outHz;
        _signals[InputSignal.TorsoRespFreq01] = freq01;

        // -----------------------------
        // 5) Debug 
        // -----------------------------
        if (debugChestResp && Time.time >= _nextDebug)
        {
            _nextDebug = Time.time + Mathf.Max(0.1f, debugInterval);

            Debug.Log(
                $"[ChestResp] rawDeg={rawSignal:F2} base={_baseline:F2} centered={centeredDz:F2} " +
                $"env={_envelope:F2} amp01={amp01:F2} " +
                $"cross={risingCross} hzRaw={hzRaw:F3} hzSm={outHz:F3} freq01={freq01:F2}"
            );
        }
    }


        private void UpdateFeetDistance()
        {
            if (leftAnkle == null || rightAnkle == null || xrOrigin == null)
            {
                if (debugFeetDistance)
                    Debug.LogWarning("[FeetDistance] Missing references.");
                return;
            }

            Transform rig = xrOrigin.transform;

            // Convert to XR Origin local space
            Vector3 leftLocal = rig.InverseTransformPoint(leftAnkle.position);
            Vector3 rightLocal = rig.InverseTransformPoint(rightAnkle.position);

            // Horizontal only
            leftLocal.y = 0f;
            rightLocal.y = 0f;

            float rawDistance = Vector3.Distance(leftLocal, rightLocal);

            _smoothedFootDistance = Mathf.Lerp(
                _smoothedFootDistance,
                rawDistance,
                1f - footSmoothing 
            );

            float normalized = Mathf.InverseLerp(
                feetMinM,
                feetMaxM,
                _smoothedFootDistance
            );

            normalized = Mathf.Clamp01(normalized);

            _signals[InputSignal.StarFeetDistance01] = normalized;
            _signals[InputSignal.StarFeetDistanceM] = rawDistance;

            if (debugFeetDistance)
            {
                Debug.Log(
                    $"[Feet Star] Raw={rawDistance:F3}m | Smooth={_smoothedFootDistance:F3} | Norm={normalized:F3}"
                );
            }
        }

        void UpdateArmRaise(float dt)
        {
            if (!EnsureHandSubsystemRunning()) return;

            float left01  = GetSingleArmRaise(handSubsystem.leftHand);
            float right01 = GetSingleArmRaise(handSubsystem.rightHand);

            _signals[InputSignal.LeftArmRaise01] = left01;
            _signals[InputSignal.RightArmRaise01] = right01;
            _signals[InputSignal.ArmsRaise01] = (left01 + right01) * 0.5f;
        }


        void UpdateArduinoPressure(float dt)
        {
            if (arduinoPressure == null)
            {
                _signals[InputSignal.ArduinoPressureRaw] = 0f;
                _signals[InputSignal.ArduinoPressure01] = 0f;
                return;
            }

            float raw = arduinoPressure.latestRaw;
            _signals[InputSignal.ArduinoPressureRaw] = raw;

            float target01 = Mathf.Clamp01(
                Mathf.InverseLerp(pressureRawMin, pressureRawMax, raw)
            );

            if (!smoothArduinoPressure)
            {
                _pressureSeeded = true;
                _pressure01 = target01;
            }
            else
            {
                _pressure01 = Filter01(
                    ref _pressureSeeded,
                    _pressure01,
                    target01,
                    dt,
                    pressureDeadZone01,
                    pressureHysteresis01,
                    pressureEmaTau,
                    pressureMaxDeltaPerSec
                );
            }

            _signals[InputSignal.ArduinoPressure01] = _pressure01;
        }

        // ============================================================
        // Hand subsystem helpers
        // ============================================================
        bool EnsureHandSubsystemRunning()
        {
            if (handSubsystem == null || !handSubsystem.running)
                handSubsystem = FindRunningHandSubsystem();
            return (handSubsystem != null && handSubsystem.running);
        }

        XRHandSubsystem FindRunningHandSubsystem()
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            for (int i = 0; i < subsystems.Count; i++)
                if (subsystems[i] != null && subsystems[i].running)
                    return subsystems[i];

            return (subsystems.Count > 0) ? subsystems[0] : null;
        }

        static float Filter01(
            ref bool seeded,
            float current,
            float target,
            float dt,
            float deadZone01,
            float hysteresis01,
            float emaTau,
            float maxDeltaPerSec)
        {
            if (!seeded)
            {
                seeded = true;
                return Mathf.Clamp01(target);
            }

            if (Mathf.Abs(target - current) <= Mathf.Max(0f, deadZone01)) target = current;
            if (Mathf.Abs(target - current) <= Mathf.Max(0f, hysteresis01)) target = current;

            float alpha = (emaTau <= 1e-4f) ? 1f : (1f - Mathf.Exp(-dt / emaTau));
            float next = Mathf.Lerp(current, target, alpha);

            if (maxDeltaPerSec > 0f)
            {
                float maxDelta = maxDeltaPerSec * dt;
                next = Mathf.Clamp(next, current - maxDelta, current + maxDelta);
            }

            return Mathf.Clamp01(next);
        }

        public void RecalibrateRightWristBaseline()
        {
            _rightWristBaselineSeeded = false;
            _rightWristSeeded = false;
            if (debugLogs) Debug.Log("[SignalProvider_App2] RightWrist baseline reset.");
        }

        float GetSingleArmRaise(XRHand hand)
        {
            if (!EnsureHandSubsystemRunning()) return 0f;
            if (!hand.isTracked) return 0f;

            XRHandJoint wrist = hand.GetJoint(XRHandJointID.Wrist);
            if (!wrist.TryGetPose(out Pose wristPose)) return 0f;

            Vector3 shoulderApprox = hmdCamera.transform.position;
            Vector3 armDir = (wristPose.position - shoulderApprox).normalized;

            float dot = Vector3.Dot(armDir, Vector3.up);

            float t = Mathf.InverseLerp(-0.2f, 0.9f, dot);
            return Mathf.Clamp01(t);
        }

    }
}
