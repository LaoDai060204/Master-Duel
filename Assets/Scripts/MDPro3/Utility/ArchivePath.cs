using System;
using System.IO;

namespace MDPro3.Utility
{
    /// <summary>Resolves ZIP entry names within the selected extraction directory.</summary>
    public static class ArchivePath
    {
        public static string Resolve(string directory, string entryName)
        {
            if (string.IsNullOrEmpty(entryName))
                throw new InvalidDataException("The archive contains an empty entry name.");

            string entry = entryName.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(entry) || (entry.Length > 1 && entry[1] == ':'))
                throw new InvalidDataException("The archive contains an absolute path: " + entryName);

            string root = Path.GetFullPath(string.IsNullOrEmpty(directory) ? "." : directory);
            string target = Path.GetFullPath(Path.Combine(root, entry));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!target.StartsWith(prefix, comparison))
                throw new InvalidDataException("The archive entry escapes its extraction directory: " + entryName);
            return target;
        }
    }
}
