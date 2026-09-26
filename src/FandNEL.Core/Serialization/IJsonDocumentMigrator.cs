using System.Text.Json;

namespace FandNEL.Core.Serialization;

/// <summary>
/// Converts an older persisted payload into the current domain model.
/// Migration is read-only; the next successful write commits the new version.
/// </summary>
public interface IJsonDocumentMigrator<T>
{
    T Migrate(int storedVersion, JsonElement payload);
}
