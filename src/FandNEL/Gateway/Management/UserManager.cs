using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using FandNEL.Core.Entities.WPFLauncher;
using FandNEL.Core.Protocol;
using FandNEL.Gateway;
using Serilog;

namespace FandNEL.Gateway.Management;

/// <summary>Java 版账号仓库及已激活会话管理，按 Gateway 的 users.json 格式持久化。</summary>
public sealed class UserManager : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(1);

    private readonly string _usersFilePath;
    private readonly WPFLauncher _launcher;
    private readonly Action<string, string>? _tokenUpdated;
    private readonly ConcurrentDictionary<string, ManagedUser> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ManagedAvailableUser> _availableUsers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Timer _saveTimer;
    private readonly Task _maintainTask;
    private volatile bool _isDirty;

    public UserManager(WPFLauncher launcher, string dataDirectory, Action<string, string>? tokenUpdated = null)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _tokenUpdated = tokenUpdated;
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _usersFilePath = Path.Combine(dataDirectory, "users.json");
        _saveTimer = new Timer(async _ =>
        {
            try { await SaveUsersToDiskIfDirtyAsync().ConfigureAwait(false); }
            catch (Exception exception) { Log.Error(exception, "Failed to save Java users"); }
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _maintainTask = MaintainAsync(_shutdown.Token);
    }

    public ManagedAvailableUser? GetAvailableUser(string userId) =>
        _availableUsers.TryGetValue(userId, out var user) ? user : null;

    public ManagedAvailableUser? GetLastAvailableUser() => _availableUsers.Values.LastOrDefault();

    public IReadOnlyList<ManagedAvailableUser> GetAvailableUserAccounts() =>
        _availableUsers.Values
            .OrderByDescending(user => user.LastLoginTime)
            .ToArray();

    public IReadOnlyList<ManagedUser> GetUsersNoDetails() => _users.Values.Select(user => new ManagedUser
    {
        UserId = user.UserId,
        Authorized = user.Authorized,
        AutoLogin = false,
        Channel = user.Channel,
        Type = user.Type,
        Details = string.Empty,
        Platform = user.Platform,
        Alias = user.Alias
    }).ToArray();

    public ManagedUser? GetUserById(string userId) =>
        _users.TryGetValue(userId, out var user) ? user : null;

    public void AddUser(ManagedUser user, bool saveToDisk = true)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Platform = GatewayPlatform.Desktop;
        _users.AddOrUpdate(user.UserId, user, (_, existing) =>
        {
            existing.Authorized = true;
            existing.Channel = user.Channel;
            existing.Type = user.Type;
            existing.Details = user.Details;
            existing.Alias = user.Alias;
            return existing;
        });
        if (saveToDisk) MarkDirtyAndScheduleSave();
    }

    public void AddUserToMaintain(string userId, string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _availableUsers.AddOrUpdate(userId,
            _ => new ManagedAvailableUser { UserId = userId, AccessToken = accessToken, LastLoginTime = now },
            (_, current) =>
            {
                current.AccessToken = accessToken;
                current.LastLoginTime = now;
                return current;
            });
        _tokenUpdated?.Invoke(userId, accessToken);
    }

    public void AddUserToMaintain(EntityAuthenticationOtp authentication) =>
        AddUserToMaintain(authentication.EntityId, authentication.Token);

    public void RemoveUser(string userId)
    {
        if (_users.TryRemove(userId, out _)) MarkDirtyAndScheduleSave();
        _availableUsers.TryRemove(userId, out _);
    }

    public void RemoveAvailableUser(string userId)
    {
        _availableUsers.TryRemove(userId, out _);
        if (_users.TryGetValue(userId, out var user))
        {
            user.Authorized = false;
            MarkDirtyAndScheduleSave();
        }
    }

    public async Task ReadUsersFromDiskAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_usersFilePath)) return;
            await using var stream = File.OpenRead(_usersFilePath);
            var users = await JsonSerializer.DeserializeAsync<List<ManagedUser>>(
                stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
            _users.Clear();
            foreach (var user in users)
            {
                user.Authorized = false;
                _users.TryAdd(user.UserId, user);
            }
            Log.Information("Loaded {Count} Java users from disk", users.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Error(exception, "Failed to read Java users from disk");
            _users.Clear();
        }
    }

    public void ReadUsersFromDisk() => ReadUsersFromDiskAsync().GetAwaiter().GetResult();

    public void MarkDirtyAndScheduleSave()
    {
        _isDirty = true;
        _saveTimer.Change(SaveDebounce, Timeout.InfiniteTimeSpan);
    }

    public async Task SaveUsersToDiskAsync(CancellationToken cancellationToken = default)
    {
        _isDirty = true;
        await SaveUsersToDiskIfDirtyAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SaveUsersToDisk() => SaveUsersToDiskAsync().GetAwaiter().GetResult();

    private async Task MaintainAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                long threshold = DateTimeOffset.UtcNow.Subtract(RefreshAfter).ToUnixTimeMilliseconds();
                var expired = _availableUsers.Values.Where(user => user.LastLoginTime < threshold).ToArray();
                await Task.WhenAll(expired.Select(user => RefreshUserAsync(user, cancellationToken))).ConfigureAwait(false);
                await Task.Delay(RefreshInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Log.Error(exception, "Error while maintaining Java user tokens");
                try { await Task.Delay(RefreshInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task RefreshUserAsync(ManagedAvailableUser user, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _launcher.AuthenticationUpdateAsync(user.UserId, user.AccessToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(updated?.Token))
            {
                Log.Warning("Java account {UserId} token refresh returned no token", user.UserId);
                return;
            }
            if (_availableUsers.TryGetValue(user.UserId, out var current) && ReferenceEquals(current, user))
            {
                current.AccessToken = updated.Token;
                current.LastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _tokenUpdated?.Invoke(current.UserId, current.AccessToken);
                Log.Information("Refreshed Java account token for {UserId}", user.UserId);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Log.Error(exception, "Failed to refresh Java account token for {UserId}", user.UserId);
        }
    }

    private async Task SaveUsersToDiskIfDirtyAsync(CancellationToken cancellationToken = default)
    {
        if (!_isDirty) return;
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isDirty) return;
            var contents = JsonSerializer.Serialize(_users.Values.ToArray(), JsonOptions);
            await File.WriteAllTextAsync(_usersFilePath, contents, cancellationToken).ConfigureAwait(false);
            _isDirty = false;
        }
        finally { _saveGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        try { await _maintainTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _saveTimer.Dispose();
        await SaveUsersToDiskIfDirtyAsync().ConfigureAwait(false);
        _shutdown.Dispose();
        _saveGate.Dispose();
    }
}
