using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ForceTransportSetup : MonoBehaviour
{
    UnityTransport transport;

    void Awake()
    {
        transport = GetComponent<UnityTransport>();

        // Force correct values BEFORE StartHost
        transport.SetConnectionData("127.0.0.1", 7778);
    }

    void Start()
    {
        Debug.Log("Starting Host…");
        bool ok = NetworkManager.Singleton.StartHost();

        if (!ok)
        {
            Debug.LogError("StartHost FAILED!");
        }
        else
        {
            Debug.Log("Host started successfully");
        }
    }
}
