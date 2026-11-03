namespace ClickStitch.Core.Tests;

public sealed class MacroEditorTests
{
    [Fact]
    public void A_new_macro_is_empty_valid_and_unchanged()
    {
        MacroEditor editor = MacroEditor.Create();

        Assert.Equal("Untitled macro", editor.Name);
        Assert.Equal("", editor.Description);
        Assert.Empty(editor.Steps);
        Assert.False(editor.HasUnsavedChanges);
        Assert.False(editor.CanUndo);
        Assert.Contains("no actions yet", editor.Preview, StringComparison.Ordinal);
        Assert.NotNull(editor.Document);
        Assert.Empty(editor.Document!.Actions);
        Assert.Equal(editor.Id, editor.Document.Id);
    }

    [Fact]
    public void Adding_a_click_can_be_undone_and_redone()
    {
        MacroEditor editor = MacroEditor.Create();

        string path = editor.Add("mouse.click", null);

        Assert.Equal("0", path);
        EditorStep step = Assert.Single(editor.Steps);
        Assert.Equal("Mouse", step.Category);
        Assert.Equal("Click", step.Title);
        Assert.Contains("at the cursor", step.Line, StringComparison.Ordinal);
        MouseClickAction click = Assert.IsType<MouseClickAction>(Assert.Single(editor.Document!.Actions));
        Assert.Equal(MacroMouseButton.Left, click.Button);
        Assert.Equal(MacroCoordinateMode.Cursor, click.Mode);
        Assert.True(editor.HasUnsavedChanges);

        editor.Undo();
        Assert.Empty(editor.Steps);
        Assert.False(editor.HasUnsavedChanges);
        editor.Redo();
        Assert.Single(editor.Steps);
    }

    [Fact]
    public void A_bad_wait_is_explained_and_blocks_the_save()
    {
        MacroEditor editor = MacroEditor.Create();
        editor.Add("wait", null);
        editor.SetField("0", "durationMs", "nope");

        Assert.Null(editor.Document);
        EditorStep step = Assert.Single(editor.Steps);
        Assert.Contains(step.Problems, problem => problem.Contains("3600000", StringComparison.Ordinal));

        string folder = TempFolder();
        try
        {
            Assert.False(editor.TrySave(folder, out string path));
            Assert.Equal("", path);
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        editor.Undo();
        Assert.Equal(200, Assert.IsType<WaitAction>(Assert.Single(editor.Document!.Actions)).DurationMs);
    }

    [Fact]
    public void Duplicate_copies_a_repeat_and_gives_every_action_a_new_id()
    {
        MacroEditor editor = MacroEditor.Create();
        string repeat = editor.Add("repeat.count", null);
        editor.Add("wait", repeat);

        editor.Duplicate(repeat);

        RepeatCountAction first = Assert.IsType<RepeatCountAction>(editor.Document!.Actions[0]);
        RepeatCountAction second = Assert.IsType<RepeatCountAction>(editor.Document.Actions[1]);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, first.Count);
        WaitAction child = Assert.IsType<WaitAction>(Assert.Single(second.Actions));
        Assert.NotEqual(Assert.IsType<WaitAction>(Assert.Single(first.Actions)).Id, child.Id);
        Assert.Equal(200, child.DurationMs);
        Assert.Equal(2, editor.Steps.Count(step => step.IsRepeat));
    }

    [Fact]
    public void Move_swaps_siblings_and_stops_at_the_ends()
    {
        MacroEditor editor = MacroEditor.Create();
        editor.Add("wait", null);
        editor.Add("comment", null);

        Assert.False(editor.Move("0", -1));
        Assert.IsType<WaitAction>(editor.Document!.Actions[0]);
        Assert.True(editor.Move("1", -1));

        Assert.IsType<CommentAction>(editor.Document!.Actions[0]);
        Assert.IsType<WaitAction>(editor.Document.Actions[1]);
        Assert.False(editor.Move("1", 1));
    }

    [Fact]
    public void Remove_drops_a_nested_action_with_its_parent()
    {
        MacroEditor editor = MacroEditor.Create();
        string repeat = editor.Add("repeat.count", null);
        editor.Add("keyboard.key", repeat);
        Assert.Equal("Press A.", editor.Steps[1].Line);

        editor.Remove(repeat);

        Assert.Empty(editor.Document!.Actions);
        Assert.Throws<ArgumentException>(() => editor.Add("wait", repeat));
        Assert.Throws<ArgumentException>(() => editor.Add("sample.ping", null));
    }

    [Fact]
    public void Save_writes_a_file_that_loads_and_clears_the_unsaved_flag()
    {
        string folder = TempFolder();
        try
        {
            MacroEditor editor = MacroEditor.Create();
            editor.SetName("Door");
            editor.SetDescription("Opens it");
            editor.Add("mouse.move", null);
            editor.SetField("0", "x", "12");
            editor.SetField("0", "y", "-4");

            Assert.True(editor.TrySave(folder, out string path));
            Assert.False(editor.HasUnsavedChanges);
            MacroReadResult loadedResult = MacroStore.Load(path);
            Assert.NotNull(loadedResult.Document);
            MacroDocument loaded = loadedResult.Document!;
            Assert.Equal("Door", loaded.Name);
            Assert.Equal("Opens it", loaded.Description);
            MouseMoveAction move = Assert.IsType<MouseMoveAction>(Assert.Single(loaded.Actions));
            Assert.Equal(12, move.X);
            Assert.Equal(-4, move.Y);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void A_blank_name_is_reported_and_undo_restores_it()
    {
        MacroEditor editor = MacroEditor.Create();
        editor.SetName("  ");

        Assert.Null(editor.Document);
        Assert.Contains(editor.DocumentProblems, problem => problem.Contains("whitespace", StringComparison.Ordinal));
        editor.Undo();
        Assert.Equal("Untitled macro", editor.Document!.Name);
    }

    [Fact]
    public void Undo_keeps_the_newest_fifty_edits()
    {
        MacroEditor editor = MacroEditor.Create();
        for (int i = 0; i < MacroEditor.UndoLimit + 1; i++)
        {
            editor.SetName("n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        for (int i = 0; i < MacroEditor.UndoLimit; i++)
        {
            editor.Undo();
        }

        Assert.Equal("n0", editor.Name);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void The_same_field_value_does_not_create_an_undo_step()
    {
        MacroEditor editor = MacroEditor.Create();
        editor.Add("mouse.wheel", null);
        editor.SetField("0", "notches", "-2");
        Assert.Contains("toward you", editor.Preview, StringComparison.Ordinal);
        editor.SetField("0", "notches", "-2");

        editor.Undo();
        Assert.Equal("Scroll 1 notches.", Assert.Single(editor.Steps).Line);
        editor.Undo();
        Assert.Empty(editor.Steps);
        Assert.False(editor.CanUndo);
    }

    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "clickstitch-editor-" + Guid.NewGuid().ToString("N"));
}
