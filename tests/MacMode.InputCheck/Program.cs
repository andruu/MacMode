using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// Manual desktop integration probe: generates no input. Native controls receive
// shortcuts from the running MacMode hook. Stores counts/booleans, never key text.
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacMode", "checks");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "input-check.json");
        int selectAll = 0, copy = 0, paste = 0, closedTabs = 0;
        bool closed = false;
        var window = new Window { Title = "MacMode Input Check", Width = 620, Height = 320 };
        var tabs = new TabControl { Margin = new Thickness(16) };
        var editor = new TextBox { Text = "MacMode check", AcceptsReturn = true, FontSize = 24, Padding = new Thickness(16) };
        tabs.Items.Add(new TabItem { Header = "Scratch", Content = editor });
        tabs.Items.Add(new TabItem { Header = "Second scratch", Content = new TextBox { Text = "Second scratch" } });
        window.Content = tabs;
        void Save() => File.WriteAllText(path, JsonSerializer.Serialize(new {
            selectAll, copy, paste, closedTabs, closed,
            correctPaste = editor.Text == "MacMode checkMacMode check",
            selectedSample = editor.SelectedText == "MacMode check"
        }));
        window.PreviewKeyDown += (_, e) => {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.A) selectAll++;
            if (e.Key == Key.C) copy++;
            if (e.Key == Key.V) paste++;
            if (e.Key == Key.W && tabs.SelectedItem != null) {
                tabs.Items.Remove(tabs.SelectedItem);
                closedTabs++;
                e.Handled = true;
            }
            Save();
        };
        editor.SelectionChanged += (_, _) => Save();
        editor.TextChanged += (_, _) => Save();
        window.Closed += (_, _) => { closed = true; Save(); };
        window.Loaded += (_, _) => { editor.Focus(); Save(); };
        new Application().Run(window);
    }
}
