using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatiScan.Core.Journal;

/// <summary>
/// E21 – Journal LOCAL (aucune télémétrie distante) : une ligne JSON par événement dans
/// <c>%LOCALAPPDATA%\BatiScan\journal\batiscan-AAAAMMJJ.jsonl</c>.
/// Contient version, action, paramètres, décisions, créations/modifications, durées, avertissements.
/// </summary>
public static class JournalLocal
{
    private static readonly object Verrou = new();
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Dossier { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatiScan", "journal");

    public static string VersionPlugin =>
        typeof(JournalLocal).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static string CheminDuJour => Path.Combine(Dossier, $"batiscan-{DateTime.Now:yyyyMMdd}.jsonl");

    /// <summary>Écrit un événement ; une erreur d'écriture du journal ne bloque jamais la commande.</summary>
    public static void Ecrire(string action, object? details = null, string niveau = "info")
    {
        try
        {
            var ligne = new Dictionary<string, object?>
            {
                ["date"] = DateTime.Now.ToString("O"),
                ["version_plugin"] = VersionPlugin,
                ["niveau"] = niveau,
                ["action"] = action,
                ["details"] = details,
            };
            string json = JsonSerializer.Serialize(ligne, Options);
            lock (Verrou)
            {
                Directory.CreateDirectory(Dossier);
                File.AppendAllText(CheminDuJour, json + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
