using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoSerialReader : MonoBehaviour
{
    public string portName = "COM3";
    public int baudRate = 115200;
    public int latestRaw = 0;

    private SerialPort serial;
    private Thread thread;
    private volatile bool running = false;

    void Start()
    {
        try
        {
            serial = new SerialPort(portName, baudRate);
            serial.ReadTimeout = 500;
            serial.NewLine = "\n";
            serial.DtrEnable = true;
            serial.RtsEnable = true;
            serial.Open();

            Debug.Log("Port opened");

            // Give Arduino time to reset after port opens
            Thread.Sleep(2000);

            // Clear any junk from startup
            serial.DiscardInBuffer();

            running = true;
            thread = new Thread(ReadLoop);
            thread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("Serial open failed: " + e.Message);
        }
    }

    void ReadLoop()
    {
        while (running && serial != null && serial.IsOpen)
        {
            try
            {
                string line = serial.ReadLine();
                Debug.Log("Received: " + line);

                if (int.TryParse(line.Trim(), out int value))
                {
                    latestRaw = value;
                }
            }
            catch (TimeoutException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError("Read error: " + e.Message);
            }
        }
    }

    void Update()
    {
        float normalized = Mathf.Clamp01(latestRaw / 1023f);
        transform.localScale = Vector3.one * (0.5f + normalized * 1.5f);
    }

    void OnApplicationQuit()
    {
        running = false;

        if (thread != null && thread.IsAlive)
            thread.Join(500);

        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}