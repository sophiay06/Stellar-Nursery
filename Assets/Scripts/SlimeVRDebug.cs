using UnityEngine;

public class SlimeVRDebug : MonoBehaviour
{
    [Header("SlimeVR Trackers")]
    public Transform waist;
    public Transform chest;

    void Update()
    {
        if (waist != null)
        {
            Debug.Log(
                $"WAIST world: {waist.position}  " +
                $"local: {waist.localPosition}  " +
                $"rotation: {waist.eulerAngles}"
            );
        }

        if (chest != null)
        {
            Debug.Log(
                $"CHEST world: {chest.position}  " +
                $"local: {chest.localPosition}  " +
                $"rotation: {chest.eulerAngles}"
            );
        }
    }
}
