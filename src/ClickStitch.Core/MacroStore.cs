using System.Text;

namespace ClickStitch.Core;

/// <summary>
/// Saves and loads one format-1 macro file. The file name is a slug of the name plus the macro id.
/// </summary>
public static class MacroStore
{
    public const string Extension = ".clickstitch.json";

    /// <summary>
    /// The macros folder in Documents, so the files are easy to find.
    /// Settings stay under Local AppData. If Documents is unavailable, the folder is under Local AppData instead.
    /// </summary>
    public static string DefaultDirectory()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documents))
        {
            documents = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(documents, "ClickStitch");
    }

    /// <summary>
    /// Writes <paramref name="document"/> into <paramref name="directory"/> and returns the full path.
    /// An older file in that directory with the same macro id is removed.
    /// </summary>
    public static string Save(MacroDocument document, string directory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string folder = Path.GetFullPath(directory);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, FileName(document));
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The macro file name escapes the folder.");
        }

        foreach (string existing in Directory.EnumerateFiles(folder, "*" + Extension))
        {
            if (string.Equals(Path.GetFullPath(existing), fullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            MacroReadResult prior = Load(existing);
            if (prior.IsValid && prior.Document is not null && prior.Document.Id == document.Id)
            {
                File.Delete(existing);
            }
        }

        File.WriteAllText(fullPath, MacroJson.Write(document), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return fullPath;
    }

    public static MacroReadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            return new MacroReadResult(null, [new MacroIssue("document", "The macro file was not found.")]);
        }

        return MacroJson.Read(File.ReadAllBytes(path));
    }

    internal static string FileName(MacroDocument document)
    {
        string slug = Slug(document.Name);
        return slug + "-" + document.Id + Extension;
    }

    private static string Slug(string name)
    {
        var builder = new StringBuilder(name.Length);
        bool hyphen = false;
        foreach (char character in name.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                hyphen = false;
                continue;
            }

            if (!hyphen && builder.Length > 0)
            {
                builder.Append('-');
                hyphen = true;
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        if (builder.Length > 40)
        {
            builder.Length = 40;
            while (builder.Length > 0 && builder[^1] == '-')
            {
                builder.Length--;
            }
        }

        return builder.Length == 0 ? "macro" : builder.ToString();
    }
}
