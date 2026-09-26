using System.Text.Json;
using System.IO;
using System.Linq;
using FandNEL.Core.Entities.G79;
using FandNEL.Gateway;
using Serilog;

namespace FandNEL.Gateway.Management;

/// <summary>基岩版账号仓库和激活会话，与 Java users.json 完全分离。</summary>
public sealed class CppUserManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _usersLock = new();
    private readonly object _availableLock = new();
    private readonly string _usersFilePath;
    private List<ManagedUser> _users = [];
    private readonly List<ManagedAvailableUser> _availableUsers = [];

    public CppUserManager(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _usersFilePath = Path.Combine(dataDirectory, "cppusers.json");
    }

    public ManagedAvailableUser? GetAvailableUser(string userId)
    {
        lock (_availableLock) return _availableUsers.LastOrDefault(user => user.UserId == userId);
    }

    public IReadOnlyList<string> GetAvailableUsers()
    {
        lock (_availableLock)
            return _availableUsers.AsEnumerable().Reverse().Select(user => user.UserId).Distinct().ToArray();
    }

    public IReadOnlyList<ManagedAvailableUser> GetAvailableUserAccounts()
    {
        lock (_availableLock)
            return _availableUsers.AsEnumerable().Reverse().GroupBy(user => user.UserId)
                .Select(group => group.First()).ToArray();
    }

    public IReadOnlyList<ManagedUser> GetUsersNoDetails()
    {
        lock (_usersLock)
            return _users.Select(user => new ManagedUser
            {
                UserId = user.UserId,
                Authorized = user.Authorized,
                AutoLogin = false,
                Channel = user.Channel,
                Type = user.Type,
                Details = string.Empty,
                Platform = GatewayPlatform.Mobile,
                Alias = user.Alias
            }).ToArray();
    }

    public ManagedUser? GetUserById(string userId)
    {
        lock (_usersLock) return _users.LastOrDefault(user => user.UserId == userId);
    }

    public ManagedAvailableUser? GetLastAvailableUser()
    {
        lock (_availableLock) return _availableUsers.LastOrDefault();
    }

    public void AddUserToMaintain(EntityAuthenticationOtp authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        lock (_availableLock)
        {
            _availableUsers.RemoveAll(user => user.UserId == authentication.EntityId);
            _availableUsers.Add(new ManagedAvailableUser
            {
                UserId = authentication.EntityId,
                AccessToken = authentication.Token,
                LastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }

    public void AddUser(ManagedUser user, bool saveToDisk = true)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_usersLock)
        {
            user.Platform = GatewayPlatform.Mobile;
            var existing = _users.LastOrDefault(candidate => candidate.UserId == user.UserId);
            if (existing is null) _users.Add(user);
            else
            {
                existing.Authorized = true;
                existing.Channel = user.Channel;
                existing.Type = user.Type;
                existing.Details = user.Details;
                existing.Alias = user.Alias;
            }
            if (saveToDisk) SaveUsersToDiskUnsafe();
        }
    }

    public void RemoveUser(string userId)
    {
        lock (_usersLock)
        {
            if (_users.RemoveAll(user => user.UserId == userId) > 0) SaveUsersToDiskUnsafe();
        }
        lock (_availableLock) _availableUsers.RemoveAll(user => user.UserId == userId);
    }

    public void RemoveAvailableUser(string userId)
    {
        lock (_availableLock) _availableUsers.RemoveAll(user => user.UserId == userId);
        lock (_usersLock)
        {
            var user = _users.LastOrDefault(candidate => candidate.UserId == userId);
            if (user is not null)
            {
                user.Authorized = false;
                SaveUsersToDiskUnsafe();
            }
        }
    }

    public void ReadUsersFromDisk()
    {
        lock (_usersLock)
        {
            try
            {
                if (!File.Exists(_usersFilePath)) return;
                _users = (JsonSerializer.Deserialize<List<ManagedUser>>(File.ReadAllText(_usersFilePath), JsonOptions) ?? [])
                    .GroupBy(user => user.UserId, StringComparer.Ordinal)
                    .Select(group => group.Last()).ToList();
                foreach (var user in _users) user.Authorized = false;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to read Bedrock users from disk");
                _users = [];
            }
        }
    }

    public void SaveUsersToDisk()
    {
        lock (_usersLock) SaveUsersToDiskUnsafe();
    }

    private void SaveUsersToDiskUnsafe()
    {
        try { File.WriteAllText(_usersFilePath, JsonSerializer.Serialize(_users, JsonOptions)); }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save Bedrock users to disk");
            throw;
        }
    }
}
