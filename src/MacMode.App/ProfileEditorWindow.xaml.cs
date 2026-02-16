using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MacMode.Core.Engine;
using MacMode.Core.Hook;
using MacMode.Core.Logging;
using MacMode.Core.Profiles;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace MacMode.App;

public partial class ProfileEditorWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _profilesDir;
    private readonly MappingEngine _engine;
    private readonly KeyboardHook _hook;

    private string? _currentProfileFileName;
    private ObservableCollection<MappingRow> _mappings = new();

    // Hotkey recording state
    private bool _isRecording;
    private MappingRow? _recordingRow;
    private string? _recordingField; // "Trigger" or "Action"
    private readonly HashSet<int> _recordingModifiers = new();
    private int _recordingMainKey;

    public ProfileEditorWindow(string profilesDir, MappingEngine engine, KeyboardHook hook)
    {
        InitializeComponent();
        _profilesDir = profilesDir;
        _engine = engine;
        _hook = hook;

        MappingsGrid.ItemsSource = _mappings;
        LoadProfileList();
    }

    private void LoadProfileList()
    {
        ProfileListBox.Items.Clear();

        if (!Directory.Exists(_profilesDir))
            return;

        foreach (string file in Directory.GetFiles(_profilesDir, "*.json").OrderBy(f => f))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            ProfileListBox.Items.Add(name);
        }

        if (ProfileListBox.Items.Count > 0)
            ProfileListBox.SelectedIndex = 0;
    }

    private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileListBox.SelectedItem is not string profileName)
        {
            EditorPanel.IsEnabled = false;
            return;
        }

        LoadProfile(profileName);
    }

    private void LoadProfile(string profileName)
    {
        string filePath = Path.Combine(_profilesDir, $"{profileName}.json");
        if (!File.Exists(filePath))
            return;

        try
        {
            string json = File.ReadAllText(filePath);
            var dto = JsonSerializer.Deserialize<ProfileDto>(json);
            if (dto == null) return;

            _currentProfileFileName = profileName;

            ProfileNameBox.Text = dto.Name;
            ProcessNamesBox.Text = string.Join(", ", dto.ProcessNames);

            bool isDefault = profileName.Equals("default", StringComparison.OrdinalIgnoreCase);
            ProcessNamesPanel.Visibility = isDefault ? Visibility.Collapsed : Visibility.Visible;
            ResetBtn.IsEnabled = DefaultProfiles.IsShippedProfile(profileName);
            DeleteProfileBtn.IsEnabled = !DefaultProfiles.IsShippedProfile(profileName);

            _mappings.Clear();
            foreach (var m in dto.Mappings)
                _mappings.Add(new MappingRow { Trigger = m.Trigger, Action = m.Action });

            EditorPanel.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load profile for editing: {ex.Message}");
            MessageBox.Show($"Failed to load profile: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_currentProfileFileName == null) return;

        try
        {
            bool isDefault = _currentProfileFileName.Equals("default", StringComparison.OrdinalIgnoreCase);

            var processNames = isDefault
                ? new List<string>()
                : ProcessNamesBox.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            var dto = new ProfileDto
            {
                Name = ProfileNameBox.Text.Trim(),
                ProcessNames = processNames,
                Mappings = _mappings.Select(m => new KeyMappingDto
                {
                    Trigger = m.Trigger,
                    Action = m.Action
                }).ToList()
            };

            string json = JsonSerializer.Serialize(dto, JsonOptions);
            string filePath = Path.Combine(_profilesDir, $"{_currentProfileFileName}.json");
            File.WriteAllText(filePath, json);

            Logger.Info($"Profile '{_currentProfileFileName}' saved from editor.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save profile: {ex.Message}");
            MessageBox.Show($"Failed to save profile: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnResetToDefault(object sender, RoutedEventArgs e)
    {
        if (_currentProfileFileName == null) return;

        var result = MessageBox.Show(
            $"Reset '{_currentProfileFileName}' to its default state? Your changes will be lost.",
            "Confirm Reset",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (DefaultProfiles.RestoreToDefault(_profilesDir, _currentProfileFileName))
        {
            LoadProfile(_currentProfileFileName);
            Logger.Info($"Profile '{_currentProfileFileName}' reset to default.");
        }
        else
        {
            MessageBox.Show("Could not find the default for this profile.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnNewProfile(object sender, RoutedEventArgs e)
    {
        string baseName = "custom";
        string name = baseName;
        int counter = 1;
        while (File.Exists(Path.Combine(_profilesDir, $"{name}.json")))
        {
            name = $"{baseName}{counter}";
            counter++;
        }

        var dto = new ProfileDto
        {
            Name = name,
            ProcessNames = new List<string> { "processname" },
            Mappings = new List<KeyMappingDto>()
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(Path.Combine(_profilesDir, $"{name}.json"), json);

        LoadProfileList();

        for (int i = 0; i < ProfileListBox.Items.Count; i++)
        {
            if (ProfileListBox.Items[i] is string n && n == name)
            {
                ProfileListBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        if (_currentProfileFileName == null) return;

        if (DefaultProfiles.IsShippedProfile(_currentProfileFileName))
        {
            MessageBox.Show("Cannot delete a built-in profile.", "Not Allowed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Delete profile '{_currentProfileFileName}'? This cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        string filePath = Path.Combine(_profilesDir, $"{_currentProfileFileName}.json");
        if (File.Exists(filePath))
            File.Delete(filePath);

        _currentProfileFileName = null;
        LoadProfileList();
    }

    private void OnAddMapping(object sender, RoutedEventArgs e)
    {
        _mappings.Add(new MappingRow { Trigger = "Alt+", Action = "Ctrl+" });
        MappingsGrid.ScrollIntoView(_mappings[^1]);
    }

    private void OnRemoveMapping(object sender, RoutedEventArgs e)
    {
        if (MappingsGrid.SelectedItem is MappingRow row)
            _mappings.Remove(row);
    }

    // --- Hotkey Recording ---

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        var row = btn.DataContext as MappingRow;
        var field = btn.Tag as string;
        if (row == null || field == null) return;

        StartRecording(row, field);
    }

    private void StartRecording(MappingRow row, string field)
    {
        _isRecording = true;
        _recordingRow = row;
        _recordingField = field;
        _recordingModifiers.Clear();
        _recordingMainKey = 0;

        _engine.IsRecording = true;
        _hook.KeyEvent += OnRecordingKeyEvent;

        RecordingDisplay.Text = "Press a key combination";
        RecordingOverlay.Visibility = Visibility.Visible;
    }

    private void StopRecording(bool apply)
    {
        _hook.KeyEvent -= OnRecordingKeyEvent;
        _engine.IsRecording = false;
        RecordingOverlay.Visibility = Visibility.Collapsed;

        if (apply && _recordingRow != null && _recordingField != null && _recordingMainKey != 0)
        {
            string combo = BuildComboString(_recordingModifiers, _recordingMainKey, _recordingField == "Trigger");

            if (_recordingField == "Trigger")
                _recordingRow.Trigger = combo;
            else
                _recordingRow.Action = combo;
        }

        _isRecording = false;
        _recordingRow = null;
        _recordingField = null;
        _recordingModifiers.Clear();
        _recordingMainKey = 0;
    }

    private void OnRecordingKeyEvent(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsInjected) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (!_isRecording) return;

            int vk = e.VirtualKeyCode;

            // Escape cancels recording
            if (vk == 0x1B && e.IsKeyDown)
            {
                StopRecording(false);
                return;
            }

            // Track modifier keys
            if (IsModifierVk(vk))
            {
                if (e.IsKeyDown)
                    _recordingModifiers.Add(NormalizeModifier(vk));
                else
                    _recordingModifiers.Remove(NormalizeModifier(vk));

                UpdateRecordingDisplay();
                return;
            }

            // Non-modifier keydown: this is the main key
            if (e.IsKeyDown)
            {
                _recordingMainKey = vk;
                UpdateRecordingDisplay();
                StopRecording(true);
            }
        });

        e.Handled = true;
    }

    private void UpdateRecordingDisplay()
    {
        var parts = new List<string>();

        if (_recordingModifiers.Contains(NativeMethods.VK_LMENU))
            parts.Add("Alt");
        if (_recordingModifiers.Contains(NativeMethods.VK_LCONTROL))
            parts.Add("Ctrl");
        if (_recordingModifiers.Contains(NativeMethods.VK_LSHIFT))
            parts.Add("Shift");
        if (_recordingModifiers.Contains(NativeMethods.VK_LWIN))
            parts.Add("Win");

        if (_recordingMainKey != 0)
        {
            string? name = KeyParser.VkToName(_recordingMainKey);
            if (name != null)
                parts.Add(name);
            else
                parts.Add($"VK 0x{_recordingMainKey:X2}");
        }

        RecordingDisplay.Text = parts.Count > 0 ? string.Join(" + ", parts) : "Press a key combination";
    }

    private static string BuildComboString(HashSet<int> modifiers, int mainKey, bool isTrigger)
    {
        var parts = new List<string>();

        // For triggers, Alt is always first (it's the "Cmd" key)
        if (isTrigger)
        {
            parts.Add("Alt");
            if (modifiers.Contains(NativeMethods.VK_LSHIFT))
                parts.Add("Shift");
            if (modifiers.Contains(NativeMethods.VK_LCONTROL))
                parts.Add("Ctrl");
        }
        else
        {
            // For actions, order: Ctrl, Shift, Alt, Win
            if (modifiers.Contains(NativeMethods.VK_LCONTROL))
                parts.Add("Ctrl");
            if (modifiers.Contains(NativeMethods.VK_LSHIFT))
                parts.Add("Shift");
            if (modifiers.Contains(NativeMethods.VK_LMENU))
                parts.Add("Alt");
            if (modifiers.Contains(NativeMethods.VK_LWIN))
                parts.Add("Win");
        }

        string? keyName = KeyParser.VkToName(mainKey);
        parts.Add(keyName ?? $"0x{mainKey:X2}");

        return string.Join("+", parts);
    }

    private static int NormalizeModifier(int vk)
    {
        return vk switch
        {
            NativeMethods.VK_LMENU or NativeMethods.VK_RMENU or NativeMethods.VK_MENU
                => NativeMethods.VK_LMENU,
            NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or NativeMethods.VK_SHIFT
                => NativeMethods.VK_LSHIFT,
            NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or NativeMethods.VK_CONTROL
                => NativeMethods.VK_LCONTROL,
            NativeMethods.VK_LWIN or NativeMethods.VK_RWIN
                => NativeMethods.VK_LWIN,
            _ => vk
        };
    }

    private static bool IsModifierVk(int vk)
    {
        return vk is NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or NativeMethods.VK_SHIFT
            or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or NativeMethods.VK_CONTROL
            or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU or NativeMethods.VK_MENU
            or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isRecording)
            StopRecording(false);

        base.OnClosing(e);
    }
}

/// <summary>
/// Observable row model for the mappings DataGrid.
/// </summary>
public class MappingRow : INotifyPropertyChanged
{
    private string _trigger = string.Empty;
    private string _action = string.Empty;

    public string Trigger
    {
        get => _trigger;
        set { _trigger = value; OnPropertyChanged(); }
    }

    public string Action
    {
        get => _action;
        set { _action = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
