using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace MGRMod.Loader;

/// <summary>
/// Stable bootstrap loaded by the game. The real mod is compiled once per
/// supported game ABI and lives below lib/&lt;version&gt;.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Bootstrap
{
    private const string ModId = "MGRMod";
    private const string ManifestFileName = "mgrmod-variants.manifest";
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        try
        {
            string loaderDirectory = Path.GetDirectoryName(
                typeof(Bootstrap).Assembly.Location) ??
                throw new InvalidOperationException("Could not resolve the MGR loader directory.");
            VariantEntry variant = SelectVariant(loaderDirectory);
            string payloadPath = ResolvePayloadPath(loaderDirectory, variant);
            VerifyHash(payloadPath, variant.Sha256);

            AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(
                typeof(Bootstrap).Assembly) ?? AssemblyLoadContext.Default;
            Assembly payload = context.LoadFromAssemblyPath(payloadPath);
            AssociateAssemblyWithMod(payload);
            InvokePayloadInitializer(payload);
            GD.Print($"[MGRMod.Loader] Loaded native {variant.CompatTarget} payload for host {HostVersionLabel() ?? "unknown"}.");
        }
        catch (Exception exception)
        {
            GD.PushError($"[MGRMod.Loader] Failed to load MGR: {exception}");
        }
    }

    private static VariantEntry SelectVariant(string loaderDirectory)
    {
        string manifestPath = Path.Combine(loaderDirectory, ManifestFileName);
        VariantManifest manifest = JsonSerializer.Deserialize<VariantManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The MGR variant manifest is empty.");
        List<(VariantEntry Entry, Version Version)> candidates = manifest.Variants
            .Select(entry =>
            {
                if (!TryParseVersion(entry.CompatTarget, out Version version))
                    throw new InvalidDataException($"Invalid MGR compatibility target: {entry.CompatTarget}");
                return (entry, version);
            })
            .OrderBy(candidate => candidate.version)
            .ToList();
        if (candidates.Count == 0)
            throw new InvalidDataException("The MGR variant manifest contains no variants.");

        Version? host = HostVersion();
        if (host is null)
            return candidates[^1].Entry;
        return candidates.LastOrDefault(candidate => candidate.Version <= host).Entry ??
            candidates[0].Entry;
    }

    private static string ResolvePayloadPath(
        string loaderDirectory,
        VariantEntry variant)
    {
        string libRoot = Path.GetFullPath(Path.Combine(loaderDirectory, "lib"));
        string directory = string.IsNullOrWhiteSpace(variant.Directory)
            ? Path.Combine("lib", variant.CompatTarget)
            : variant.Directory;
        string payloadPath = Path.GetFullPath(Path.Combine(
            loaderDirectory,
            directory,
            string.IsNullOrWhiteSpace(variant.Assembly) ? "MGRMod.dll" : variant.Assembly));
        string requiredPrefix = libRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!payloadPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The MGR variant path escapes the lib directory.");
        if (!File.Exists(payloadPath))
            throw new FileNotFoundException("The selected MGR payload is missing.", payloadPath);
        return payloadPath;
    }

    private static void VerifyHash(string payloadPath, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
            throw new InvalidDataException("The selected MGR payload has no SHA-256 entry.");
        string actualHash = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(payloadPath)));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA-256 mismatch for {payloadPath}.");
    }

    private static Version? HostVersion()
    {
        string? label = HostVersionLabel();
        return label is not null && TryParseVersion(label, out Version version)
            ? version
            : null;
    }

    private static string? HostVersionLabel()
    {
        string? assemblyDirectory = Path.GetDirectoryName(
            typeof(ModManager).Assembly.Location);
        string? assemblyReleaseInfo = assemblyDirectory is null
            ? null
            : Path.GetFullPath(Path.Combine(
                assemblyDirectory,
                "..",
                "release_info.json"));
        string? assemblyVersion = TryReadReleaseVersion(assemblyReleaseInfo);
        if (assemblyVersion is not null)
            return assemblyVersion;

        try
        {
            string executablePath = OS.GetExecutablePath();
            string executableDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
            string releaseInfoPath = OS.GetName() == "macOS"
                ? Path.GetFullPath(Path.Combine(executableDirectory, "..", "Resources", "release_info.json"))
                : Path.Combine(executableDirectory, "release_info.json");
            return TryReadReleaseVersion(releaseInfoPath);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadReleaseVersion(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("version", out JsonElement value)
                ? value.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseVersion(string text, out Version version)
    {
        string value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
            value = value[1..];
        int suffix = value.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            value = value[..suffix];
        return Version.TryParse(value, out version!);
    }

    private static void AssociateAssemblyWithMod(Assembly payload)
    {
        MethodInfo? associate = typeof(ModManager).GetMethod(
            "AssociateAssemblyWithMod",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            [typeof(string), typeof(Assembly)],
            modifiers: null);
        if (associate is not null)
        {
            associate.Invoke(null, [ModId, payload]);
            return;
        }

        foreach (Mod mod in ModManager.Mods)
        {
            object? manifest = typeof(Mod).GetField(
                    "manifest",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(mod);
            string? id = manifest?.GetType().GetField(
                    "id",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(manifest) as string;
            if (!string.Equals(id, ModId, StringComparison.Ordinal))
                continue;

            if (typeof(Mod).GetField(
                    "assemblies",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(mod) is IList assemblies)
            {
                if (!assemblies.Contains(payload))
                    assemblies.Add(payload);
                return;
            }

            FieldInfo? assemblyField = typeof(Mod).GetField(
                "assembly",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (assemblyField is not null && assemblyField.FieldType.IsInstanceOfType(payload))
            {
                assemblyField.SetValue(mod, payload);
                ReassociateAfterLegacyModLoad(mod, payload, assemblyField);
                return;
            }

            throw new MissingMemberException(
                typeof(Mod).FullName,
                "assemblies or assembly");
        }

        throw new InvalidOperationException($"Could not find the {ModId} mod record.");
    }

    private static void ReassociateAfterLegacyModLoad(
        Mod targetMod,
        Assembly payload,
        FieldInfo assemblyField)
    {
        Action<Mod>? reassociate = null;
        reassociate = detectedMod =>
        {
            if (!ReferenceEquals(detectedMod, targetMod))
                return;

            assemblyField.SetValue(detectedMod, payload);
            ModManager.OnModDetected -= reassociate;
        };
        ModManager.OnModDetected += reassociate;
    }

    private static void InvokePayloadInitializer(Assembly payload)
    {
        Type entry = payload.GetType("MGRMod.Entry", throwOnError: true) ??
            throw new TypeLoadException("MGRMod.Entry was not found in the selected payload.");
        MethodInfo initialize = entry.GetMethod(
            "Initialize",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new MissingMethodException(entry.FullName, "Initialize");
        initialize.Invoke(null, null);
    }

    private sealed class VariantManifest
    {
        public int Schema { get; init; }
        public List<VariantEntry> Variants { get; init; } = [];
    }

    private sealed class VariantEntry
    {
        public string CompatTarget { get; init; } = string.Empty;
        public string Directory { get; init; } = string.Empty;
        public string Assembly { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
    }
}
