using System.Globalization;
using System.Text;

namespace BatiScan.Core.Rapports;

/// <summary>
/// E16 – Export tabulaire (CSV « ; », décimales françaises, UTF-8 avec BOM pour Excel).
/// Chaque fichier commence par un en-tête de contexte : version, date, unités, repère,
/// source et paramètres, afin que chaque valeur reste reliée à son contexte de calcul.
/// </summary>
public sealed class RapportCsv
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly List<string> _contexte = [];
    private readonly List<string[]> _lignes = [];
    private string[] _colonnes = [];

    public RapportCsv(string titre)
    {
        Contexte("Rapport", titre);
        Contexte("Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Fr));
        Contexte("Version BatiScan", Journal.JournalLocal.VersionPlugin);
    }

    public RapportCsv Contexte(string cle, string valeur)
    {
        _contexte.Add($"# {Echapper(cle)};{Echapper(valeur)}");
        return this;
    }

    public RapportCsv Colonnes(params string[] colonnes) { _colonnes = colonnes; return this; }

    public RapportCsv Ligne(params object?[] valeurs)
    {
        _lignes.Add(valeurs.Select(Formater).ToArray());
        return this;
    }

    public static string Formater(object? v) => v switch
    {
        null => "",
        double d when double.IsNaN(d) => "",
        double d => d.ToString("0.######", Fr),
        float f => f.ToString("0.######", Fr),
        bool b => b ? "oui" : "non",
        IFormattable f => f.ToString(null, Fr),
        _ => v.ToString() ?? "",
    };

    private static string Echapper(string s) =>
        s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    public string VersTexte()
    {
        var sb = new StringBuilder();
        foreach (var c in _contexte) sb.AppendLine(c);
        sb.AppendLine(string.Join(";", _colonnes.Select(Echapper)));
        foreach (var l in _lignes) sb.AppendLine(string.Join(";", l.Select(Echapper)));
        return sb.ToString();
    }

    public void Enregistrer(string chemin)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(chemin))!);
        File.WriteAllText(chemin, VersTexte(), new UTF8Encoding(true));
    }

    public static string DossierRapports =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BatiScan", "Rapports");

    public static string CheminHorodate(string prefixe) =>
        Path.Combine(DossierRapports, $"{prefixe}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
}
