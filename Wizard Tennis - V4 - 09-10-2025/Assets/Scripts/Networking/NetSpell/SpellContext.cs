using Unity.Netcode;
using Unity.Collections;

public struct SpellContext : INetworkSerializable
{
    public ulong casterId;
    public ulong victimId;
    public FixedString64Bytes spellName;
    public FixedString64Bytes address;

    // Correct constraints for Netcode
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref casterId);
        serializer.SerializeValue(ref victimId);
        serializer.SerializeValue(ref spellName);
        serializer.SerializeValue(ref address);
    }
}
