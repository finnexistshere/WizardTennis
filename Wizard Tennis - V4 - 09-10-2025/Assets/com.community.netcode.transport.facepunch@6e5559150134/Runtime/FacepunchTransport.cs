using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

// Should I be modifying someone else's code, written by someone smarter than me, for a system I don't fully understand?

// Probably not.

// But humanity never got anywhere by being afraid of change, so here we fucking go.

namespace Netcode.Transports.Facepunch
{
    using SocketConnection = Connection;

    public class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private ConnectionManager connectionManager;
        private SocketManager socketManager;
        private Dictionary<ulong, Client> connectedClients;

        [Space]
        [Tooltip("The Steam App ID of your game. Technically you're not allowed to use 480, but Valve doesn't do anything about it so it's fine for testing purposes.")]
        [SerializeField] private uint steamAppId = 480;

        [Tooltip("The Steam ID of the user targeted when joining as a client.")]
        [SerializeField] public ulong targetSteamId;

        [Header("Info")]
        [ReadOnly]
        [Tooltip("When in play mode, this will display your Steam ID.")]
        [SerializeField] private ulong userSteamId;

        private LogLevel LogLevel => NetworkManager.Singleton.LogLevel;

        private readonly Queue<TransportEvent> eventQueue = new();

        private struct TransportEvent
        {
            public NetworkEvent Type;
            public ulong ClientId;
            public ArraySegment<byte> Payload;
            public float Time;
        }


        private class Client
        {
            public SteamId steamId;
            public SocketConnection connection;

        }

        public const byte VoicePacket = 0xF1;

        public event Action<ulong, ArraySegment<byte>> OnVoiceDataReceived;

        public bool IsServer => socketManager != null;
        public bool IsClient => connectionManager != null;

        public IReadOnlyDictionary<ulong, Steamworks.Data.Connection> ServerConnections
        {
            get
            {
                var result = new Dictionary<ulong, Steamworks.Data.Connection>();

                foreach (var pair in connectedClients)
                    result[pair.Key] = pair.Value.connection;

                return result;
            }
        }

        public Steamworks.Data.Connection ClientConnection =>
            connectionManager != null
                ? connectionManager.Connection
                : default;

        #region MonoBehaviour Messages

        private void Awake()
        {
            // SteamClient is initialized elsewhere (FacepunchSteamManager)
            StartCoroutine(InitSteamworks());
        }
        #endregion

        #region NetworkTransport Overrides

        public override ulong ServerClientId => 0;

        public override void DisconnectLocalClient()
        {
            connectionManager?.Connection.Close();

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting local client.");
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (connectedClients.TryGetValue(clientId, out Client user))
            {
                // Flush any pending messages before closing the connection
                user.connection.Flush();
                user.connection.Close();
                connectedClients.Remove(clientId);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting remote client with ID {clientId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to disconnect remote client with ID {clientId}, client not connected.");
        }

        public override unsafe ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            connectedClients = new Dictionary<ulong, Client>();
        }

        private SendType NetworkDeliveryToSendType(NetworkDelivery delivery)
        {
            return delivery switch
            {
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.Unreliable => SendType.Unreliable,
                NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
                _ => SendType.Reliable
            };
        }

        public override void Shutdown()
        {
            try
            {
                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Shutting down.");

                connectionManager?.Close();
                socketManager?.Close();
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exception while shutting down: {e}");
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
	        var sendType = NetworkDeliveryToSendType(delivery);

	        if (clientId == ServerClientId)
		        connectionManager.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (connectedClients.TryGetValue(clientId, out Client user))
		        user.connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (LogLevel <= LogLevel.Normal)
		        Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to send packet to remote client with ID {clientId}, client not connected.");
        }

        public override NetworkEvent PollEvent(
            out ulong clientId,
            out ArraySegment<byte> payload,
            out float receiveTime)
        {
            connectionManager?.Receive();
            socketManager?.Receive();

            if (eventQueue.Count > 0)
            {
                var ev = eventQueue.Dequeue();
                clientId = ev.ClientId;
                payload = ev.Payload;
                receiveTime = ev.Time;
                return ev.Type;
            }

            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }


        public override bool StartClient()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as client.");

            connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
            connectionManager.Interface = this;
            return true;
        }

        public override bool StartServer()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as server.");

            socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
            socketManager.Interface = this;
            return true;
        }

        #endregion

        #region ConnectionManager Implementation

        private byte[] payloadCache = new byte[4096];

        private void EnsurePayloadCapacity(int size)
        {
            if (payloadCache.Length >= size)
                return;

            payloadCache = new byte[Math.Max(payloadCache.Length * 2, size)];
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connecting with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnConnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}.");
        }

        unsafe void IConnectionManager.OnMessage(
            IntPtr data,
            int size,
            long messageNum,
            long recvTime,
            int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            if (size > 0 && payloadCache[0] == VoicePacket)
            {
                byte[] voiceData = new byte[size - 1];

                Buffer.BlockCopy(
                    payloadCache,
                    1,
                    voiceData,
                    0,
                    size - 1
                );

                OnVoiceDataReceived?.Invoke(
                    ServerClientId,
                    new ArraySegment<byte>(voiceData)
                );

                return;
            }

            InvokeOnTransportEvent(
                NetworkEvent.Data,
                ServerClientId,
                new ArraySegment<byte>(payloadCache, 0, size),
                Time.realtimeSinceStartup
            );
        }

        #endregion

        #region SocketManager Implementation

        void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Accepting connection from Steam user {info.Identity.SteamId}.");

            connection.Accept();
        }

        void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
        {
            if (!connectedClients.ContainsKey(connection.Id))
            {
                connectedClients.Add(connection.Id, new Client()
                {
                    connection = connection,
                    steamId = info.Identity.SteamId
                });

                InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to connect client with ID {connection.Id}, client already connected.");
        }

        void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
        {
            if (connectedClients.Remove(connection.Id))
	    {
	        InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);

	       if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}");
	    }
     	    else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to diconnect client with ID {connection.Id}, client not connected.");
        }

        unsafe void ISocketManager.OnMessage(
            SocketConnection connection,
            NetIdentity identity,
            IntPtr data,
            int size,
            long messageNum,
            long recvTime,
            int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            if (size > 0 && payloadCache[0] == VoicePacket)
            {
                byte[] voiceData = new byte[size - 1];

                Buffer.BlockCopy(
                    payloadCache,
                    1,
                    voiceData,
                    0,
                    size - 1
                );

                OnVoiceDataReceived?.Invoke(
                    connection.Id,
                    new ArraySegment<byte>(voiceData)
                );

                return;
            }

            InvokeOnTransportEvent(
                NetworkEvent.Data,
                connection.Id,
                new ArraySegment<byte>(payloadCache, 0, size),
                Time.realtimeSinceStartup
            );
        }

        #endregion

        #region Utility Methods

        private IEnumerator InitSteamworks()
        {
            yield return new WaitUntil(() => SteamClient.IsValid);

            SteamNetworkingUtils.InitRelayNetworkAccess();

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Initialized access to Steam Relay Network.");

            userSteamId = SteamClient.SteamId;

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Fetched user Steam ID.");
        }

        #endregion

        #region Voice Chat Methods

        public void SendVoiceToServer(byte[] data)
        {
            if (connectionManager == null)
                return;

            if (connectionManager.Connection == null)
                return;

            byte[] packet = new byte[data.Length + 1];

            packet[0] = VoicePacket;

            Buffer.BlockCopy(
                data,
                0,
                packet,
                1,
                data.Length
            );

            connectionManager.Connection.SendMessage(
                packet,
                SendType.Unreliable
            );
        }

        public void SendVoiceToClient(ulong clientId, byte[] data)
        {
            if (connectedClients == null)
            {
                Debug.LogError("[Voice] connectedClients dictionary is null — " +
                                "Initialize() was never called on this FacepunchTransport instance.");
                return;
            }

            if (!connectedClients.TryGetValue(clientId, out Client client))
            {
                if (LogLevel <= LogLevel.Normal)
                    Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to send voice packet " +
                                      $"to remote client with ID {clientId}, client not connected.");
                return;
            }

            if (client == null)
            {
                Debug.LogError($"[Voice] connectedClients contained a null entry for client {clientId}.");
                return;
            }

            byte[] packet = new byte[data.Length + 1];
            packet[0] = VoicePacket;

            Buffer.BlockCopy(data, 0, packet, 1, data.Length);

            try
            {
                client.connection.SendMessage(packet, SendType.Unreliable);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Voice] client.connection.SendMessage threw for client {clientId}: {e}");
            }
        }

        #endregion

    }
}