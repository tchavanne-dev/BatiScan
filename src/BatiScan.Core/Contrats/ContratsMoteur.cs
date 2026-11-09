using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BatiScan.Core.Contrats;

// ── Contrat d'échange 1.0.0 (contracts/requete.schema.json, reponse.schema.json) ──

public static class Contrat
{
    public const string Version = "1.0.0";

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
}

public sealed class RequeteMoteur
{
    [JsonPropertyName("contract_version")] public string VersionContrat { get; set; } = Contrat.Version;
    [JsonPropertyName("id_requete")] public string IdRequete { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("operation")] public required string Operation { get; set; }
    [JsonPropertyName("unites")] public Unites Unites { get; set; } = new();
    [JsonPropertyName("repere")] public required Repere Repere { get; set; }
    [JsonPropertyName("source")] public required SourceNuage Source { get; set; }
    [JsonPropertyName("points")] public required SpecPoints Points { get; set; }
    [JsonPropertyName("zone")] public ZoneJson? Zone { get; set; }
    [JsonPropertyName("parametres")] public JsonObject Parametres { get; set; } = [];
    [JsonPropertyName("graine")] public int Graine { get; set; } = 42;
}

public sealed class Unites
{
    [JsonPropertyName("longueur")] public string Longueur { get; set; } = "m";
    [JsonPropertyName("angle")] public string Angle { get; set; } = "deg";
}

public sealed class Repere
{
    /// <summary>« revit_interne_m » : coordonnées internes Revit converties en mètres.</summary>
    [JsonPropertyName("nom")] public string Nom { get; set; } = "revit_interne_m";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("transformation_vers_partage")] public double[]? TransformationVersPartage { get; set; }
}

public sealed class SourceNuage
{
    [JsonPropertyName("id")] public required string Id { get; set; }
    [JsonPropertyName("chemin")] public required string Chemin { get; set; }
    [JsonPropertyName("empreinte")] public required string Empreinte { get; set; }
    [JsonPropertyName("revision")] public int? Revision { get; set; }
    [JsonPropertyName("transformation_instance")] public double[]? TransformationInstance { get; set; }
}

public sealed class SpecPoints
{
    [JsonPropertyName("chemin")] public required string Chemin { get; set; }
    [JsonPropertyName("format")] public string Format { get; set; } = "xyz_f32_le";
    [JsonPropertyName("nombre")] public int? Nombre { get; set; }
    [JsonPropertyName("origine_m")] public double[] OrigineM { get; set; } = [0, 0, 0];
    [JsonPropertyName("densite_moyenne_m")] public double? DensiteMoyenneM { get; set; }
}

public sealed class ZoneJson
{
    [JsonPropertyName("nom")] public required string Nom { get; set; }
    [JsonPropertyName("min")] public required double[] Min { get; set; }
    [JsonPropertyName("max")] public required double[] Max { get; set; }
    [JsonPropertyName("rotation_deg")] public double? RotationDeg { get; set; }
}

public sealed class ReponseMoteur
{
    [JsonPropertyName("contract_version")] public string VersionContrat { get; set; } = "";
    [JsonPropertyName("id_requete")] public string IdRequete { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "";
    [JsonPropertyName("statut")] public string Statut { get; set; } = "erreur";
    [JsonPropertyName("moteur")] public InfoMoteur? Moteur { get; set; }
    [JsonPropertyName("unites")] public JsonObject? Unites { get; set; }
    [JsonPropertyName("repere")] public JsonObject? Repere { get; set; }
    [JsonPropertyName("source")] public JsonObject? Source { get; set; }
    [JsonPropertyName("parametres")] public JsonObject? Parametres { get; set; }
    [JsonPropertyName("graine")] public int Graine { get; set; }
    [JsonPropertyName("nombre_points")] public NombrePoints NombrePoints { get; set; } = new();
    [JsonPropertyName("couverture")] public Couverture Couverture { get; set; } = new();
    [JsonPropertyName("propositions")] public List<JsonElement> Propositions { get; set; } = [];
    [JsonPropertyName("avertissements")] public List<string> Avertissements { get; set; } = [];
    [JsonPropertyName("erreurs")] public List<ErreurMoteur> Erreurs { get; set; } = [];
    [JsonPropertyName("duree_s")] public double DureeS { get; set; }

    public bool EnErreur => Statut == "erreur";

    /// <summary>Désérialise les propositions du type demandé (« niveau », « mur »…).</summary>
    public List<T> Lire<T>(string type) =>
        Propositions
            .Where(p => p.TryGetProperty("type", out var t) && t.GetString() == type)
            .Select(p => p.Deserialize<T>(Contrat.Json)!)
            .ToList();

    public string ResumeErreurs() => string.Join("\n", Erreurs.Select(e => $"[{e.Code}] {e.Message}"));
}

public sealed class InfoMoteur
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("open3d")] public string Open3d { get; set; } = "";
    [JsonPropertyName("python")] public string Python { get; set; } = "";
}

public sealed class NombrePoints
{
    [JsonPropertyName("recus")] public int Recus { get; set; }
    [JsonPropertyName("dans_zone")] public int DansZone { get; set; }
    [JsonPropertyName("traites")] public int Traites { get; set; }
}

public sealed class Couverture
{
    [JsonPropertyName("methode")] public string Methode { get; set; } = "";
    [JsonPropertyName("taille_cellule_m")] public double? TailleCelluleM { get; set; }
    [JsonPropertyName("ratio")] public double Ratio { get; set; }
}

public sealed class ErreurMoteur
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

// ── Propositions typées ─────────────────────────────────────────────────────

public sealed class PropNiveau
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("altitude_m")] public double AltitudeM { get; set; }
    [JsonPropertyName("inclinaison_deg")] public double InclinaisonDeg { get; set; }
    [JsonPropertyName("rms_m")] public double RmsM { get; set; }
    [JsonPropertyName("nombre_points")] public int NombrePoints { get; set; }
    [JsonPropertyName("surface_observee_m2")] public double SurfaceObserveeM2 { get; set; }
    [JsonPropertyName("type_suppose")] public string TypeSuppose { get; set; } = "";
    [JsonPropertyName("justification")] public string Justification { get; set; } = "";
}

public sealed class PropMur
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("debut")] public double[] Debut { get; set; } = [];
    [JsonPropertyName("fin")] public double[] Fin { get; set; } = [];
    [JsonPropertyName("z_bas")] public double ZBas { get; set; }
    [JsonPropertyName("z_haut")] public double ZHaut { get; set; }
    [JsonPropertyName("longueur_m")] public double LongueurM { get; set; }
    [JsonPropertyName("epaisseur_m")] public double? EpaisseurM { get; set; }
    [JsonPropertyName("epaisseur_mesuree")] public bool EpaisseurMesuree { get; set; }
    [JsonPropertyName("normale")] public double[] Normale { get; set; } = [];
    [JsonPropertyName("cote_matiere")] public string CoteMatiere { get; set; } = "";
    [JsonPropertyName("nombre_faces")] public int NombreFaces { get; set; }
    [JsonPropertyName("nombre_points")] public int NombrePoints { get; set; }
    [JsonPropertyName("rms_m")] public double RmsM { get; set; }
    [JsonPropertyName("devers")] public DeversMur Devers { get; set; } = new();
    [JsonPropertyName("plombage")] public string Plombage { get; set; } = "";
    [JsonPropertyName("couverture")] public Couverture Couverture { get; set; } = new();
    [JsonPropertyName("justification")] public string Justification { get; set; } = "";
}

public sealed class DeversMur
{
    [JsonPropertyName("angle_deg")] public double? AngleDeg { get; set; }
    [JsonPropertyName("pente_m_par_m")] public double? PenteMParM { get; set; }
    [JsonPropertyName("deplacement_sommet_m")] public double? DeplacementSommetM { get; set; }
    [JsonPropertyName("direction_inclinaison")] public double[]? DirectionInclinaison { get; set; }
    [JsonPropertyName("incertain")] public bool Incertain { get; set; } = true;
    [JsonPropertyName("raison")] public string? Raison { get; set; }
}

public sealed class PropTrace
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("z")] public double Z { get; set; }
    [JsonPropertyName("epaisseur_m")] public double EpaisseurM { get; set; }
    [JsonPropertyName("nombre_points")] public int NombrePoints { get; set; }
    [JsonPropertyName("segments")] public List<SegmentTrace> Segments { get; set; } = [];
}

public sealed class SegmentTrace
{
    [JsonPropertyName("debut")] public double[] Debut { get; set; } = [];
    [JsonPropertyName("fin")] public double[] Fin { get; set; } = [];
    [JsonPropertyName("rms_m")] public double RmsM { get; set; }
    [JsonPropertyName("nombre_points")] public int NombrePoints { get; set; }
}

public sealed class PropPoteau
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("centre")] public double[] Centre { get; set; } = [];
    [JsonPropertyName("z_bas")] public double ZBas { get; set; }
    [JsonPropertyName("z_haut")] public double ZHaut { get; set; }
    [JsonPropertyName("encombrement_observe")] public Encombrement Encombrement { get; set; } = new();
    [JsonPropertyName("rotation_deg")] public double RotationDeg { get; set; }
    [JsonPropertyName("section_observee")] public string SectionObservee { get; set; } = "";
    [JsonPropertyName("justification")] public string Justification { get; set; } = "";
}

public sealed class Encombrement
{
    [JsonPropertyName("a_m")] public double AM { get; set; }
    [JsonPropertyName("b_m")] public double BM { get; set; }
}

public sealed class PropPoutre
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("debut")] public double[] Debut { get; set; } = [];
    [JsonPropertyName("fin")] public double[] Fin { get; set; } = [];
    [JsonPropertyName("z_sous_face")] public double ZSousFace { get; set; }
    [JsonPropertyName("retombee_observee_m")] public double RetombeeObserveeM { get; set; }
    [JsonPropertyName("largeur_observee_m")] public double LargeurObserveeM { get; set; }
    [JsonPropertyName("longueur_observee_m")] public double LongueurObserveeM { get; set; }
    [JsonPropertyName("hypothese")] public string Hypothese { get; set; } = "";
    [JsonPropertyName("justification")] public string Justification { get; set; } = "";
}

public sealed class PropEcart
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("reference")] public string Reference { get; set; } = "";
    [JsonPropertyName("nombre_points")] public int NombrePoints { get; set; }
    [JsonPropertyName("statut")] public string Statut { get; set; } = "";
    [JsonPropertyName("moyenne_signee_m")] public double? MoyenneSigneeM { get; set; }
    [JsonPropertyName("moyenne_absolue_m")] public double? MoyenneAbsolueM { get; set; }
    [JsonPropertyName("p95_absolu_m")] public double? P95AbsoluM { get; set; }
    [JsonPropertyName("max_absolu_m")] public double? MaxAbsoluM { get; set; }
    [JsonPropertyName("part_hors_tolerance")] public double? PartHorsTolerance { get; set; }
    [JsonPropertyName("couverture")] public Couverture Couverture { get; set; } = new();
}

public sealed class PropDirection
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("angle_deg")] public double AngleDeg { get; set; }
    [JsonPropertyName("poids_relatif")] public double PoidsRelatif { get; set; }
}
