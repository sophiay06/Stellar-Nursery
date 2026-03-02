using UnityEngine;

public enum MeditationPhase
{
    Arrival,
    Balance,
    Release
}

public class MeditationStateController : MonoBehaviour
{
    [Header("Current Phase")]
    public MeditationPhase currentPhase = MeditationPhase.Arrival;

    [Header("One-shot latch")]
    [Tooltip("Set true when we enter Release once. Visual controllers should consume this so Release runs only once.")]
    public bool releaseLatched = false;

    // ============================================================
    // Breathing Stability Input
    // ============================================================

    [Header("Breathing Stability")]
    public BreathingStability breathingStability;

    [Range(0f, 1f)]
    [Tooltip("Required breathing stability to count as calm.")]
    public float requiredStability01 = 0.75f;

    // ============================================================
    // Timing
    // ============================================================

    [Header("Hold times (seconds)")]
    public float holdToEnterBalance = 12f;
    public float holdToEnterRelease = 25f;

    [Header("Hysteresis (prevents flicker)")]
    public float exitGraceSeconds = 2f;

    // ============================================================
    // Huge Stars
    // ============================================================

    [Header("Huge Stars")]
    public HugeStarsController hugeStars;

    [Tooltip("Show huge stars this many seconds BEFORE Release.")]
    public float hugeStarsLeadTime = 5f;

    [Header("Huge Stars State")]
    public bool hugeStarsActive = false;

    private bool hugeStarsShownThisCycle = false;

    // ============================================================
    // Internal Timers
    // ============================================================

    private float calmHoldTimer = 0f;
    private float outOfStabilityTimer = 0f;

    void Update()
    {
        if (breathingStability == null)
            return;

        bool isStable =
            breathingStability.stability01 >= requiredStability01;

        switch (currentPhase)
        {
            case MeditationPhase.Arrival:
                UpdateArrival(isStable);
                break;

            case MeditationPhase.Balance:
                UpdateBalance(isStable);
                break;

            case MeditationPhase.Release:
                break;
        }
    }

    // ============================================================
    // Arrival Phase
    // ============================================================

    void UpdateArrival(bool isStable)
    {
        if (isStable)
        {
            calmHoldTimer += Time.deltaTime;

            if (calmHoldTimer >= holdToEnterBalance)
                EnterBalance();
        }
        else
        {
            calmHoldTimer = Mathf.Max(0f, calmHoldTimer - Time.deltaTime);
        }
    }

    // ============================================================
    // Balance Phase
    // ============================================================

    void UpdateBalance(bool isStable)
    {
        if (isStable)
        {
            outOfStabilityTimer = 0f;
            calmHoldTimer += Time.deltaTime;

            float timeUntilRelease = holdToEnterRelease - calmHoldTimer;

            if (!hugeStarsShownThisCycle &&
                timeUntilRelease <= hugeStarsLeadTime)
            {
                hugeStarsShownThisCycle = true;
                hugeStarsActive = true;

                if (hugeStars != null)
                    hugeStars.Show();
            }

            if (calmHoldTimer >= holdToEnterRelease)
                EnterRelease();
        }
        else
        {
            outOfStabilityTimer += Time.deltaTime;

            if (outOfStabilityTimer >= exitGraceSeconds)
            {
                EnterArrival();
            }
            else
            {
                calmHoldTimer =
                    Mathf.Max(0f, calmHoldTimer - Time.deltaTime * 0.5f);
            }
        }
    }

    // ============================================================
    // Phase Transitions
    // ============================================================

    void EnterArrival()
    {
        currentPhase = MeditationPhase.Arrival;

        calmHoldTimer = 0f;
        outOfStabilityTimer = 0f;

        releaseLatched = false;

        hugeStarsShownThisCycle = false;
        hugeStarsActive = false;

        Debug.Log("Meditation → ARRIVAL (breathing stability)");
    }

    void EnterBalance()
    {
        currentPhase = MeditationPhase.Balance;

        outOfStabilityTimer = 0f;

        hugeStarsShownThisCycle = false;
        hugeStarsActive = false;

        Debug.Log("Meditation → BALANCE (breathing stability)");
    }

    void EnterRelease()
    {
        currentPhase = MeditationPhase.Release;
        releaseLatched = true;

        Debug.Log("Meditation → RELEASE (breathing stability)");
    }

    public void ResetSession()
    {
        currentPhase = MeditationPhase.Arrival;

        calmHoldTimer = 0f;
        outOfStabilityTimer = 0f;

        releaseLatched = false;

        hugeStarsShownThisCycle = false;
        hugeStarsActive = false;

        Debug.Log("Meditation session reset → ARRIVAL");
    }
}
