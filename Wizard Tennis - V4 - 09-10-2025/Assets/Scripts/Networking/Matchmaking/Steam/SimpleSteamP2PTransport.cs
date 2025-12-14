using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Steamworks;

/// <summary>
/// Simple Steam P2P Transport for Netcode for GameObjects
/// Drop this on your NetworkManager GameObject
/// </summary>
public class SimpleSteamP2PTransport : NetworkTransport
{
    [Header("Steam P2P Settings")]
    [Tooltip("Target Steam ID to connect to (set by SteamLobbyManager for clients)")]
    public ulong targetSteamId = 0;

    [Tooltip("Maximum packet size in bytes")]
    public int maxPacketSize = 1200;

    private const int CHANNEL_DEFAULT = 0;
    private Dictionary<ulong, CSteamID> clientIdToSteamId = new Dictionary<ulong, CSteamID>();
    private Dictionary<ulong, ulong> steamIdToClientId = new Dictionary<ulong, ulong>();
    private ulong nextClientId = 1;

    // Callbacks
    private Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
    private Callback<P2PSessionConnectFail_t> p2pSessionConnectFailCallback;

    private bool isInitialized = false;
    private EP2PSend sendType = EP2PSend.k_EP2PSendReliable;

    public override ulong ServerClientId => 0;

    private void Awake()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamTransport] Steam not initialized!");
            return;
        }

        p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
    }

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[SteamTransport] Send called before initialization");
            return;
        }

        // Determine send type based on delivery
        EP2PSend sendType = networkDelivery switch
        {
            NetworkDelivery.Reliable => EP2PSend.k_EP2PSendReliable,
            NetworkDelivery.ReliableFragmentedSequenced => EP2PSend.k_EP2PSendReliable,
            NetworkDelivery.ReliableSequenced => EP2PSend.k_EP2PSendReliable,
            NetworkDelivery.Unreliable => EP2PSend.k_EP2PSendUnreliable,
            NetworkDelivery.UnreliableSequenced => EP2PSend.k_EP2PSendUnreliableNoDelay,
            _ => EP2PSend.k_EP2PSendReliable
        };

        if (clientId == ServerClientId)
        {
            // Server sending to all clients
            foreach (var kvp in clientIdToSteamId)
            {
                SendToSteamId(kvp.Value, payload, sendType);
            }
        }
        else
        {
            // Send to specific client
            if (clientIdToSteamId.TryGetValue(clientId, out CSteamID steamId))
            {
                SendToSteamId(steamId, payload, sendType);
            }
            else
            {
                Debug.LogWarning($"[SteamTransport] Unknown client ID: {clientId}");
            }
        }
    }

    private void SendToSteamId(CSteamID targetSteamId, ArraySegment<byte> payload, EP2PSend sendType)
    {
        // Create buffer from ArraySegment
        byte[] buffer = new byte[payload.Count];
        Array.Copy(payload.Array, payload.Offset, buffer, 0, payload.Count);

        bool success = SteamNetworking.SendP2PPacket(
            targetSteamId,
            buffer,
            (uint)buffer.Length,
            sendType,
            CHANNEL_DEFAULT
        );

        if (!success)
        {
            Debug.LogWarning($"[SteamTransport] Failed to send packet to {targetSteamId}");
        }
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0;
        payload = default;
        receiveTime = Time.realtimeSinceStartup;

        if (!isInitialized || !SteamManager.Initialized)
        {
            return NetworkEvent.Nothing;
        }

        // Check for incoming packets
        if (SteamNetworking.IsP2PPacketAvailable(out uint packetSize, CHANNEL_DEFAULT))
        {
            byte[] buffer = new byte[packetSize];

            if (SteamNetworking.ReadP2PPacket(buffer, packetSize, out uint bytesRead, out CSteamID senderId, CHANNEL_DEFAULT))
            {
                // Map Steam ID to client ID
                if (!steamIdToClientId.TryGetValue(senderId.m_SteamID, out clientId))
                {
                    // New client connection
                    clientId = nextClientId++;
                    steamIdToClientId[senderId.m_SteamID] = clientId;
                    clientIdToSteamId[clientId] = senderId;

                    Debug.Log($"[SteamTransport] New client connected: {senderId} -> ClientID {clientId}");

                    // Return connect event first
                    payload = new ArraySegment<byte>(buffer, 0, (int)bytesRead);
                    return NetworkEvent.Connect;
                }

                payload = new ArraySegment<byte>(buffer, 0, (int)bytesRead);
                return NetworkEvent.Data;
            }
        }

        return NetworkEvent.Nothing;
    }

    public override bool StartClient()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamTransport] Cannot start client - Steam not initialized!");
            return false;
        }

        if (targetSteamId == 0)
        {
            Debug.LogError("[SteamTransport] Cannot start client: targetSteamId not set!");
            Debug.LogError("[SteamTransport] Make sure SteamLobbyManager sets the targetSteamId before starting client");
            return false;
        }

        Debug.Log($"[SteamTransport] Starting client, connecting to Steam ID: {targetSteamId}");
        Debug.Log($"[SteamTransport] My Steam ID: {SteamUser.GetSteamID()}");

        isInitialized = true;

        // Map server
        CSteamID serverSteamId = new CSteamID(targetSteamId);
        steamIdToClientId[targetSteamId] = ServerClientId;
        clientIdToSteamId[ServerClientId] = serverSteamId;

        Debug.Log($"[SteamTransport] Mapped server: ClientID {ServerClientId} -> Steam ID {serverSteamId}");

        // Accept P2P session with server
        SteamNetworking.AcceptP2PSessionWithUser(serverSteamId);
        Debug.Log($"[SteamTransport] Accepted P2P session with server");

        // Send initial packet to establish connection
        byte[] initPacket = new byte[1] { 0xFF };
        bool sent = SteamNetworking.SendP2PPacket(serverSteamId, initPacket, 1, EP2PSend.k_EP2PSendReliable, CHANNEL_DEFAULT);

        if (sent)
        {
            Debug.Log($"[SteamTransport] ? Sent initial connection packet to server");
        }
        else
        {
            Debug.LogError($"[SteamTransport] ? Failed to send initial packet to server!");
        }

        return true;
    }

    public override bool StartServer()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamTransport] Cannot start server - Steam not initialized!");
            return false;
        }

        Debug.Log("[SteamTransport] Starting server");
        Debug.Log($"[SteamTransport] Server Steam ID: {SteamUser.GetSteamID()}");
        isInitialized = true;
        return true;
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        if (clientIdToSteamId.TryGetValue(clientId, out CSteamID steamId))
        {
            Debug.Log($"[SteamTransport] Disconnecting client {clientId} ({steamId})");
            SteamNetworking.CloseP2PSessionWithUser(steamId);
            clientIdToSteamId.Remove(clientId);
            steamIdToClientId.Remove(steamId.m_SteamID);
        }
    }

    public override void DisconnectLocalClient()
    {
        Debug.Log("[SteamTransport] Disconnecting local client");

        if (clientIdToSteamId.TryGetValue(ServerClientId, out CSteamID serverSteamId))
        {
            SteamNetworking.CloseP2PSessionWithUser(serverSteamId);
        }

        Shutdown();
    }

    public override ulong GetCurrentRtt(ulong clientId)
    {
        return 0; // Steam doesn't provide easy RTT access
    }

    public override void Shutdown()
    {
        Debug.Log("[SteamTransport] Shutting down");

        // Close all P2P sessions
        foreach (var kvp in clientIdToSteamId)
        {
            SteamNetworking.CloseP2PSessionWithUser(kvp.Value);
        }

        clientIdToSteamId.Clear();
        steamIdToClientId.Clear();
        isInitialized = false;
        nextClientId = 1;
    }

    public override void Initialize(NetworkManager networkManager = null)
    {
        Debug.Log("[SteamTransport] Initialize called");
    }

    // Steam Callbacks
    private void OnP2PSessionRequest(P2PSessionRequest_t request)
    {
        Debug.Log($"[SteamTransport] P2P session request from {request.m_steamIDRemote}");

        // Auto-accept all P2P session requests
        // In production, you might want to validate this is from lobby members
        SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
    }

    private void OnP2PSessionConnectFail(P2PSessionConnectFail_t failure)
    {
        Debug.LogError($"[SteamTransport] P2P session connect failed with {failure.m_steamIDRemote}: {failure.m_eP2PSessionError}");

        // Find and disconnect the client
        if (steamIdToClientId.TryGetValue(failure.m_steamIDRemote.m_SteamID, out ulong clientId))
        {
            InvokeOnTransportEvent(NetworkEvent.Disconnect, clientId, default, Time.realtimeSinceStartup);
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}