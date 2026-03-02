using UnityEngine;

//Manual breathing input for inspector testing. 0 = full exhale, 1 = full inhale
public class BreathingManual : MonoBehaviour
{
    [Range(0f, 1f)]
    public float breath01 = 0.5f;
}
