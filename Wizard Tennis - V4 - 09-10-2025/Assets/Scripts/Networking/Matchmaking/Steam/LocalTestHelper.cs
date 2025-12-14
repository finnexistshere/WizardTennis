using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// DEVELOPMENT ONLY - Switches to Unity Transport for local testing
/// Attach this to NetworkManager and toggle "Use Local Testing Mode"
/// Remove before shipping!
/// </summary>
public class LocalTestHelper : MonoBehaviour
{
    [Header("Local Testing (Same Steam Account)")]
    [SerializeField] private bool useLocalTestingMode = false;

    [Header("References")]
    [SerializeField] private NetworkManager networkManager;

    private NetworkTransport originalTransport;
    private UnityTransport unityTransport;

    private void Awake()
    {
        if (networkManager == null)
            networkManager = GetComponent<NetworkManager>();

        if (useLocalTestingMode)
        {
            Debug.LogWarning("[LocalTest] ?? LOCAL TESTING MODE ENABLED");
            Debug.LogWarning("[LocalTest] Using Unity Transport instead of Steam P2P");
            Debug.LogWarning("[LocalTest] This bypasses Steam - REMOVE BEFORE SHIPPING!");

            SetupLocalTesting();
        }
    }

    private void SetupLocalTesting()
    {
        // Store original transport
        originalTransport = networkManager.NetworkConfig.NetworkTransport;

        // Get or add UnityTransport
        unityTransport = networkManager.GetComponent<UnityTransport>();
        if (unityTransport == null)
        {
            unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
        }

        // Configure for localhost
        unityTransport.SetConnectionData("127.0.0.1", 7777);

        // Switch transport
        networkManager.NetworkConfig.NetworkTransport = unityTransport;

        Debug.Log("[LocalTest] ? Configured Unity Transport for localhost");
        Debug.Log("[LocalTest] Host will listen on 127.0.0.1:7777");
        Debug.Log("[LocalTest] Client will connect to 127.0.0.1:7777");
    }

    private void OnDestroy()
    {
        // Restore original transport
        if (useLocalTestingMode && originalTransport != null && networkManager != null)
        {
            networkManager.NetworkConfig.NetworkTransport = originalTransport;
        }
    }
}