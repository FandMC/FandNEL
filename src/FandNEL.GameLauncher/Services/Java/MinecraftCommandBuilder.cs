using System.Text;
using System.Text.Json;
using FandNEL.Core.Entities.WPFLauncher.Minecraft;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Skip32;
using FandNEL.Core.Utils;
using FandNEL.Core.Utils.Cipher;
using FandNEL.GameLauncher.Models;

namespace FandNEL.GameLauncher.Services.Java;

/// <summary>将网易版本清单转换为独立参数，避免路径空格和 JSON 引号被 shell 拆散。</summary>
internal static class MinecraftCommandBuilder
{
    public static ProcessLaunchSpec Build(JavaLaunchRequest request, LauncherPaths paths, string java, string runtime, int authPort, int rpcPort)
    {
        var version = MinecraftInstaller.VersionName(request.GameVersion);
        var versionRoot = Path.Combine(paths.Minecraft, "versions", version);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionRoot, version + ".json")),
            new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = manifest.RootElement;
        var uuid = new Skip32Cipher("SaintSteve"u8.ToArray()).GenerateRoleUuid(request.RoleName, uint.Parse(request.UserId));
        var userProperties = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["uid"] = new object[] { uint.Parse(request.UserId), 0 },
            ["gameid"] = new[] { 0, 0 },
            ["launcherport"] = new[] { rpcPort, 0 },
            ["filterkey"] = new[] { RandomUtil.GetRandomString(32, "abcdefghijklmnopqrstuvwxyz"), "0" },
            ["filterpath"] = new[] { string.Empty, "0" },
            ["timedelta"] = new[] { 0, 0 },
            ["launchversion"] = new[] { request.ProtocolVersion, "0" }
        });
        var placeholders = new Dictionary<string, string>
        {
            ["${version_name}"] = version,
            ["${assets_root}"] = Path.Combine(paths.Minecraft, "assets"),
            ["${assets_index_name}"] = root.TryGetProperty("assetIndex", out var index) && index.TryGetProperty("id", out var indexId) ? indexId.GetString()! : version,
            ["${auth_uuid}"] = uuid,
            ["${auth_access_token}"] = request.GameVersion >= EnumGameVersion.V_1_18 ? "0" : RandomUtil.GetRandomString(32, "ABCDEF0123456789"),
            ["${auth_player_name}"] = request.RoleName,
            ["${game_directory}"] = runtime,
            ["${user_properties}"] = userProperties,
            ["${user_type}"] = "legacy",
            ["${version_type}"] = "release",
            ["${natives_directory}"] = Path.Combine(versionRoot, "natives"),
            ["${library_directory}"] = Path.Combine(paths.Minecraft, "libraries"),
            ["${launcher_name}"] = "FandNEL",
            ["${launcher_version}"] = "1.0",
            ["${classpath_separator}"] = Path.PathSeparator.ToString()
        };
        placeholders["${classpath}"] = BuildClasspath(root, paths, versionRoot);
        var arguments = new List<string>
        {
            $"-DlauncherControlPort={authPort}", $"-DlauncherGameId={request.GameId}", $"-DuserId={request.UserId}",
            "-DToken=" + TokenUtil.GenerateEncryptToken(request.UserToken), "-DServer=RELEASE",
            $"-Xmx{request.MaxMemoryMb}M", "-Xmn128M",
            "-Djava.library.path=" + Path.Combine(versionRoot, "natives"),
            "-Druntime_path=" + Path.Combine(versionRoot, "natives", "runtime")
        };

        if (root.TryGetProperty("jvm_arguments", out var jvm) && root.TryGetProperty("parameter_arguments", out var game))
        {
            var provided = SplitArguments(jvm.GetString() ?? string.Empty);
            for (var i = 0; i < provided.Count; i++)
            {
                var token = Expand(provided[i]);
                if (token.StartsWith("-Xmx", StringComparison.Ordinal) || token.StartsWith("-Xmn", StringComparison.Ordinal))
                    continue;
                if (token.StartsWith("-DlibraryDirectory=", StringComparison.Ordinal))
                    token = "-DlibraryDirectory=" + Path.Combine(paths.Minecraft, "libraries");
                arguments.Add(token);
                if (token is "-cp" or "-classpath" or "-p" or "--module-path")
                {
                    if (++i == provided.Count)
                        throw new InvalidDataException($"JVM 参数 {token} 缺少值。");
                    arguments.Add(string.Join(Path.PathSeparator, Expand(provided[i]).Split(';')
                        .Select(path => Path.IsPathRooted(path) ? path : Path.Combine(paths.Minecraft, path))));
                }
            }
            arguments.AddRange(SplitArguments(game.GetString() ?? string.Empty).Select(Expand));
        }
        else
        {
            var jars = new List<string>();
            if (root.TryGetProperty("libraries", out var libraries))
            {
                foreach (var library in libraries.EnumerateArray())
                {
                    if (!IsAllowed(library))
                        continue;
                    string? relative = null;
                    if (library.TryGetProperty("downloads", out var downloads) && downloads.TryGetProperty("artifact", out var artifact) && artifact.TryGetProperty("path", out var path))
                        relative = path.GetString();
                    else if (library.TryGetProperty("name", out var name))
                    {
                        var parts = name.GetString()!.Split(':');
                        if (parts.Length >= 3 && !parts[1].Contains("platform", StringComparison.Ordinal))
                            relative = $"{parts[0].Replace('.', '/')}/{parts[1]}/{parts[2]}/{parts[1]}-{parts[2]}{(parts.Length > 3 ? "-" + parts[3] : string.Empty)}.jar";
                    }
                    if (relative is not null)
                        jars.Add(LauncherPaths.Child(Path.Combine(paths.Minecraft, "libraries"), relative));
                }
            }
            jars.Add(Path.Combine(versionRoot, version + ".jar"));
            placeholders["${classpath}"] = string.Join(Path.PathSeparator, jars);
            if (root.TryGetProperty("arguments", out var modern))
            {
                if (modern.TryGetProperty("jvm", out var modernJvm))
                    arguments.AddRange(ReadArguments(modernJvm).Where(value => !value.StartsWith("-Xmx", StringComparison.Ordinal)).Select(Expand));
                else
                    arguments.AddRange(["-cp", placeholders["${classpath}"]]);
                arguments.Add(root.GetProperty("mainClass").GetString() ?? throw new InvalidDataException("版本缺少主类。"));
                arguments.AddRange(ReadArguments(modern.GetProperty("game")).Select(Expand));
            }
            else
            {
                arguments.AddRange(["-cp", placeholders["${classpath}"], root.GetProperty("mainClass").GetString()!]);
                arguments.AddRange(SplitArguments(root.GetProperty("minecraftArguments").GetString() ?? string.Empty).Select(Expand));
            }
        }
        SetOption("--assetsDir", Path.Combine(paths.Minecraft, "assets"));
        SetOption("--gameDir", runtime);
        SetOption("--userProperties", userProperties);
        SetOption("--userPropertiesEx", JsonSerializer.Serialize(new EntityUserPropertiesEx
        {
            GameType = (int)request.GameType, Channel = "netease", TimeDelta = 0, IsFilter = true, LauncherVersion = request.ProtocolVersion
        }));
        SetOption("--server", request.ServerHost);
        SetOption("--port", request.ServerPort.ToString());
        return new ProcessLaunchSpec(java, runtime, arguments, new Dictionary<string, string?>());

        string Expand(string input)
        {
            foreach (var pair in placeholders)
                input = input.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
            return input;
        }
        void SetOption(string name, string value)
        {
            var position = arguments.IndexOf(name);
            if (position >= 0)
            {
                if (position + 1 == arguments.Count)
                    throw new InvalidDataException($"游戏参数 {name} 缺少值。");
                arguments[position + 1] = value;
            }
            else
                arguments.AddRange([name, value]);
        }
    }

    private static IEnumerable<string> ReadArguments(JsonElement array)
    {
        foreach (var argument in array.EnumerateArray())
        {
            if (argument.ValueKind == JsonValueKind.String)
                yield return argument.GetString()!;
            else if (IsAllowed(argument))
            {
                var value = argument.GetProperty("value");
                if (value.ValueKind == JsonValueKind.String)
                    yield return value.GetString()!;
                else
                    foreach (var item in value.EnumerateArray())
                        yield return item.GetString()!;
            }
        }
    }

    private static string BuildClasspath(JsonElement root, LauncherPaths paths, string versionRoot)
    {
        var jars = new List<string>();
        if (root.TryGetProperty("libraries", out var libraries))
        {
            foreach (var library in libraries.EnumerateArray())
            {
                if (!IsAllowed(library))
                    continue;
                string? relative = null;
                if (library.TryGetProperty("downloads", out var downloads) && downloads.TryGetProperty("artifact", out var artifact) && artifact.TryGetProperty("path", out var path))
                    relative = path.GetString();
                else if (library.TryGetProperty("name", out var name))
                {
                    var parts = name.GetString()!.Split(':');
                    if (parts.Length >= 3 && !parts[1].Contains("platform", StringComparison.Ordinal))
                        relative = $"{parts[0].Replace('.', '/')}/{parts[1]}/{parts[2]}/{parts[1]}-{parts[2]}{(parts.Length > 3 ? "-" + parts[3] : string.Empty)}.jar";
                }
                if (relative is not null)
                    jars.Add(LauncherPaths.Child(Path.Combine(paths.Minecraft, "libraries"), relative));
            }
        }
        jars.Add(Path.Combine(versionRoot, Path.GetFileName(versionRoot) + ".jar"));
        return string.Join(Path.PathSeparator, jars);
    }

    private static bool IsAllowed(JsonElement element)
    {
        if (!element.TryGetProperty("rules", out var rules))
            return true;
        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (rule.TryGetProperty("features", out var features) && features.EnumerateObject().Any(feature => feature.Value.GetBoolean()))
                continue;
            if (rule.TryGetProperty("os", out var os) && os.TryGetProperty("name", out var name) && name.GetString() != "windows")
                continue;
            allowed = rule.GetProperty("action").GetString() == "allow";
        }
        return allowed;
    }

    internal static List<string> SplitArguments(string command)
    {
        var result = new List<string>();
        var token = new StringBuilder();
        var quoted = false;
        var started = false;
        for (var index = 0; index < command.Length; index++)
        {
            var current = command[index];
            if (current == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (current == '\\' && index + 1 < command.Length && command[index + 1] == '"')
            {
                token.Append('"');
                index++;
                started = true;
            }
            else if (char.IsWhiteSpace(current) && !quoted)
            {
                if (started)
                {
                    result.Add(token.ToString());
                    token.Clear();
                    started = false;
                }
            }
            else
            {
                token.Append(current);
                started = true;
            }
        }
        if (quoted)
            throw new InvalidDataException("版本启动参数包含未闭合的双引号。");
        if (started)
            result.Add(token.ToString());
        return result;
    }
}
