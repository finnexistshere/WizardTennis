using System;
using UnityEngine;

namespace Netcode.Transports.Facepunch
{
    public class FacepunchVoiceServer : MonoBehaviour
    {
        public static FacepunchVoiceServer Instance { get; private set; }

        private FacepunchTransport transport;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            transport = FindObjectOfType<FacepunchTransport>();

            if (transport == null)
            {
                Debug.LogError(
                    "[Voice] Could not find FacepunchTransport."
                );

                return;
            }

            transport.OnVoiceDataReceived += OnVoiceDataReceived;
        }

        private void OnDestroy()
        {
            if (transport != null)
                transport.OnVoiceDataReceived -= OnVoiceDataReceived;
        }

        private void OnVoiceDataReceived(
            ulong senderClientId,
            ArraySegment<byte> data)
        {
            if (!Unity.Netcode.NetworkManager.Singleton.IsServer)
                return;

            byte[] packet = new byte[data.Count];

            Buffer.BlockCopy(
                data.Array,
                data.Offset,
                packet,
                0,
                data.Count
            );

            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId == senderClientId)
                    continue;

                transport.SendVoiceToClient(
                    client.ClientId,
                    packet
                );
            }
        }
    }
}