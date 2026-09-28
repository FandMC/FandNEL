namespace FandNEL.Proxy.Packet.Minecraft.V1206;

public enum DisplayBillboard : byte { Fixed, Vertical, Horizontal, Center }

[Flags]
public enum TextDisplayFlags : byte
{
    None = 0,
    Shadow = 0x01,
    SeeThrough = 0x02,
    DefaultBackground = 0x04,
    AlignLeft = 0x08,
    AlignRight = 0x10
}

[Flags]
public enum ArmorStandFlags : byte
{
    None = 0,
    Small = 0x01,
    ShowArms = 0x04,
    NoBasePlate = 0x08,
    Marker = 0x10
}
