namespace ClickStitch.Core.Tests;

public sealed class MacroLibraryTests
{
    [Fact]
    public void Open_round_trips_a_saved_macro_and_a_copy_keeps_the_original()
    {
        string folder = Temp();
        try
        {
            MacroEditor editor = MacroEditor.Create();
            editor.SetName("Door");
            string repeat = editor.Add("repeat.count", null);
            editor.Add("wait", repeat);
            editor.SetField("0/0", "durationMs", "50");
            Assert.True(editor.TrySave(folder, out string path));

            MacroEditor again = MacroEditor.Open(MacroStore.Load(path).Document!);
            Assert.False(again.HasUnsavedChanges);
            Assert.Equal(editor.Id, again.Id);
            Assert.Equal("50", again.Steps[1].Fields[0].Value);
            again.MakeCopy();
            Assert.StartsWith("Copy of ", again.Name, StringComparison.Ordinal);
            Assert.NotEqual(editor.Id, again.Id);
            Assert.True(again.TrySave(folder, out _));
            Assert.Equal(2, Directory.GetFiles(folder).Length);
            Assert.Equal("Door", MacroStore.Load(path).Document!.Name);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void The_library_lists_sorts_renames_copies_and_deletes()
    {
        string folder = Temp();
        try
        {
            Save(folder, "beta", "Beta");
            Save(folder, "alpha", "Alpha");
            File.WriteAllText(Path.Combine(folder, "broken.clickstitch.json"), "{");

            IReadOnlyList<MacroFile> files = MacroLibrary.Read(folder);
            Assert.Equal(3, files.Count);
            MacroFile[] byName = MacroLibrary.Order(files, MacroSort.Name).ToArray();
            Assert.Equal("Alpha", byName[0].Name);
            Assert.Equal("broken.clickstitch.json", byName[2].Name);
            Assert.Contains("JSON", byName[2].Problem, StringComparison.Ordinal);

            MacroFile alpha = files.Single(file => file.Id == "alpha");
            Assert.True(MacroLibrary.Matches(alpha, "alp"));
            Assert.False(MacroLibrary.Matches(alpha, "door"));
            Assert.True(MacroLibrary.TryRename(alpha.Path, folder, "First", out string renamed, out _));
            Assert.Equal("First", MacroStore.Load(renamed).Document!.Name);
            Assert.False(File.Exists(alpha.Path));

            Assert.True(MacroLibrary.TryDuplicate(renamed, folder, out string copy, out _));
            MacroDocument duplicated = MacroStore.Load(copy).Document!;
            Assert.NotEqual("alpha", duplicated.Id);
            Assert.StartsWith("Copy of ", duplicated.Name, StringComparison.Ordinal);

            Assert.False(MacroLibrary.TryDelete(Path.Combine(Path.GetTempPath(), "other.clickstitch.json"), folder, out string outside));
            Assert.Contains("outside", outside, StringComparison.Ordinal);
            Assert.True(MacroLibrary.TryDelete(copy, folder, out _));
            Assert.False(File.Exists(copy));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Import_rejects_a_bad_file_and_export_writes_outside_the_library()
    {
        string folder = Temp();
        string outside = Temp();
        try
        {
            string bad = Path.Combine(outside, "bad.clickstitch.json");
            File.WriteAllText(bad, "{");
            Assert.False(MacroLibrary.TryImport(bad, folder, out _, out string error));
            Assert.Contains("JSON", error, StringComparison.Ordinal);
            Assert.Empty(MacroLibrary.Read(folder));

            string saved = Save(folder, "door", "Door");
            string destination = Path.Combine(outside, "door-copy.clickstitch.json");
            Assert.True(MacroLibrary.TryExport(saved, folder, destination, out _));
            Assert.Equal("Door", MacroStore.Load(destination).Document!.Name);
            Assert.True(File.Exists(saved));
            Assert.False(MacroLibrary.TryExport(saved, folder, Path.Combine(folder, "again.clickstitch.json"), out string inside));
            Assert.Contains("outside", inside, StringComparison.Ordinal);

            string empty = Temp();
            try
            {
                Assert.True(MacroLibrary.TryImport(destination, empty, out string imported, out _));
                Assert.Equal("door", MacroStore.Load(imported).Document!.Id);
            }
            finally
            {
                Directory.Delete(empty, true);
            }

            Assert.True(MacroLibrary.TryImport(destination, folder, out string second, out _));
            Assert.NotEqual("door", MacroStore.Load(second).Document!.Id);
        }
        finally
        {
            Directory.Delete(folder, true);
            Directory.Delete(outside, true);
        }
    }

    private static string Save(string folder, string id, string name)
    {
        MacroReadResult result = MacroJson.Read(
            $$"""
            {
              "formatVersion": 1,
              "id": "{{id}}",
              "name": "{{name}}",
              "actions": [
                { "id": "click", "type": "mouse.click", "button": "left", "coordinateMode": "cursor" }
              ]
            }
            """);
        return MacroStore.Save(result.Document!, folder);
    }

    private static string Temp()
    {
        string folder = Path.Combine(Path.GetTempPath(), "clickstitch-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
