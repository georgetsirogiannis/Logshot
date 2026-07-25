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
        { "Κλάμερα", "Κάμερα" },
        { "Σκηνη", "Σκηνή" },
        { "Σκν", "Σκηνή" },
        { "Πλανο", "Πλάνο" },
        { "Πλν", "Πλάνο" },
        { "Ληψη", "Λήψη" },
        { "Ληψ", "Λήψη" },
        { "Καμερα", "Κάμερα" },
        { "Καμ", "Κάμερα" },
        { "Φακος", "Φακός" },
        { "Γερανος", "Γερανός" },
        { "Στεντικαμ", "Steadicam" },
        { "Στεντι", "Steadicam" },
        { "Τριποδο", "Τρίποδο" },
        { "Αμερικεν", "Αμερικέν" },
        { "Αντιπλανο", "Αντι-πλάνο" },
        { "Τραβελινγκ", "Τραβελίνγκ" },
        { "Τραβ", "Τραβελίνγκ" },
        { "Πανοραμικ", "Πανοραμίκ" },
        { "Παν", "Πανοραμίκ" },
        { "Χειρος", "Χειρός" },
        { "Σταθερο", "Σταθερό" },
        { "Μονοπλανο", "Μονοπλάνο" },
        { "Ποβ", "POV" },
        { "Κλακετα", "Κλακέτα" },
        { "Κλακ", "Κλακέτα" },
        { "Μοτερ", "Μοτέρ" },
        { "Ακυρο", "Άκυρο" },
        { "Ακυρ", "Άκυρο" },
        { "Αρχη", "Αρχή" },
        { "Τελος", "Τέλος" },
        { "Δραση", "Δράση" },
        { "Διαλογος", "Διάλογος" },
        { "Εσωτερικο", "Εσωτερικό" },
        { "Εσωτ", "Εσωτερικό" },
        { "Εξωτερικο", "Εξωτερικό" },
        { "Εξωτ", "Εξωτερικό" },
        { "Μερα", "Μέρα" },
        { "Νυχτα", "Νύχτα" },
        { "Ηχος", "Ήχος" },
        { "Ηχ", "Ήχος" },
        { "Μπουμα", "Μπούμα" },
        { "Ψειρα", "Ψείρα" },
        { "Ψιρα", "Ψείρα" },
        { "Συγχρονισμος", "Συγχρονισμός" },
        { "Ασυγχρονο", "Ασύγχρονο" },
        { "Ασυγχ", "Ασύγχρονο" },
        { "Εστιαση", "Εστίαση" },
        { "Ρακορ", "Ρακόρ" },
        { "Αξονας", "Άξονας" },
        { "Αξονα", "Άξονα" },
        { "Επεισοδιο", "Επεισόδιο" },
        { "Επεισ", "Επεισόδιο" },
        { "Επσ", "Επεισόδιο" },
        { "Epeisodio", "Επεισόδιο" },
        { "Σκην", "Σκηνή" },
        { "Σκηνηξ", "Σκηνή" },
        { "Skhnh", "Σκηνή" },
        { "Πλαν", "Πλάνο" },
        { "Πλναο", "Πλάνο" },
        { "Πλανοπ", "Πλάνο" },
        { "Plano", "Πλάνο" },
        { "Λψ", "Λήψη" },
        { "Ληψηξ", "Λήψη" },
        { "Κλακετ", "Κλακέτα" },
        { "Κλακετασ", "Κλακέτας" },
        { "Klaketa", "Κλακέτα" },
        { "Διαλογ", "Διάλογος" },
        { "Καμερασ", "Κάμερας" },
        { "Kamera", "Κάμερα" },
        { "Fakos", "Φακός" },
        { "Ευρυγωνιος", "Ευρυγώνιος" },
        { "Ευρυγων", "Ευρυγώνιος" },
        { "Ευρυγ", "Ευρυγώνιος" },
        { "Τηλεφακος", "Τηλεφακός" },
        { "Τηλεφ", "Τηλεφακός" },
        { "Μακρο", "Μακρό" },
        { "Ζουμ", "Zoom" },
        { "Τριπ", "Τρίποδο" },
        { "Stenticam", "Steadicam" },
        { "Γεραν", "Γερανός" },
        { "Ντροουν", "Drone" },
        { "Droun", "Drone" },
        { "Dron", "Drone" },
        { "Τζιμπ", "Jib" },
        { "Γεν", "Γενικό" },
        { "Γενκιο", "Γενικό" },
        { "Γενικοπ", "Γενικό" },
        { "Geniko", "Γενικό" },
        { "Μεσ", "Μεσαίο" },
        { "Μεσιαο", "Μεσαίο" },
        { "Μεσαιοπ", "Μεσαίο" },
        { "Mesaio", "Μεσαίο" },
        { "Κοντ", "Κοντινό" },
        { "Κοτινο", "Κοντινό" },
        { "Κοντινοπ", "Κοντινό" },
        { "Kontino", "Κοντινό" },
        { "Αμερικ", "Αμερικέν" },
        { "Αμερ", "Αμερικέν" },
        { "Ameriken", "Αμερικέν" },
        { "Μακρινο", "Μακρινό" },
        { "Μακριν", "Μακρινό" },
        { "Makrino", "Μακρινό" },
        { "Λεπτομερεια", "Λεπτομέρεια" },
        { "Λεπτομερ", "Λεπτομέρεια" },
        { "Λεπτομ", "Λεπτομέρεια" },
        { "Πλονζε", "Πλονζέ" },
        { "Πλονζ", "Πλονζέ" },
        { "Κοντρ πλονζε", "Κοντρ-πλονζέ" },
        { "Κοντρπλονζε", "Κοντρ-πλονζέ" },
        { "Pov", "POV" },
        { "Προφιλ", "Προφίλ" },
        { "Προφ", "Προφίλ" },
        { "Σταθ", "Σταθερό" },
        { "Hxos", "Ήχος" },
        { "Rakor", "Ρακόρ" },
        { "Αξον", "Άξονας" },
        { "Axonas", "Άξονας" },
        { "Εστιασ", "Εστίαση" },
        { "Flou", "Φλου" },
        { "Ακυρό", "Άκυρο" },
        { "Akyro", "Άκυρο" },
        { "Λαθος", "Λάθος" },
        { "Λαθ", "Λάθος" }
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