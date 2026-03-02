using UnityEngine;
using OscJack;

public class OscSniffer : MonoBehaviour
{
    public int listenPort = 9000;
    OscServer _server;

    void OnEnable()
    {
        _server = new OscServer(listenPort);
        _server.MessageDispatcher.AddCallback("", (addr, data) =>
        {
            // Prints the address and first float if any
            float v = (data.GetElementCount() > 0) ? data.GetElementAsFloat(0) : float.NaN;
            Debug.Log($"[OscSniffer] {addr}  v0={v}");
        });

        Debug.Log("[OscSniffer] Listening on " + listenPort);
    }

    void OnDisable()
    {
        _server?.Dispose();
        _server = null;
    }
}
