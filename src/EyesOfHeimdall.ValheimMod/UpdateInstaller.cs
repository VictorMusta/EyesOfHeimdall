using System.Security.Cryptography;

namespace EyesOfHeimdall.ValheimMod;

internal static class UpdateInstaller
{
    // All-or-nothing: a half-applied update (new Core, old mod) would stop the plugin loading, and the updater with it.
    public static void Install(string pluginDir, UpdateManifest manifest, IReadOnlyDictionary<string, byte[]> downloads)
    {
        foreach (var (name, sha256) in manifest.Files)
        {
            if (!downloads.TryGetValue(name, out var bytes))
            {
                throw new InvalidDataException($"{name} was not downloaded");
            }

            if (!string.Equals(Sha256Of(bytes), sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{name} does not match the hash published in the manifest");
            }
        }

        var staged = new List<string>();
        var swapped = new List<(string Target, string Backup, bool HadOriginal)>();

        try
        {
            foreach (var (name, _) in manifest.Files)
            {
                var stagedPath = Path.Combine(pluginDir, name) + ".new";
                staged.Add(stagedPath);
                File.WriteAllBytes(stagedPath, downloads[name]);
            }

            foreach (var (name, _) in manifest.Files)
            {
                var target = Path.Combine(pluginDir, name);
                var backup = target + ".bak";
                var hadOriginal = File.Exists(target);

                if (hadOriginal)
                {
                    File.Delete(backup);
                    File.Move(target, backup);
                }

                swapped.Add((target, backup, hadOriginal));
                File.Move(target + ".new", target);
            }
        }
        catch (Exception e)
        {
            var restored = TryRollback(swapped);
            foreach (var path in staged)
            {
                TryDelete(path);
            }

            if (!restored)
            {
                throw new IOException("update failed and the previous files could not all be restored — reinstall the mod", e);
            }

            throw;
        }
    }

    private static bool TryRollback(List<(string Target, string Backup, bool HadOriginal)> swapped)
    {
        var restored = true;

        for (var i = swapped.Count - 1; i >= 0; i--)
        {
            var (target, backup, hadOriginal) = swapped[i];
            try
            {
                File.Delete(target);
                if (hadOriginal)
                {
                    File.Move(backup, target);
                }
            }
            catch (Exception)
            {
                restored = false;
            }
        }

        return restored;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    private static string Sha256Of(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
