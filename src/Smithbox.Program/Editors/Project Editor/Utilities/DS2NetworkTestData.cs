using System;
using System.IO;
using System.Linq;

namespace StudioCore.Application;

/// <summary>
/// Resolves the PS3 Network Test's loose USRDIR data and extracted GameData BXF4 archive.
/// The archive retains game-root paths; it is mounted beside USRDIR, not below it.
/// </summary>
public static class DS2NetworkTestData
{
    public static bool TryResolveDataRoots(string selectedPath, out string userDirectory, out string archiveDirectory)
    {
        userDirectory = null;
        archiveDirectory = null;

        if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            return false;

        var selected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedPath));
        var candidates = new[]
        {
            selected,
            Path.Combine(selected, "USRDIR"),
            Path.Combine(selected, "PS3_GAME", "USRDIR"),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(selected)).Equals("GameData-bhd", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(Path.TrimEndingDirectorySeparator(selected))?.FullName
                : null
        };

        foreach (var candidate in candidates)
        {
            if (IsDataRoot(candidate))
            {
                userDirectory = candidate;
                archiveDirectory = Path.Combine(candidate, "GameData-bhd");
                return true;
            }
        }

        // A package collection is accepted only when it contains exactly one matching package.
        var packages = Directory.EnumerateDirectories(selected)
            .Select(path => Path.Combine(path, "USRDIR"))
            .Where(IsDataRoot)
            .Take(2)
            .ToArray();

        if (packages.Length != 1)
            return false;

        userDirectory = packages[0];
        archiveDirectory = Path.Combine(userDirectory, "GameData-bhd");
        return true;
    }

    private static bool IsDataRoot(string path)
    {
        if (path == null || !File.Exists(Path.Combine(path, "regulation.bnd.dcx")))
            return false;

        var extracted = Path.Combine(path, "GameData-bhd");
        return Directory.Exists(Path.Combine(extracted, "map"))
            && Directory.Exists(Path.Combine(extracted, "model"))
            && Directory.Exists(Path.Combine(extracted, "param"));
    }
}
