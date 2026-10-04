using System.Text.RegularExpressions;

namespace EyesOfHeimdall.ValheimMod;

internal sealed class UpdateManifest
{
    // Network input: bare "*.dll" names only, so a manifest can never write outside the plugin folder.
    private static readonly Regex SafeDllName = new(@"^[A-Za-z0-9_.\-]+\.dll$", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256Hex = new(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

    private UpdateManifest(System.Version version, IReadOnlyList<(string Name, string Sha256)> files)
    {
        Version = version;
        Files = files;
    }

    // System.Version spelled out everywhere: Valheim declares its own global "Version" class, which would win.
    public System.Version Version { get; }
    public IReadOnlyList<(string Name, string Sha256)> Files { get; }

    public static UpdateManifest Parse(string text)
    {
        System.Version? version = null;
        var files = new List<(string Name, string Sha256)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.TrimStart('﻿').Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw new FormatException("manifest line is not key=value");
            }

            var key = line.Substring(0, separator).Trim();
            var value = line.Substring(separator + 1).Trim();

            if (key == "version")
            {
                if (!System.Version.TryParse(value, out version))
                {
                    throw new FormatException("manifest version is not a valid version number");
                }

                continue;
            }

            if (!SafeDllName.IsMatch(key) || !Sha256Hex.IsMatch(value) || !seen.Add(key))
            {
                throw new FormatException("manifest file entry is not a unique '<name>.dll=<sha256>' pair");
            }

            files.Add((key, value));
        }

        if (version == null || files.Count == 0)
        {
            throw new FormatException("manifest must declare a version and at least one file");
        }

        return new UpdateManifest(version, files);
    }
}
