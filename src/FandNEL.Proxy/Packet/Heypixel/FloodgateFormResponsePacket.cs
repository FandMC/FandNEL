using System.Text;
using FandNEL.Proxy.Packet.IO;

namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record FloodgateFormResponsePacket(ushort FormId, string Response)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public byte[] Write()
    {
        using var writer = new PacketWriter();
        return writer.WriteUnsignedShort(FormId).WriteBytes(StrictUtf8.GetBytes(Response)).ToArray();
    }
}
