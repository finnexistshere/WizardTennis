using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Netcode.Transports.Facepunch;

/// <summary>
/// DEVELOPMENT ONLY - Switches to Unity Transport for local testing with same Steam account
/// Attach this to NetworkManager GameObject in your game scene
/// CHECK "Use Local Testing" to bypass Steam P2P
/// UNCHECK before building for real Steam testing!
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class LocalTestHelper : MonoBehaviour
{
    [Header("?? DEVELOPMENT ONLY - Remove Before Shipping!")]
    [SerializeField] private bool useLocalTesting = false;

    [Header("Local Network Settings")]
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    private NetworkManager networkManager;
    private NetworkTransport originalTransport;
    private UnityTransport unityTransport;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        if (useLocalTesting)
        {
            Debug.LogWarning("??????????????????????????????????????????????????");
            Debug.LogWarning("?  ??  LOCAL TESTING MODE ENABLED  ??           ?");
            Debug.LogWarning("?                                                ?");
            Debug.LogWarning("?  Using Unity Transport (localhost)             ?");
            Debug.LogWarning("?  Steam P2P is BYPASSED                         ?");
            Debug.LogWarning("?  DISABLE THIS BEFORE SHIPPING!                 ?");
            Debug.LogWarning("??????????????????????????????????????????????????");

            SetupLocalTesting();
        }
    }

    private void SetupLocalTesting()
    {
        // Store original transport (FacepunchTransport)
        originalTransport = networkManager.NetworkConfig.NetworkTransport;

        // Get or add UnityTransport
        unityTransport = networkManager.GetComponent<UnityTransport>();
        if (unityTransport == null)
        {
            unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
            Debug.Log("[LocalTest] Added UnityTransport component");
        }

        // Configure for localhost
        unityTransport.SetConnectionData(ipAddress, port);
        Debug.Log($"[LocalTest] ? Configured Unity Transport");
        Debug.Log($"[LocalTest]    Address: {ipAddress}");
        Debug.Log($"[LocalTest]    Port: {port}");

        // Switch to UnityTransport
        networkManager.NetworkConfig.NetworkTransport = unityTransport;

        Debug.Log("[LocalTest] ? Switched to Unity Transport");
        Debug.Log("[LocalTest] Host will listen on: 127.0.0.1:7777");
        Debug.Log("[LocalTest] Client will connect to: 127.0.0.1:7777");
    }

    private void OnDestroy()
    {
        // Restore original transport when destroyed
        if (useLocalTesting && originalTransport != null && networkManager != null)
        {
            networkManager.NetworkConfig.NetworkTransport = originalTransport;
            Debug.Log("[LocalTest] Restored original transport");
        }
    }

    // Public method to check if local testing is enabled
    public bool IsLocalTestingEnabled()
    {
        return useLocalTesting;
    }

    // Method to switch transport at runtime (if needed)
    public void EnableLocalTesting()
    {
        if (!useLocalTesting)
        {
            useLocalTesting = true;
            SetupLocalTesting();
        }
    }

    public void DisableLocalTesting()
    {
        if (useLocalTesting && originalTransport != null)
        {
            useLocalTesting = false;
            networkManager.NetworkConfig.NetworkTransport = originalTransport;
            Debug.Log("[LocalTest] Disabled local testing, restored Steam transport");
        }
    }
}