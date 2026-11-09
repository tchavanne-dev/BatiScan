using Autodesk.Revit.DB;
using BatiScan.Core.Geometrie;

namespace BatiScan.Infrastructure;

/// <summary>
/// E02 – Repères du projet : interne Revit, point de base, point de topographie,
/// coordonnées partagées. LECTURE SEULE : rien ici ne modifie les coordonnées partagées.
/// </summary>
public sealed class RepereProjet
{
    /// <summary>Interne (m) → partagé (m), si confirmé.</summary>
    public TransformationRigide? InterneVersPartage { get; }
    public string Statut { get; }
    public bool Confirme => InterneVersPartage is not null;
    public bool CoordonneesEtablies { get; }
    public Vecteur3 PointBaseInterne { get; }
    public Vecteur3 PointBasePartage { get; }
    public Vecteur3 PointTopoInterne { get; }
    public Vecteur3 PointTopoPartage { get; }
    public double AngleNordGeographiqueDeg { get; }
    public string NomSite { get; }

    private RepereProjet(Document doc)
    {
        BasePoint pbp = BasePoint.GetProjectBasePoint(doc);
        BasePoint topo = BasePoint.GetSurveyPoint(doc);
        PointBaseInterne = ConversionRevit.VersM(pbp.Position);
        PointBasePartage = ConversionRevit.VersM(pbp.SharedPosition);
        PointTopoInterne = ConversionRevit.VersM(topo.Position);
        PointTopoPartage = ConversionRevit.VersM(topo.SharedPosition);

        ProjectLocation loc = doc.ActiveProjectLocation;
        NomSite = loc.Name;
        ProjectPosition pos = loc.GetProjectPosition(XYZ.Zero);
        AngleNordGeographiqueDeg = ConversionRevit.VersDeg(pos.Angle);

        // Sens de GetTotalTransform non documenté de façon univoque : on retient celui des deux
        // sens (T ou T⁻¹) qui envoie Position → SharedPosition pour les DEUX points de référence.
        Transform t = loc.GetTotalTransform();
        TransformationRigide? retenu = null;
        foreach (Transform cand in new[] { t, t.Inverse })
        {
            var c = ConversionRevit.VersM(cand);
            if (c.Appliquer(PointTopoInterne).Distance(PointTopoPartage) < 1e-4
                && c.Appliquer(PointBaseInterne).Distance(PointBasePartage) < 1e-4)
            {
                retenu = c;
                break;
            }
        }
        InterneVersPartage = retenu;
        Statut = retenu is null
            ? "NON CONFIRMÉ : la transformation du site ne relie pas les points de base à leurs coordonnées partagées."
            : "Confirmé par le point de base du projet et le point de topographie.";
        CoordonneesEtablies = retenu is not null && !retenu.EstIdentite(1e-6, 1e-6);
    }

    public static RepereProjet Lire(Document doc) => new(doc);

    public Vecteur3 VersPartage(Vecteur3 interne) =>
        (InterneVersPartage ?? throw new InvalidOperationException(Statut)).Appliquer(interne);

    public Vecteur3 DepuisPartage(Vecteur3 partage) =>
        (InterneVersPartage ?? throw new InvalidOperationException(Statut)).Inverse().Appliquer(partage);

    /// <summary>Contrôle aller-retour interne → partagé → interne (cible J01 ≤ 0,1 mm).</summary>
    public double ErreurAllerRetourM(Vecteur3 p) => Confirme ? DepuisPartage(VersPartage(p)).Distance(p) : double.NaN;

    public string Decrire()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Site actif : {NomSite}");
        sb.AppendLine($"Point de base (interne → partagé) : {ConversionRevit.P(PointBaseInterne)} → {ConversionRevit.P(PointBasePartage)}");
        sb.AppendLine($"Point de topographie (interne → partagé) : {ConversionRevit.P(PointTopoInterne)} → {ConversionRevit.P(PointTopoPartage)}");
        sb.AppendLine($"Angle Nord géographique / Nord du projet : {ConversionRevit.Deg(AngleNordGeographiqueDeg, 4)}");
        sb.AppendLine($"Coordonnées partagées : {(CoordonneesEtablies ? "établies" : "non établies (identité ou non confirmées)")}");
        sb.AppendLine($"Statut de la relation interne ↔ partagé : {Statut}");
        if (Confirme) sb.AppendLine($"Erreur aller-retour au point de base : {ConversionRevit.Mm(ErreurAllerRetourM(PointBaseInterne))}");
        return sb.ToString();
    }
}
