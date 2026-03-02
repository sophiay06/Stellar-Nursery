using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandSubsystemProbe : MonoBehaviour
{
    void Start()
    {
        var subs = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        Debug.Log("XRHandSubsystem count = " + subs.Count);
        if (subs.Count > 0) Debug.Log("XRHandSubsystem running = " + subs[0].running);
    }
}
