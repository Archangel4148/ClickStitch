namespace ClickStitch.Core.Tests;

public sealed class MacroStoreTests
{
    [Fact]
    public void The_default_folder_is_ClickStitch_under_Documents()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Assert.Equal(Path.Combine(documents, "ClickStitch"), MacroStore.DefaultDirectory());
    }

    [Fact]
    public void Save_and_load_round_trip_and_a_renamed_id_replaces_the_old_file()
    {
        string folder = Path.Combine(Path.GetTempPath(), "clickstitch-" + Guid.NewGuid().ToString("N"));
        try
        {
            MacroDocument alpha = Read("alpha", "Hello, World!");
            MacroDocument other = Read("other", "Hello, World!");
            string alphaPath = MacroStore.Save(alpha, folder);
            string otherPath = MacroStore.Save(other, folder);

            Assert.Equal("hello-world-alpha.clickstitch.json", Path.GetFileName(alphaPath));
            Assert.Equal("hello-world-other.clickstitch.json", Path.GetFileName(otherPath));
            Assert.Equal(2, Directory.GetFiles(folder).Length);

            MacroReadResult loaded = MacroStore.Load(alphaPath);
            Assert.True(loaded.IsValid);
            Assert.Equal("Hello, World!", loaded.Document!.Name);
            Assert.Equal("alpha", loaded.Document.Id);
            Assert.IsType<MouseClickAction>(Assert.Single(loaded.Document.Actions));

            MacroDocument renamed = Read("alpha", "Beta");
            string renamedPath = MacroStore.Save(renamed, folder);

            Assert.Equal("beta-alpha.clickstitch.json", Path.GetFileName(renamedPath));
            Assert.False(File.Exists(alphaPath));
            Assert.True(File.Exists(otherPath));
            Assert.Equal("Beta", MacroStore.Load(renamedPath).Document!.Name);

            MacroReadResult missing = MacroStore.Load(Path.Combine(folder, "missing.clickstitch.json"));
            Assert.False(missing.IsValid);
            Assert.Equal("The macro file was not found.", missing.Issues[0].Message);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    private static MacroDocument Read(string id, string name)
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
        Assert.True(result.IsValid, result.Issues.Count == 0 ? "" : result.Issues[0].Message);
        return result.Document!;
    }
}
