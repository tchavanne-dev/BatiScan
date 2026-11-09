using System.Text.Json;
using System.Text.Json.Serialization;
using BatiScan.Core.Scan;

namespace BatiScan.Core.Etat;

/// <summary>
/// Données BatiScan persistées DANS le projet Revit (stockage extensible, un seul
/// DataStorage). Ne contient que des données appartenant au plugin (E21 « Maintenance »).
/// Coordonnées en mètres, repère interne Revit.
/// </summary>
public sealed class EtatProjet
{
    public const int VersionFormat = 1;

    public int Version { get; set; } = VersionFormat;

    // ── E03 : Nord de travail (le Nord géographique n'est jamais modifié) ─────
    public double? NordTravailDeg { get; set; }
    public string? NordTravailOrigine { get; set; }

    // ── E05 : zones et coupes nommées ─────────────────────────────────────────
    public List<ZoneTravail> Zones { get; set; } = [];
    public List<CoupeEnregistree> Coupes { get; set; } = [];

    // ── E22 : révisions par source (clé = UniqueId du PointCloudType) ─────────
    public Dictionary<string, List<RevisionScan>> Revisions { get; set; } = [];

    // ── E14 / E21 : analyses et leur validité ─────────────────────────────────
    public List<AnalyseEnregistree> Analyses { get; set; } = [];

    public RevisionScan? DerniereRevision(string cleSource) =>
        Revisions.TryGetValue(cleSource, out var l) && l.Count > 0 ? l[^1] : null;

    public void AjouterRevision(string cleSource, RevisionScan r)
    {
        if (!Revisions.TryGetValue(cleSource, out var l)) Revisions[cleSource] = l = [];
        r.Numero = l.Count + 1;
        r.RevisionPrecedente = l.Count > 0 ? l[^1].Empreinte.Sha256 : null;
        l.Add(r);
    }

    /// <summary>
    /// E05 / E22 – Invalide zones, coupes et analyses dérivées d'une source dont le
    /// contenu ou la transformation a changé. Retourne le nombre d'éléments invalidés.
    /// Rien n'est supprimé : les éléments passent « à recalculer ».
    /// </summary>
    public int InvaliderSource(string cleSource, string raison)
    {
        int n = 0;
        foreach (var z in Zones.Where(z => z.CleSource == cleSource && z.Valide)) { z.Valide = false; z.RaisonInvalidite = raison; n++; }
        foreach (var c in Coupes.Where(c => c.CleSource == cleSource && c.Valide)) { c.Valide = false; c.RaisonInvalidite = raison; n++; }
        foreach (var a in Analyses.Where(a => a.CleSource == cleSource && a.Statut == "a_jour")) { a.Statut = "a_recalculer"; a.RaisonInvalidite = raison; n++; }
        return n;
    }

    // ── Sérialisation ─────────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string VersJson() => JsonSerializer.Serialize(this, Options);

    public static EtatProjet DepuisJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EtatProjet();
        var e = JsonSerializer.Deserialize<EtatProjet>(json, Options) ?? new EtatProjet();
        if (e.Version > VersionFormat)
            throw new InvalidOperationException($"Données BatiScan au format {e.Version}, plus récent que ce plugin ({VersionFormat}).");
        return e;
    }
}

/// <summary>Zone de travail nommée (E05) : boîte orientée autour de Z, centre de boîte.</summary>
public sealed class ZoneTravail
{
    public required string Nom { get; set; }
    public required double[] MinM { get; set; }
    public required double[] MaxM { get; set; }
    public double RotationDeg { get; set; }
    public string? NiveauAssocie { get; set; }
    /// <summary>Source + révision + transformation au moment de la création.</summary>
    public string CleSource { get; set; } = "";
    public string EmpreinteContexte { get; set; } = "";
    public bool Valide { get; set; } = true;
    public string? RaisonInvalidite { get; set; }
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore] public double EpaisseurM => MaxM[2] - MinM[2];
    [JsonIgnore] public double VolumeM3 => (MaxM[0] - MinM[0]) * (MaxM[1] - MinM[1]) * EpaisseurM;
}

/// <summary>Coupe multiple nommée et rappelable (E05, E24).</summary>
public sealed class CoupeEnregistree
{
    public required string Nom { get; set; }
    public required string Zone { get; set; }
    public required double[] AltitudesM { get; set; }
    public double EpaisseurM { get; set; }
    public string? NiveauReference { get; set; }
    public string CleSource { get; set; } = "";
    public bool Valide { get; set; } = true;
    public string? RaisonInvalidite { get; set; }
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Résultat d'analyse persistant (E14) : relisible après fermeture, invalidable.</summary>
public sealed class AnalyseEnregistree
{
    public required string Operation { get; set; }
    public required string CleSource { get; set; }
    public string Statut { get; set; } = "a_jour";
    public string? RaisonInvalidite { get; set; }
    public string? CheminRapport { get; set; }
    public string Resume { get; set; } = "";
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
}
