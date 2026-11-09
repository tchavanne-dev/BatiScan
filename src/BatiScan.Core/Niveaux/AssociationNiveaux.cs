using System.Globalization;

namespace BatiScan.Core.Niveaux;

/// <summary>Niveau candidat issu du moteur (altitude dans le repère interne, mètres).</summary>
public sealed record CandidatNiveau(string Id, double AltitudeInterneM, string TypeSuppose, string Justification, int NombrePoints, double SurfaceM2);

/// <summary>Niveau Revit déjà présent (élévation ramenée au repère interne, mètres).</summary>
public sealed record NiveauExistant(long Id, string Nom, double AltitudeInterneM);

public enum ActionNiveau { Creer, AssocierExistant, Ignorer }

/// <summary>Ligne du récapitulatif soumis à validation avant création (E04, E21).</summary>
public sealed class LigneNiveau
{
    public required string CandidatId { get; init; }
    public required double AltitudeInterneM { get; init; }
    /// <summary>Hauteur relative au zéro architectural ±0,00 choisi.</summary>
    public required double HauteurRelativeM { get; init; }
    /// <summary>Altitude dans les coordonnées partagées (absolue), si le décalage vertical est connu.</summary>
    public double? AltitudePartageeM { get; init; }
    public required string TypeSuppose { get; init; }
    public required string Justification { get; init; }
    public string NomPropose { get; set; } = "";
    public ActionNiveau Action { get; set; }
    public long? NiveauExistantId { get; init; }
    public string? NiveauExistantNom { get; init; }
    public double? EcartNiveauExistantM { get; init; }
    public bool CreerVueEnPlan { get; set; }
}

/// <summary>
/// E04 – Association des niveaux détectés au zéro architectural et aux niveaux existants.
/// <list type="bullet">
/// <item>Le zéro est <b>choisi par l'utilisateur</b> ; le point le plus bas n'est jamais présumé RDC.</item>
/// <item>Un candidat proche d'un niveau existant (tolérance) est associé, jamais dupliqué.</item>
/// <item>Les candidats « mobilier » ou « pente » sont proposés en « Ignorer » par défaut.</item>
/// </list>
/// </summary>
public static class AssociationNiveaux
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static List<LigneNiveau> Associer(
        IReadOnlyList<CandidatNiveau> candidats,
        IReadOnlyList<NiveauExistant> existants,
        double altitudeZeroInterneM,
        double toleranceM,
        double? decalagePartageZM,
        string convention)
    {
        var lignes = new List<LigneNiveau>();
        var existantsPris = new HashSet<long>();
        foreach (var c in candidats.OrderBy(c => c.AltitudeInterneM))
        {
            double rel = c.AltitudeInterneM - altitudeZeroInterneM;
            var proche = existants
                .Where(e => !existantsPris.Contains(e.Id))
                .Select(e => (e, ecart: Math.Abs(e.AltitudeInterneM - c.AltitudeInterneM)))
                .Where(t => t.ecart <= toleranceM)
                .OrderBy(t => t.ecart)
                .FirstOrDefault();

            bool secondaire = c.TypeSuppose is "mobilier_ou_plan_de_travail" or "pente";
            ActionNiveau action;
            if (proche.e is not null) { action = ActionNiveau.AssocierExistant; existantsPris.Add(proche.e.Id); }
            else action = secondaire ? ActionNiveau.Ignorer : ActionNiveau.Creer;

            lignes.Add(new LigneNiveau
            {
                CandidatId = c.Id,
                AltitudeInterneM = c.AltitudeInterneM,
                HauteurRelativeM = rel,
                AltitudePartageeM = decalagePartageZM is null ? null : c.AltitudeInterneM + decalagePartageZM.Value,
                TypeSuppose = c.TypeSuppose,
                Justification = c.Justification,
                NomPropose = proche.e?.Nom ?? NommerNiveau(rel, convention),
                Action = action,
                NiveauExistantId = proche.e?.Id,
                NiveauExistantNom = proche.e?.Nom,
                EcartNiveauExistantM = proche.e is null ? null : proche.ecart,
            });
        }
        return lignes;
    }

    /// <summary>Nom au format français « Niveau +2,85 (SF) » ; ±0,00 pour le zéro.</summary>
    public static string NommerNiveau(double hauteurRelativeM, string convention)
    {
        double r = Math.Round(hauteurRelativeM, 2);
        string cote = Math.Abs(r) < 0.005 ? "±0,00" : (r > 0 ? "+" : "−") + Math.Abs(r).ToString("0.00", Fr);
        string suffixe = convention switch
        {
            "sol_fini" => " (SF)",
            "structure" => " (BS)",
            "sous_face" => " (SsF)",
            _ => "",
        };
        return $"Niveau {cote}{suffixe}";
    }

    /// <summary>Rend un nom unique face aux noms existants (Revit refuse les doublons de nom).</summary>
    public static string NomUnique(string nom, ISet<string> nomsExistants)
    {
        if (!nomsExistants.Contains(nom)) return nom;
        for (int i = 2; ; i++)
        {
            string n = $"{nom} ({i})";
            if (!nomsExistants.Contains(n)) return n;
        }
    }
}
