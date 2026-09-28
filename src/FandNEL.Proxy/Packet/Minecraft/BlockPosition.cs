namespace FandNEL.Proxy.Packet.Minecraft;

/// <summary>Minecraft 网络 Position，使用 26 位 X/Z 和 12 位 Y 有符号坐标。</summary>
public readonly record struct BlockPosition(int X, int Y, int Z);
