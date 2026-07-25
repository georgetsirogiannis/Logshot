using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Logshot.Services;

public class AutocorrectionManager
{
    public static AutocorrectionManager Instance { get; } = new();

    public bool IsEnabled { get; set; } = true;
    public string CustomDictionaryText { get; set; } = string.Empty;

    // These are your hardcoded defaults
    private readonly Dictionary<string, string> _defaultPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Κοντινπ", "Κοντινό" },
        { "Κοτνινο", "Κοντινό" },
        { "Κοντινο", "Κοντινό" },
        { "Κοντιν", "Κοντινό" },
        { "Γενικο", "Γενικό" },
        { "Γενικ", "Γενικό" },
        { "Γενικόπ", "Γενικό" },
        { "Μεσδαίο", "Μεσαίο" },
        { "Μεσαιο", "Μεσαίο" },
        { "Μεσαι", "Μεσαίο" },
        { "Μεσασίο", "Μεσαίο" },
        { "Roning", "Ronin" },
        { "Ρονιν", "Ronin" },
        { "Ρόνιν", "Ronin" },
        { "ΠΟΩ", "POV" },
        { "Κοτνινό", "Κοντινό" },
        { "Κλάμερα", "Κάμερα" }
    };

    private Dictionary<string, string> _activePairs = new();
    private readonly string _settingsFilePath;

    private AutocorrectionManager()
    {
        _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "logshot_autocorrect.json");
        LoadSettings();
        RebuildActivePairs();
    }

    public Dictionary<string, string> GetActivePairs() => _activePairs;

    public void SaveSettings(bool isEnabled, string customDictText)
    {
        IsEnabled = isEnabled;
        CustomDictionaryText = customDictText ?? string.Empty;

        var data = new { IsEnabled, CustomDictionaryText };
        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(data));

        RebuildActivePairs();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                using var doc = JsonDocument.Parse(json);
                IsEnabled = doc.RootElement.GetProperty("IsEnabled").GetBoolean();
                CustomDictionaryText = doc.RootElement.GetProperty("CustomDictionaryText").GetString() ?? "";
            }
            catch { /* If file is corrupted, silently fallback to defaults */ }
        }
    }

    private void RebuildActivePairs()
    {
        _activePairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in _defaultPairs)
        {
            _activePairs[pair.Key] = pair.Value;
        }

        var lines = CustomDictionaryText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // Users can use '=' or ',' to separate wrong from right
            var parts = line.Split(new[] { '=', ',' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var wrong = parts[0].Trim();
                var right = parts[1].Trim();
                if (!string.IsNullOrEmpty(wrong) && !string.IsNullOrEmpty(right))
                {
                    _activePairs[wrong] = right;
                }
            }
        }
    }
}