namespace ClickStitch.Core;

public enum MacroSort
{
    Name,
    Newest,
}

/// <summary>One macro file in the library folder. A problem means the file could not be read as a macro.</summary>
public sealed class MacroFile
{
    internal MacroFile(string path, string id, string name, string description, int actionCount, DateTime modifiedUtc, string problem)
    {
        Path = path;
        Id = id;
        Name = name;
        Description = description;
        ActionCount = actionCount;
        ModifiedUtc = modifiedUtc;
        Problem = problem;
    }

    public string Path { get; }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public int ActionCount { get; }

    public DateTime ModifiedUtc { get; }

    public string Problem { get; }
}

/// <summary>
/// Lists, renames, copies, deletes, imports, and exports macros in one folder.
/// It does not run them.
/// </summary>
public static class MacroLibrary
{
    public static IReadOnlyList<MacroFile> Read(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string folder = System.IO.Path.GetFullPath(directory);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var files = new List<MacroFile>();
        foreach (string path in Directory.EnumerateFiles(folder, "*" + MacroStore.Extension))
        {
            string full = System.IO.Path.GetFullPath(path);
            if (!IsInside(full, folder))
            {
                continue;
            }

            DateTime modified = File.GetLastWriteTimeUtc(full);
            MacroReadResult loaded = MacroStore.Load(full);
            if (loaded.Document is not MacroDocument document)
            {
                string problem = loaded.Issues.Count == 0 ? "This file could not be read." : loaded.Issues[0].Message;
                files.Add(new MacroFile(full, "", System.IO.Path.GetFileName(full), "", 0, modified, problem));
                continue;
            }

            files.Add(new MacroFile(full, document.Id, document.Name, document.Description, document.Actions.Count, modified, ""));
        }

        return files;
    }

    public static bool Matches(MacroFile file, string query)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        string term = query.Trim();
        return file.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || file.Description.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<MacroFile> Order(IReadOnlyList<MacroFile> files, MacroSort sort)
    {
        ArgumentNullException.ThrowIfNull(files);
        return sort == MacroSort.Newest
            ? files.OrderByDescending(static file => file.ModifiedUtc).ThenBy(static file => file.Name, StringComparer.OrdinalIgnoreCase).ToArray()
            : files.OrderBy(static file => file.Name, StringComparer.OrdinalIgnoreCase).ThenBy(static file => file.Id, StringComparer.Ordinal).ToArray();
    }

    public static bool TryRename(string path, string directory, string name, out string savedPath, out string error)
    {
        savedPath = "";
        if (!TryDocument(path, directory, out MacroEditor? editor, out error))
        {
            return false;
        }

        editor!.SetName(name);
        return Save(editor, directory, out savedPath, out error);
    }

    public static bool TryDuplicate(string path, string directory, out string savedPath, out string error)
    {
        savedPath = "";
        if (!TryDocument(path, directory, out MacroEditor? editor, out error))
        {
            return false;
        }

        editor!.MakeCopy();
        return Save(editor, directory, out savedPath, out error);
    }

    public static bool TryDelete(string path, string directory, out string error)
    {
        if (!TryInside(path, directory, out string full, out _, out error))
        {
            return false;
        }

        if (!full.EndsWith(MacroStore.Extension, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            error = "That macro file was not found.";
            return false;
        }

        File.Delete(full);
        error = "";
        return true;
    }

    /// <summary>Copies a macro into the library. An id that is already there becomes a new copy.</summary>
    public static bool TryImport(string sourcePath, string directory, out string savedPath, out string error)
    {
        savedPath = "";
        error = "";
        ArgumentException.ThrowIfNullOrEmpty(directory);
        MacroReadResult loaded = MacroStore.Load(sourcePath);
        if (loaded.Document is not MacroDocument document)
        {
            error = loaded.Issues.Count == 0 ? "This file could not be read." : loaded.Issues[0].Message;
            return false;
        }

        MacroEditor editor = MacroEditor.Open(document);
        foreach (MacroFile existing in Read(directory))
        {
            if (existing.Id == document.Id)
            {
                editor.MakeCopy();
                break;
            }
        }

        return Save(editor, directory, out savedPath, out error);
    }

    /// <summary>Copies the file outside the library. The library file stays.</summary>
    public static bool TryExport(string path, string directory, string destination, out string error)
    {
        if (!TryInside(path, directory, out string full, out string folder, out error))
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrEmpty(destination);
        string target = System.IO.Path.GetFullPath(destination);
        if (IsInside(target, folder))
        {
            error = "Export writes a copy outside the library.";
            return false;
        }

        string? parent = System.IO.Path.GetDirectoryName(target);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            error = "The export folder was not found.";
            return false;
        }

        File.Copy(full, target, overwrite: true);
        error = "";
        return true;
    }

    private static bool TryDocument(string path, string directory, out MacroEditor? editor, out string error)
    {
        editor = null;
        if (!TryInside(path, directory, out string full, out _, out error))
        {
            return false;
        }

        MacroReadResult loaded = MacroStore.Load(full);
        if (loaded.Document is not MacroDocument document)
        {
            error = loaded.Issues.Count == 0 ? "This file could not be read." : loaded.Issues[0].Message;
            return false;
        }

        editor = MacroEditor.Open(document);
        error = "";
        return true;
    }

    private static bool Save(MacroEditor editor, string directory, out string savedPath, out string error)
    {
        if (!editor.TrySave(directory, out savedPath))
        {
            error = editor.DocumentProblems.Count > 0 ? editor.DocumentProblems[0] : "The macro could not be saved.";
            savedPath = "";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryInside(string path, string directory, out string full, out string folder, out string error)
    {
        full = "";
        folder = "";
        error = "";
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        folder = System.IO.Path.GetFullPath(directory);
        full = System.IO.Path.GetFullPath(path);
        if (!IsInside(full, folder))
        {
            error = "That file is outside the library.";
            return false;
        }

        return true;
    }

    private static bool IsInside(string full, string folder) =>
        full.StartsWith(folder + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
