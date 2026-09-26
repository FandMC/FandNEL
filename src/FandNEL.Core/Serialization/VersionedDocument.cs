namespace FandNEL.Core.Serialization;

/// <summary>
/// Version envelope used by persisted documents so migrations can be added without
/// changing the storage implementation.
/// </summary>
public sealed record VersionedDocument<T>(int Version, T Payload);
