using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Steamworks;

namespace Netcode.Transports.Facepunch
{
    public class FacepunchVoiceManager : MonoBehaviour
    {
        public static FacepunchVoiceManager Instance { get; private set; }

        [Header("Transport")]

        [Tooltip("Drag the NetworkManager (or any object holding the FacepunchTransport) here. " +
                 "If left empty, falls back to NetworkManager.Singleton's assigned transport.")]
        [SerializeField]
        private NetworkTransport networkTransportReference;

        [Header("Microphone")]

        [SerializeField]
        private string microphoneDevice = "";

        [SerializeField]
        private int microphoneFrequency = 48000;

        [SerializeField]
        private int microphoneBufferSeconds = 2;

        [Header("Voice")]

        [SerializeField]
        private int voiceSampleRate = 16000;

        [SerializeField]
        private int frameMilliseconds = 20;

        [SerializeField]
        private float silenceThreshold = 0.015f;

        [SerializeField]
        private bool voiceActivation = true;

        [Header("Push To Talk")]

        [SerializeField]
        private KeyCode pushToTalkKey = KeyCode.V;

        [Header("Voice Modifier")]

        [SerializeField]
        private VoiceModifierSettings voiceModifier =
            new VoiceModifierSettings();

        [Header("Playback")]

        [SerializeField]
        private bool use3DVoice = false;

        [SerializeField]
        private float voiceMinDistance = 2f;

        [SerializeField]
        private float voiceMaxDistance = 20f;

        [SerializeField]
        private float defaultPlayerVolume = 1f;

        private FacepunchTransport transport;

        private AudioClip microphoneClip;

        private int microphoneReadPosition;

        private bool microphoneRunning;

        private readonly Dictionary<ulong, VoicePlayer>
            voicePlayers =
            new Dictionary<ulong, VoicePlayer>();

        private int InputSamplesPerFrame =>
            Mathf.RoundToInt(
                microphoneFrequency *
                frameMilliseconds /
                1000f
            );

        private int OutputSamplesPerFrame =>
            Mathf.RoundToInt(
                voiceSampleRate *
                frameMilliseconds /
                1000f
            );

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);
        }

        private bool networkEventsHooked;

        private void Start()
        {
            TryHookNetworkManagerEvents();

            // In case NetworkManager.Singleton already started before this
            // object's Start() ran (e.g. scene load order), attempt an
            // immediate bind too.
            RebindTransport();
        }

        private void TryHookNetworkManagerEvents()
        {
            if (networkEventsHooked)
                return;

            if (NetworkManager.Singleton == null)
                return;

            NetworkManager.Singleton.OnServerStarted += HandleNetworkSessionStarted;
            NetworkManager.Singleton.OnClientStarted += HandleNetworkSessionStarted;

            networkEventsHooked = true;
        }

        private void HandleNetworkSessionStarted()
        {
            RebindTransport();
        }

        private void RebindTransport()
        {
            FacepunchTransport resolved = ResolveTransport();

            if (resolved == null)
            {
                // Not an error here — this can legitimately fire before
                // networking has actually started yet.
                return;
            }

            if (resolved == transport)
            {
                // Same instance as before — nothing to rebind, but make sure
                // the mic is running in case this is the first real bind.
                if (!microphoneRunning)
                    StartMicrophone();

                return;
            }

            if (transport != null)
            {
                transport.OnVoiceDataReceived -= OnVoiceDataReceived;

                Debug.Log(
                    $"[Voice] Rebinding from stale transport " +
                    $"(instance {transport.GetInstanceID()}) to " +
                    $"new transport (instance {resolved.GetInstanceID()})."
                );
            }

            transport = resolved;
            transport.OnVoiceDataReceived += OnVoiceDataReceived;

            if (!microphoneRunning)
                StartMicrophone();
        }

        private FacepunchTransport ResolveTransport()
        {
            // Prefer whatever was manually assigned in the Inspector.
            if (networkTransportReference != null)
            {
                if (networkTransportReference is FacepunchTransport direct)
                    return direct;

                FacepunchTransport onSameObject =
                    networkTransportReference.GetComponent<FacepunchTransport>();

                if (onSameObject != null)
                    return onSameObject;

                Debug.LogWarning(
                    "[Voice] networkTransportReference was assigned but no " +
                    "FacepunchTransport component was found on it. Falling back."
                );
            }

            // Fall back to whatever Netcode itself is actually using —
            // guaranteed to be the one that had Initialize() called on it.
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.NetworkConfig != null)
            {
                return NetworkManager.Singleton.NetworkConfig.NetworkTransport
                    as FacepunchTransport;
            }

            return null;
        }

        private void Update()
        {
            if (transport == null)
                return;

            if (NetworkManager.Singleton == null)
                return;

            if (!NetworkManager.Singleton.IsClient)
                return;

            ProcessMicrophone();

            HandlePushToTalk();
        }

        private void HandlePushToTalk()
        {
            if (!Input.GetKeyDown(pushToTalkKey) &&
                !Input.GetKeyUp(pushToTalkKey))
            {
                return;
            }

            bool enabled =
                Input.GetKey(pushToTalkKey);

            SetVoiceEnabled(enabled);
        }

        private void StartMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning(
                    "[Voice] No microphone detected."
                );

                return;
            }

            if (string.IsNullOrEmpty(microphoneDevice))
            {
                microphoneDevice =
                    Microphone.devices[0];
            }

            microphoneClip =
                Microphone.Start(
                    microphoneDevice,
                    true,
                    microphoneBufferSeconds,
                    microphoneFrequency
                );

            if (microphoneClip == null)
            {
                Debug.LogError(
                    "[Voice] Failed to start microphone."
                );

                return;
            }

            microphoneRunning = true;

            microphoneReadPosition = 0;

            Debug.Log(
                "[Voice] Microphone started: " +
                microphoneDevice
            );
        }

        private void ProcessMicrophone()
        {
            if (!microphoneRunning)
                return;

            if (microphoneClip == null)
                return;

            if (voiceActivation)
            {
                if (!Input.GetKey(pushToTalkKey))
                    return;
            }

            int microphonePosition =
                Microphone.GetPosition(
                    microphoneDevice
                );

            if (microphonePosition < 0)
                return;

            int availableSamples;

            if (microphonePosition >=
                microphoneReadPosition)
            {
                availableSamples =
                    microphonePosition -
                    microphoneReadPosition;
            }
            else
            {
                availableSamples =
                    microphoneClip.samples -
                    microphoneReadPosition +
                    microphonePosition;
            }

            while (
                availableSamples >=
                InputSamplesPerFrame)
            {
                float[] input =
                    new float[
                        InputSamplesPerFrame
                    ];

                microphoneClip.GetData(
                    input,
                    microphoneReadPosition
                );

                microphoneReadPosition +=
                    InputSamplesPerFrame;

                microphoneReadPosition %=
                    microphoneClip.samples;

                availableSamples -=
                    InputSamplesPerFrame;

                ProcessVoiceFrame(input);
            }
        }

        private void ProcessVoiceFrame(
            float[] input)
        {
            float[] mono;

            if (microphoneClip.channels == 1)
            {
                mono = input;
            }
            else
            {
                mono =
                    ConvertToMono(
                        input,
                        microphoneClip.channels
                    );
            }

            float rms =
                CalculateRms(mono);

            if (rms < silenceThreshold)
                return;

            float[] resampled =
                Resample(
                    mono,
                    microphoneFrequency,
                    voiceSampleRate
                );

            VoiceModifier.Process(
                resampled,
                voiceSampleRate,
                voiceModifier
            );

            short[] pcm =
                FloatToPcm16(resampled);

            short predictor;
            byte stepIndex;

            byte[] compressed =
                VoiceAdpcm.Encode(
                    pcm,
                    out predictor,
                    out stepIndex
                );

            byte[] packet =
                BuildVoicePacket(
                    pcm.Length,
                    predictor,
                    stepIndex,
                    compressed
                );

            if (NetworkManager.Singleton.IsServer)
            {
                Debug.Log(
                    $"[Voice][SEND] Host looping back own mic frame locally. " +
                    $"samples={pcm.Length}, packetBytes={packet.Length}"
                );

                // Host has no connectionManager to send over — handle our own
                // voice locally exactly as if it arrived over the network.
                OnVoiceDataReceived(NetworkManager.ServerClientId, new ArraySegment<byte>(packet));
            }
            else
            {
                Debug.Log(
                    $"[Voice][SEND] Sending mic frame to server. " +
                    $"samples={pcm.Length}, packetBytes={packet.Length}"
                );

                transport.SendVoiceToServer(packet);
            }
        }

        private byte[] BuildVoicePacket(
            int sampleCount,
            short predictor,
            byte stepIndex,
            byte[] compressed)
        {
            int headerSize = 10;

            byte[] packet =
                new byte[
                    headerSize +
                    compressed.Length
                ];

            packet[0] = 1;

            Buffer.BlockCopy(
                BitConverter.GetBytes(
                    (ushort)sampleCount
                ),
                0,
                packet,
                1,
                2
            );

            Buffer.BlockCopy(
                BitConverter.GetBytes(
                    predictor
                ),
                0,
                packet,
                3,
                2
            );

            packet[5] = stepIndex;

            Buffer.BlockCopy(
                BitConverter.GetBytes(
                    (uint)Time.frameCount
                ),
                0,
                packet,
                6,
                4
            );

            Buffer.BlockCopy(
                compressed,
                0,
                packet,
                headerSize,
                compressed.Length
            );

            return packet;
        }

        private void OnVoiceDataReceived(
            ulong senderClientId,
            ArraySegment<byte> data)
        {
            if (data.Array == null ||
                data.Count < 9)
            {
                return;
            }

            byte[] buffer =
                new byte[data.Count];

            Buffer.BlockCopy(
                data.Array,
                data.Offset,
                buffer,
                0,
                data.Count
            );

            if (NetworkManager.Singleton == null)
                return;

            Debug.Log(
                $"[Voice][RECV] Transport delivered voice data. " +
                $"senderClientId={senderClientId}, bytes={buffer.Length}, " +
                $"role={(NetworkManager.Singleton.IsServer ? "Server" : "Client")}"
            );

            if (NetworkManager.Singleton.IsServer)
            {
                RelayVoice(senderClientId, buffer);

                // Host is also a listening peer — play back everyone except itself.
                if (NetworkManager.Singleton.IsClient &&
                    senderClientId != NetworkManager.ServerClientId)
                {
                    DecodeVoice(senderClientId, buffer);
                }

                return;
            }

            ReceiveVoice(
                senderClientId,
                buffer
            );
        }

        private void RelayVoice(
            ulong senderClientId,
            byte[] voicePacket)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            foreach (
                NetworkClient client
                in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId ==
                    senderClientId)
                {
                    continue;
                }

                byte[] relayPacket =
                    new byte[
                        8 +
                        voicePacket.Length
                    ];

                Buffer.BlockCopy(
                    BitConverter.GetBytes(
                        senderClientId
                    ),
                    0,
                    relayPacket,
                    0,
                    8
                );

                Buffer.BlockCopy(
                    voicePacket,
                    0,
                    relayPacket,
                    8,
                    voicePacket.Length
                );

                Debug.Log(
                    $"[Voice][SEND] Relaying voice from {senderClientId} " +
                    $"to client {client.ClientId}. bytes={relayPacket.Length}"
                );

                transport.SendVoiceToClient(
                    client.ClientId,
                    relayPacket
                );
            }
        }

        private void ReceiveVoice(
            ulong ignoredSender,
            byte[] relayPacket)
        {
            if (relayPacket.Length < 17)
                return;

            ulong senderClientId =
                BitConverter.ToUInt64(
                    relayPacket,
                    0
                );

            byte[] voicePacket =
                new byte[
                    relayPacket.Length - 8
                ];

            Buffer.BlockCopy(
                relayPacket,
                8,
                voicePacket,
                0,
                voicePacket.Length
            );

            Debug.Log(
                $"[Voice][RECV] Unwrapped relay packet from server. " +
                $"originalSenderClientId={senderClientId}, bytes={voicePacket.Length}"
            );

            DecodeVoice(
                senderClientId,
                voicePacket
            );
        }

        private void DecodeVoice(
            ulong senderClientId,
            byte[] packet)
        {
            if (packet.Length < 10)
                return;

            if (packet[0] != 1)
                return;

            int sampleCount =
                BitConverter.ToUInt16(
                    packet,
                    1
                );

            short predictor =
                BitConverter.ToInt16(
                    packet,
                    3
                );

            byte stepIndex =
                packet[5];

            byte[] compressed =
                new byte[
                    packet.Length - 10
                ];

            Buffer.BlockCopy(
                packet,
                10,
                compressed,
                0,
                compressed.Length
            );

            short[] samples =
                VoiceAdpcm.Decode(
                    compressed,
                    sampleCount,
                    predictor,
                    stepIndex
                );

            VoicePlayer player =
                GetOrCreatePlayer(
                    senderClientId
                );

            Debug.Log(
                $"[Voice][RECV] Decoded voice from {senderClientId}. " +
                $"sampleCount={sampleCount}, pushing to player={player.name}"
            );

            player.PushSamples(
                samples
            );
        }

        public VoicePlayer RegisterPlayer(ulong clientId, Transform followTarget = null)
        {
            if (voicePlayers.TryGetValue(clientId, out VoicePlayer existing))
            {
                if (followTarget != null)
                {
                    existing.transform.SetParent(followTarget, false);
                    existing.transform.localPosition = Vector3.zero;
                }
                return existing;
            }

            Transform parent = followTarget != null ? followTarget : transform;

            GameObject playerObject = new GameObject("Voice_" + clientId);
            playerObject.transform.SetParent(parent, false);

            VoicePlayer voicePlayer = playerObject.AddComponent<VoicePlayer>();
            voicePlayer.Initialize(clientId);
            voicePlayer.SetVolume(defaultPlayerVolume);
            voicePlayer.Set3D(use3DVoice, voiceMinDistance, voiceMaxDistance);

            voicePlayers.Add(clientId, voicePlayer);

            return voicePlayer;
        }

        public void UnregisterPlayer(ulong clientId)
        {
            if (voicePlayers.TryGetValue(clientId, out VoicePlayer player))
            {
                if (player != null)
                    Destroy(player.gameObject);

                voicePlayers.Remove(clientId);
            }
        }

        // DecodeVoice still calls this as a fallback, now just delegating:
        private VoicePlayer GetOrCreatePlayer(ulong clientId)
        {
            return RegisterPlayer(clientId);
        }

        public void SetVoiceEnabled(
            bool enabled)
        {
            voiceActivation = enabled;
        }

        public void SetMuted(
            ulong clientId,
            bool muted)
        {
            if (voicePlayers.TryGetValue(
                clientId,
                out VoicePlayer player))
            {
                player.SetVolume(
                    muted ? 0f : defaultPlayerVolume
                );
            }
        }

        public void SetPlayerVolume(
            ulong clientId,
            float volume)
        {
            if (voicePlayers.TryGetValue(
                clientId,
                out VoicePlayer player))
            {
                player.SetVolume(volume);
            }
        }

        public void SetVoiceModifier(
            VoiceModifierType type)
        {
            voiceModifier.type = type;
        }

        public void SetVoicePitch(
            float pitch)
        {
            voiceModifier.pitch =
                Mathf.Clamp(
                    pitch,
                    0.5f,
                    2f
                );
        }

        public void SetVoiceVolume(
            float volume)
        {
            voiceModifier.volume =
                Mathf.Clamp(
                    volume,
                    0f,
                    2f
                );
        }

        public void SetRobotAmount(
            float amount)
        {
            voiceModifier.robotAmount =
                Mathf.Clamp01(amount);
        }

        public void SetDistortion(
            float amount)
        {
            voiceModifier.distortion =
                Mathf.Clamp01(amount);
        }

        public void SetRadioAmount(
            float amount)
        {
            voiceModifier.radioAmount =
                Mathf.Clamp01(amount);
        }

        public bool IsPlayerSpeaking(
            ulong clientId)
        {
            if (voicePlayers.TryGetValue(
                clientId,
                out VoicePlayer player))
            {
                return player.IsSpeaking;
            }

            return false;
        }

        public void SetPlayerPosition(
            ulong clientId,
            Vector3 position)
        {
            if (voicePlayers.TryGetValue(
                clientId,
                out VoicePlayer player))
            {
                player.SetPosition(position);
            }
        }

        public void SetProximityVoice(
            bool enabled)
        {
            use3DVoice = enabled;

            foreach (
                VoicePlayer player
                in voicePlayers.Values)
            {
                player.Set3D(
                    enabled,
                    voiceMinDistance,
                    voiceMaxDistance
                );
            }
        }

        private float[] ConvertToMono(
            float[] input,
            int channels)
        {
            int sampleCount =
                input.Length / channels;

            float[] mono =
                new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0f;

                for (int c = 0; c < channels; c++)
                {
                    sum +=
                        input[
                            i * channels + c
                        ];
                }

                mono[i] =
                    sum / channels;
            }

            return mono;
        }

        private float[] Resample(
            float[] input,
            int sourceRate,
            int targetRate)
        {
            if (sourceRate == targetRate)
                return input;

            int outputLength =
                Mathf.RoundToInt(
                    input.Length *
                    targetRate /
                    (float)sourceRate
                );

            float[] output =
                new float[outputLength];

            float ratio =
                sourceRate /
                (float)targetRate;

            for (int i = 0; i < outputLength; i++)
            {
                float source =
                    i * ratio;

                int index =
                    Mathf.FloorToInt(source);

                int next =
                    Mathf.Min(
                        index + 1,
                        input.Length - 1
                    );

                float t =
                    source - index;

                output[i] =
                    Mathf.Lerp(
                        input[index],
                        input[next],
                        t
                    );
            }

            return output;
        }

        private float CalculateRms(
            float[] samples)
        {
            if (samples.Length == 0)
                return 0f;

            double sum = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                sum +=
                    samples[i] *
                    samples[i];
            }

            return Mathf.Sqrt(
                (float)(
                    sum / samples.Length
                )
            );
        }

        private short[] FloatToPcm16(
            float[] samples)
        {
            short[] output =
                new short[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                float value =
                    Mathf.Clamp(
                        samples[i],
                        -1f,
                        1f
                    );

                output[i] =
                    (short)(
                        value *
                        32767f
                    );
            }

            return output;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null && networkEventsHooked)
            {
                NetworkManager.Singleton.OnServerStarted -= HandleNetworkSessionStarted;
                NetworkManager.Singleton.OnClientStarted -= HandleNetworkSessionStarted;
            }

            if (transport != null)
            {
                transport.OnVoiceDataReceived -=
                    OnVoiceDataReceived;
            }

            if (microphoneRunning)
            {
                Microphone.End(
                    microphoneDevice
                );
            }

            foreach (
                VoicePlayer player
                in voicePlayers.Values)
            {
                if (player != null)
                    Destroy(player.gameObject);
            }

            voicePlayers.Clear();

            if (Instance == this)
                Instance = null;
        }
    }
}