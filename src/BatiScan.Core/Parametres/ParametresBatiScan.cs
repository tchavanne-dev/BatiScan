using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatiScan.Core.Parametres;

/// <summary>
/// Tolérances et paramètres utilisateur (mètres, degrés).
/// <para>
/// Les valeurs par défaut reprennent les cibles PROPOSÉES du cahier des charges
/// (section « Recette ») ; elles restent à valider avec le professeur (D07).
/// Les valeurs sans cible dans le cahier sont marquées « hypothèse BatiScan ».
/// Fichier : <c>%APPDATA%\BatiScan\parametres.json</c> (créé au premier enregistrement).
/// </para>
/// </summary>
public sealed class ParametresBatiScan
{
    // ── E02 / E22 : calage et actualisation ──────────────────────────────────
    /// <summary>Résidu maximal accepté sur un point de contrôle de calage (hypothèse BatiScan, D07).</summary>
    public double ToleranceResiduCalageM { get; set; } = 0.010;
    /// <summary>Variation maximale des contrôles avant/après (cible cahier : ≤ 1 mm).</summary>
    public double ToleranceConservationCalageM { get; set; } = 0.001;
    /// <summary>Erreur aller-retour des conversions (cible cahier J01 : ≤ 0,1 mm).</summary>
    public double ToleranceAllerRetourM { get; set; } = 0.0001;
    /// <summary>En deçà, un nouveau scan est considéré « même repère » (hypothèse BatiScan).</summary>
    public double ToleranceMemeRepereM { get; set; } = 0.005;
    /// <summary>Rayon de recherche automatique d'un point de contrôle sur un scan actualisé.</summary>
    public double RayonRechercheControleM { get; set; } = 0.10;

    // ── E03 : orientation ────────────────────────────────────────────────────
    /// <summary>Distance minimale entre les deux points d'axe (cible 0,1° ⇒ ≥ 2 m pour 2 mm de pointé).</summary>
    public double DistanceMinAxeM { get; set; } = 2.0;

    // ── E04 : niveaux ────────────────────────────────────────────────────────
    /// <summary>Écart d'altitude sous lequel un candidat est associé à un niveau existant (pas de doublon).</summary>
    public double ToleranceAssociationNiveauM { get; set; } = 0.020;

    // ── E07 / E08 / E24 : murs ───────────────────────────────────────────────
    public double TolerancePlombDeg { get; set; } = 0.2;
    public double ToleranceRegularisationAngleDeg { get; set; } = 1.0;
    public double ToleranceRegularisationDeplacementM { get; set; } = 0.020;
    /// <summary>Épaisseur proposée pour un mur à face unique (HYPOTHÈSE affichée, jamais « mesurée »).</summary>
    public double EpaisseurHypotheseM { get; set; } = 0.20;
    /// <summary>Écart d'épaisseur pour réutiliser un type de mur existant (E13).</summary>
    public double ToleranceTypeEpaisseurM { get; set; } = 0.001;
    /// <summary>Distance sous laquelle un mur proposé est considéré comme déjà créé (relance, E21).</summary>
    public double ToleranceDoublonM { get; set; } = 0.05;

    // ── E23 : Snap ───────────────────────────────────────────────────────────
    public double RayonSnapM { get; set; } = 0.05;
    public double TailleCelluleSnapM { get; set; } = 0.05;
    /// <summary>Espacement moyen demandé à Revit pour le cache d'aperçu.</summary>
    public double DensiteCacheSnapM { get; set; } = 0.01;
    /// <summary>Demi-côté de la boîte d'affinage à pleine densité autour du candidat.</summary>
    public double DemiCoteAffinageM { get; set; } = 0.03;
    /// <summary>Cible exploratoire de latence à chaud (p95, cahier : ≤ 250 ms).</summary>
    public double CibleLatenceP95Ms { get; set; } = 250;

    // ── Extraction / moteur ──────────────────────────────────────────────────
    public int PointsMaxParExtraction { get; set; } = 1_000_000; // limite API Revit GetPoints
    public double DensiteExtractionM { get; set; } = 0.01;
    public int DelaiMaxMoteurS { get; set; } = 600;
    public int Graine { get; set; } = 42;
    /// <summary>Racine du dépôt (contient venv\ et engine\). Vide = recherche automatique.</summary>
    public string RacineMoteur { get; set; } = "";

    // ── Persistance ──────────────────────────────────────────────────────────
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string CheminParDefaut =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatiScan", "parametres.json");

    public static ParametresBatiScan Charger(string? chemin = null)
    {
        chemin ??= CheminParDefaut;
        try
        {
            if (File.Exists(chemin))
                return JsonSerializer.Deserialize<ParametresBatiScan>(File.ReadAllText(chemin), Options) ?? new();
        }
        catch (JsonException)
        {
            // Fichier illisible : on repart des valeurs par défaut, sans l'écraser.
        }
        return new();
    }

    public void Enregistrer(string? chemin = null)
    {
        chemin ??= CheminParDefaut;
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
        File.WriteAllText(chemin, JsonSerializer.Serialize(this, Options));
    }
}
