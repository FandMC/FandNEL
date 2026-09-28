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
    private readonly ConcurrentDictionary<string, ManagedUser> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ManagedAvailableUser> _availableUsers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshGates = new(StringComparer.Ordinal);
    private readonly object _availableGate = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Timer _saveTimer;
    private readonly Task _maintainTask;
    private long _changeVersion;
    private long _savedVersion;
    private int _disposed;

    public UserManager(WPFLauncher launcher, string dataDirectory)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _usersFilePath = Path.Combine(dataDirectory, "users.json");
        ReadUsersFromDisk();
        _saveTimer = new Timer(_ =>
        {
            try { SaveUsersToDiskIfDirtyAsync().GetAwaiter().GetResult(); }
            catch (Exception exception) { Log.Error(exception, "Failed to save Java users"); }
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _maintainTask = MaintainAsync(_shutdown.Token);
    }

    public event Action<string, string>? TokenUpdated;
    public event Action<string>? TokenRemoved;

    public ManagedAvailableUser? GetAvailableUser(string userId) =>
        _availableUsers.TryGetValue(userId, out var user) ? user : null;

    public string GetAccessToken(string userId)
    {
        var user = GetAvailableUser(userId);
        return !string.IsNullOrWhiteSpace(user?.AccessToken)
            ? user.AccessToken
            : throw new InvalidOperationException("账号会话已失效，请重新激活。");
    }

    public async Task<ManagedAvailableUser?> RefreshAvailableUserAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var refreshGate = _refreshGates.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var user = GetAvailableUser(userId);
            if (user is null || !NeedsRefresh(user)) return user;

            EntityAuthenticationUpdate? updated;
            try
            {
                updated = await _launcher.AuthenticationUpdateAsync(userId, user.AccessToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_availableGate)
                {
                    var current = GetAvailableUser(userId);
                    if (!ReferenceEquals(current, user)) return current;
                }
                throw;
            }

            lock (_availableGate)
            {
                var current = GetAvailableUser(userId);
                if (!ReferenceEquals(current, user)) return current;
                if (string.IsNullOrWhiteSpace(updated?.Token))
                    throw new InvalidOperationException("账号 token 刷新失败，请稍后重试。");

                // 只替换本次请求对应的会话，不能覆盖重新激活的 token 或复活已停用账号。
                user = new ManagedAvailableUser
                {
                    UserId = userId,
                    AccessToken = updated.Token,
                    LastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                _availableUsers[userId] = user;
                TokenUpdated?.Invoke(userId, updated.Token);
            }
            Log.Information("Refreshed Java account token for {UserId}", userId);
            return user;
        }
        finally { refreshGate.Release(); }
    }

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
        _users[user.UserId] = user;
        if (saveToDisk) MarkDirtyAndScheduleSave();
    }

    public void AddUserToMaintain(string userId, string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_availableGate)
        {
            _availableUsers[userId] = new ManagedAvailableUser
                { UserId = userId, AccessToken = accessToken, LastLoginTime = now };
            TokenUpdated?.Invoke(userId, accessToken);
        }
    }

    public void AddUserToMaintain(EntityAuthenticationOtp authentication) =>
        AddUserToMaintain(authentication.EntityId, authentication.Token);

    public void RemoveUser(string userId)
    {
        if (_users.TryRemove(userId, out _)) MarkDirtyAndScheduleSave();
        RemoveAvailableUser(userId);
    }

    public void RemoveAvailableUser(string userId)
    {
        lock (_availableGate)
        {
            _availableUsers.TryRemove(userId, out _);
            TokenRemoved?.Invoke(userId);
        }
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
                stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Java 账号文件内容不能为空。");
            if (users.Any(user => user is null || string.IsNullOrWhiteSpace(user.UserId)
                    || user.Channel is null || user.Type is null || user.Details is null)
                || users.Select(user => user.UserId).Distinct(StringComparer.Ordinal).Count() != users.Count)
                throw new InvalidDataException("Java 账号文件包含无效或重复账号。");
            _users.Clear();
            foreach (var user in users)
            {
                user.Authorized = false;
                _users.TryAdd(user.UserId, user);
            }
            Log.Information("Loaded {Count} Java users from disk", users.Count);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Java 账号文件格式错误，未覆盖 users.json。", exception);
        }
    }

    public void ReadUsersFromDisk() => ReadUsersFromDiskAsync().GetAwaiter().GetResult();

    public void MarkDirtyAndScheduleSave()
    {
        Interlocked.Increment(ref _changeVersion);
        _saveTimer.Change(SaveDebounce, Timeout.InfiniteTimeSpan);
    }

    public async Task SaveUsersToDiskAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _changeVersion);
        await SaveUsersToDiskIfDirtyAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SaveUsersToDisk() => SaveUsersToDiskAsync().GetAwaiter().GetResult();

    private async Task MaintainAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var expired = _availableUsers.Values.Where(NeedsRefresh).ToArray();
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
            await RefreshAvailableUserAsync(user.UserId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Log.Error(exception, "Failed to refresh Java account token for {UserId}", user.UserId);
        }
    }

    private static bool NeedsRefresh(ManagedAvailableUser user) =>
        user.LastLoginTime <= DateTimeOffset.UtcNow.Subtract(RefreshAfter).ToUnixTimeMilliseconds();

    private async Task SaveUsersToDiskIfDirtyAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _changeVersion) == Volatile.Read(ref _savedVersion)) return;
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (Volatile.Read(ref _changeVersion) != Volatile.Read(ref _savedVersion))
            {
                long version = Volatile.Read(ref _changeVersion);
                string temporaryPath = $"{_usersFilePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                                     FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        await JsonSerializer.SerializeAsync(stream, _users.Values.ToArray(), JsonOptions,
                            cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        stream.Flush(flushToDisk: true);
                    }
                    if (File.Exists(_usersFilePath))
                        File.Replace(temporaryPath, _usersFilePath, destinationBackupFileName: null);
                    else
                        File.Move(temporaryPath, _usersFilePath);
                    Volatile.Write(ref _savedVersion, version);
                }
                finally
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        Log.Warning(exception, "Failed to remove temporary Java users file");
                    }
                }
            }
        }
        finally { _saveGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _shutdown.Cancel();
        try { await _maintainTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        await _saveTimer.DisposeAsync().ConfigureAwait(false);
        await SaveUsersToDiskIfDirtyAsync().ConfigureAwait(false);
        _shutdown.Dispose();
        _saveGate.Dispose();
    }
}
