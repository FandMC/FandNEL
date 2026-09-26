namespace FandNEL.Proxy.Protocol;

public enum ConnectionState
{
    Handshaking = 0,
    Status = 1,
    Login = 2,
    Play = 3,
    Configuration = 4
}

public enum PacketDirection
{
    ServerBound,
    ClientBound
}

public enum ProtocolVersion
{
    Unknown = -1,
    V1076 = 5,
    V108X = 47,
    V1122 = 340,
    V1165 = 754,
    V1180 = 757,
    V1200 = 763,
    V1206 = 766,
    V1210 = 767,
    V1218 = 772,
    V12110 = 773
}
