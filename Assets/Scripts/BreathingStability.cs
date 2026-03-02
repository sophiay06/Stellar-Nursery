using UnityEngine;
using System.Collections.Generic;
using MappingTool;  
using Signals; 

public class BreathingStability : MonoBehaviour
{
    [Header("References")]
    public SignalProvider signalProvider;

    [Header("Window Settings")]
    [Tooltip("Seconds of breathing history to evaluate.")]
    public float windowSeconds = 6f;

    [Tooltip("Sampling rate (Hz).")]
    public float sampleRate = 30f;

    [Header("Stability Thresholds")]
    [Tooltip("Variance considered perfectly stable.")]
    public float minVariance = 0.0005f;

    [Tooltip("Variance considered completely unstable.")]
    public float maxVariance = 0.02f;

    [Header("Debug")]
    public bool debugLogs = false;
    public float debugInterval = 0.5f;

    public float stability01 { get; private set; }

    private readonly Queue<float> _samples = new Queue<float>();
    private int _maxSamples;
    private float _sampleTimer;
    private float _nextDebug;

    void Start()
    {
        _maxSamples = Mathf.Max(5, Mathf.RoundToInt(windowSeconds * sampleRate));
    }

    void Update()
    {
        if (signalProvider == null) return;

        if (!signalProvider.TryGetSignal(InputSignal.TorsoRespAmplitude01, out float amp))
            return;

        _sampleTimer += Time.deltaTime;

        float step = (sampleRate <= 1e-3f) ? 0.033f : (1f / sampleRate);
        if (_sampleTimer >= step)
        {
            _sampleTimer = 0f;

            AddSample(amp);
            ComputeStability();

            if (debugLogs && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + Mathf.Max(0.1f, debugInterval);
                Debug.Log($"[BreathingStability] amp={amp:F3} samples={_samples.Count}/{_maxSamples} stability01={stability01:F3}");
            }
        }
    }

    void AddSample(float v)
    {
        _samples.Enqueue(v);
        while (_samples.Count > _maxSamples)
            _samples.Dequeue();
    }

    void ComputeStability()
    {
        if (_samples.Count < 5)
        {
            stability01 = 0f;
            return;
        }

        // mean
        float mean = 0f;
        foreach (var s in _samples) mean += s;
        mean /= _samples.Count;

        // variance
        float var = 0f;
        foreach (var s in _samples)
        {
            float d = s - mean;
            var += d * d;
        }
        var /= _samples.Count;

        // map variance -> stability (low var => high stability)
        // float t = Mathf.InverseLerp(maxVariance, minVariance, var);
        // stability01 = Mathf.Clamp01(t);
        float t = Mathf.InverseLerp(maxVariance, minVariance, var);
        float rawStability = Mathf.Clamp01(t);

        // Add smoothing to stability itself
        float smoothingTau = 1.2f; // seconds
        float alpha = 1f - Mathf.Exp(-Time.deltaTime / smoothingTau);

        stability01 = Mathf.Lerp(stability01, rawStability, alpha);
    }
}
