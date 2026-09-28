using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using FandNEL.Proxy.Packet.Heypixel;

namespace FandNEL.Proxy.Heypixel;

public sealed class ClientFingerprintBuilder
{
    private const string ReportedGameDirectory = @"E:\MCLDownload\Game\.minecraft";
    private const string ReportedJavaHome = @"E:\MCLDownload\ext\jre-v64-220420\jdk17";
    private const string ReportedModsDirectory = @"E:\MCLDownload\Game\.minecraft\mods";

    private readonly string[] _processorInfo;
    private readonly string[][] _diskDriveInfo;
    private readonly string[] _systemProductInfo;
    private readonly string[] _accountDirectories;
    private readonly EncryptedFileFingerprint[] _encryptedModFiles;
    private readonly SaltedDesCipher _cipher;
    private readonly HeypixelClientIdentity _identity;
    private readonly List<KnownModDescriptor> _knownMods = KnownModCatalog.CreateSessionCopy();
    private readonly string[][] _networkAdapterInfo;
    private readonly Random _random;
    private readonly string _hardwarePlaceholder;

    public ClientFingerprintBuilder(HeypixelClientIdentity identity, SaltedDesCipher cipher)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(cipher);
        if (!long.TryParse(identity.UserId, NumberStyles.None, CultureInfo.InvariantCulture, out long userId))
        {
            throw new ArgumentException("Heypixel requires a numeric user ID.", nameof(identity));
        }

        _identity = identity;
        _cipher = cipher;
        _random = new Random(unchecked((int)userId));
        _hardwarePlaceholder = GenerateHardwarePlaceholder();
        _processorInfo = GenerateProcessorInfo();
        _systemProductInfo = GenerateSystemProductInfo();
        _networkAdapterInfo = GenerateNetworkAdapterInfo();
        _diskDriveInfo = GenerateDiskDriveInfo();
        _accountDirectories = GenerateAccountDirectories();
        _encryptedModFiles = BuildEncryptedModFileFingerprints();
        EncryptedUserToken = UserTokenCipher.Encrypt(identity.UserToken);
    }

    public string EncryptedUserToken { get; }

    public InfoReportData BuildInfoReport() => new(
        _knownMods.Select(mod => new KnownModFingerprint(mod.ModName, mod.ModPath, mod.ModHash)).ToArray(),
        ReportedGameDirectory, ReportedJavaHome, _processorInfo, _systemProductInfo, _networkAdapterInfo,
        _diskDriveInfo, _accountDirectories, long.Parse(_identity.UserId, CultureInfo.InvariantCulture),
        _encryptedModFiles.Select(file => new EncryptedModFingerprint(file.EncryptedPath, file.EncryptedSha1)).ToArray());

    private EncryptedFileFingerprint[] BuildEncryptedModFileFingerprints()
    {
        return _identity.ModDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.jar"))
            .Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(BuildEncryptedModFileFingerprint)
            .ToArray();
    }

    private EncryptedFileFingerprint BuildEncryptedModFileFingerprint(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        using var input = File.OpenRead(filePath);
        string sha1 = Convert.ToHexString(SHA1.HashData(input)).ToLowerInvariant();
        PatchKnownMod(filePath, fileName, sha1);

        byte[] reportedPath = Encoding.UTF8.GetBytes(ReportedModsDirectory + "\\" + fileName);
        byte[] reportedHash = Encoding.UTF8.GetBytes(sha1);
        return new EncryptedFileFingerprint
        {
            EncryptedPath = Convert.ToBase64String(_cipher.Encrypt(reportedPath)),
            EncryptedSha1 = Convert.ToBase64String(_cipher.Encrypt(reportedHash))
        };
    }

    private void PatchKnownMod(string filePath, string fileName, string sha1)
    {
        string? modsToml = ModArchiveMetadataReader.ReadModsToml(filePath);
        if (modsToml is null)
        {
            return;
        }

        string? modId = ModArchiveMetadataReader.ParseModId(modsToml);
        if (modId is null)
        {
            return;
        }

        KnownModDescriptor? knownMod = _knownMods.Find(mod => mod.ModName == modId);
        if (knownMod is null)
        {
            return;
        }

        knownMod.ModPath = ModArchiveMetadataReader.ReplaceFileName(knownMod.ModPath, fileName);
        knownMod.ModHash = sha1;
    }

    private string[] GenerateAccountDirectories()
    {
        List<string> directories = [];
        for (int index = 0; index < _random.Next(1, 3); index++)
        {
            string account = RandomIdentityGenerator.GenerateDigits(_random, _random.Next(12, 17)) + "@163.com";
            directories.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes("Game\\" + account)));
        }

        return directories.ToArray();
    }

    private string[][] GenerateDiskDriveInfo()
    {
        string[] drive =
        [
            GenerateDiskSerialNumber(),
            @"\\.\PHYSICALDRIVE0",
            SyntheticFingerprintData.StorageDeviceModels[
                _random.Next(SyntheticFingerprintData.StorageDeviceModels.Count)]
        ];
        return [drive];
    }

    private string GenerateDiskSerialNumber() =>
        (RandomIdentityGenerator.GenerateHex(_random, 2) + "_" +
         RandomIdentityGenerator.GenerateHex(_random, 2) + "_" +
         RandomIdentityGenerator.GenerateHex(_random, 2) + "_" +
         RandomIdentityGenerator.GenerateHex(_random, 2) + ".").ToUpper();

    private string[][] GenerateNetworkAdapterInfo()
    {
        List<(string Name, string MacAddress)> adapters = [];
        int adapterCount = _random.Next(1, 4);
        List<string> availableNames = new(SyntheticFingerprintData.NetworkAdapterNames);
        availableNames.Shuffle(_random);

        for (int index = 0; index < adapterCount; index++)
        {
            adapters.Add((
                availableNames[index],
                RandomIdentityGenerator.GenerateMacAddress(_random, 6)));
        }

        // The second shuffle is present in the original and advances the deterministic random stream.
        availableNames.Shuffle(_random);

        int interfaceIndex = 0;
        return adapters.Select(adapter => new[]
        {
            $"etc{++interfaceIndex}",
            adapter.Name,
            adapter.MacAddress,
            "[" + RandomIdentityGenerator.GeneratePrivateIpv4Address(_random) + "]",
            "[fe80:0:0:0:" + RandomIdentityGenerator.GenerateIpv6InterfaceIdentifier(_random) + "]"
        }).ToArray();
    }

    private string[] GenerateProcessorInfo() =>
        SyntheticFingerprintData.ProcessorProfiles[
            _random.Next(SyntheticFingerprintData.ProcessorProfiles.Count)].Split('|');

    private string[] GenerateSystemProductInfo()
    {
        string manufacturer = SyntheticFingerprintData.BaseboardManufacturers[
            _random.Next(SyntheticFingerprintData.BaseboardManufacturers.Count)];
        string identifyingNumber = GenerateSystemIdentifyingNumber();
        string version = _random.Next(2) == 1 ? _hardwarePlaceholder : "1.0";
        byte[] uuidBytes = new byte[16];
        _random.NextBytes(uuidBytes);

        return
        [
            manufacturer,
            "unknown",
            identifyingNumber,
            version,
            new Guid(uuidBytes).ToString().ToUpper()
        ];
    }

    private string GenerateHardwarePlaceholder() => _random.Next(4) switch
    {
        0 => "unknown",
        1 => "Standard",
        2 => "Not Applicable",
        _ => "Default string"
    };

    private string GenerateSystemIdentifyingNumber() => _random.Next(2) switch
    {
        0 => _hardwarePlaceholder,
        1 => RandomIdentityGenerator.GenerateAlphaNumeric(_random, _random.Next(8, 10)).ToUpper(),
        _ => "unknown"
    };

}
