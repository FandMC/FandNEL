namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record HeypixelSkinSyncPacket
{
    // 此通道原协议传递的是 Base64 文本本身，不能再次解码。
    public byte[] Write() => "ASQwMDAwMDAwMC0wMDAwLTQwMDAtODAwMC0wMDAwMzliYzYyMTM="u8.ToArray();
}
