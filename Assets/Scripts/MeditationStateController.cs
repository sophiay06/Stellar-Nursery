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

    [Header("Input")]
    public NebulaCompression nebulaCompression;

    [Header("Calm band on compression")]
    [Range(0f, 1f)] public float calmMin = 0.30f;
    [Range(0f, 1f)] public float calmMax = 0.70f;

    [Header("Hold times (seconds)")]
    public float holdToEnterBalance = 12f;// 10–20s
    public float holdToEnterRelease = 25f;  // 20–40s

    [Header("Hysteresis (prevents flicker)")]
    public float exitGraceSeconds = 2f;

    [Header("Huge Stars")]
    public HugeStarsController hugeStars;
    [Tooltip("Show huge stars this many seconds BEFORE Balance would transition into Release.")]
    public float hugeStarsLeadTime = 5f;

    [Header("Huge Stars State")]
    [Tooltip("True after huge stars have been shown for this cycle. Used to freeze starfield mapping.")]
    public bool hugeStarsActive = false;

    private bool hugeStarsShownThisCycle = false;

    private float calmHoldTimer = 0f;
    private float outOfBandTimer = 0f;

    void Update()
    {
        if (nebulaCompression == null) return;

        float c = nebulaCompression.compression;
        bool inCalmBand = (c >= calmMin && c <= calmMax);

        switch (currentPhase)
        {
            case MeditationPhase.Arrival:
                UpdateArrival(inCalmBand);
                break;

            case MeditationPhase.Balance:
                UpdateBalance(inCalmBand);
                break;

            case MeditationPhase.Release:
                break;
        }
    }

    void UpdateArrival(bool inCalmBand)
    {
        if (inCalmBand)
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

    void UpdateBalance(bool inCalmBand)
    {
        if (inCalmBand)
        {
            outOfBandTimer = 0f;
            calmHoldTimer += Time.deltaTime;

            //huge stars trigger
            float timeUntilRelease = holdToEnterRelease - calmHoldTimer;
            if (!hugeStarsShownThisCycle && timeUntilRelease <= hugeStarsLeadTime)
            {
                hugeStarsShownThisCycle = true;
                hugeStarsActive = true;

                if (hugeStars != null) hugeStars.Show();
            }

            if (calmHoldTimer >= holdToEnterRelease)
                EnterRelease();
        }
        else
        {
            outOfBandTimer += Time.deltaTime;

            if (outOfBandTimer >= exitGraceSeconds)
            {
                EnterArrival();
            }
            else
            {
                calmHoldTimer = Mathf.Max(0f, calmHoldTimer - Time.deltaTime * 0.5f);
            }
        }
    }

    void EnterArrival()
    {
        currentPhase = MeditationPhase.Arrival;
        calmHoldTimer = 0f;
        outOfBandTimer = 0f;

        releaseLatched = false;

        hugeStarsShownThisCycle = false;
        hugeStarsActive = false; // reset

        Debug.Log("Meditation → ARRIVAL (compression proxy)");
    }

    void EnterBalance()
    {
        currentPhase = MeditationPhase.Balance;
        outOfBandTimer = 0f;

        hugeStarsShownThisCycle = false;
        hugeStarsActive = false; // reset for this phase

        Debug.Log("Meditation → BALANCE (compression proxy)");
    }

    void EnterRelease()
    {
        currentPhase = MeditationPhase.Release;
        releaseLatched = true;

        Debug.Log("Meditation → RELEASE (compression proxy)");
    }

    public void ResetSession()
    {
        currentPhase = MeditationPhase.Arrival;
        calmHoldTimer = 0f;
        outOfBandTimer = 0f;

        releaseLatched = false;

        hugeStarsShownThisCycle = false;
        hugeStarsActive = false;

        Debug.Log("Meditation session reset → ARRIVAL");
    }
}
