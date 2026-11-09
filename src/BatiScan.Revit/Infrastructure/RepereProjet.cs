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

        // Interne → partagé construit avec la SEULE méthode documentée : GetProjectPosition(p) donne
        // les coordonnées partagées de p (« Report Shared Coordinates »). GetTotalTransform n'est pas
        // utilisé : la doc API 2027 ne précise pas son sens interne ↔ partagé (avis contradictoires
        // sur le forum Autodesk). Les images de l'origine et de trois points d'axe donnent rotation et
        // translation sans hypothèse sur le signe de ProjectPosition.Angle.
        TransformationRigide? retenu = null;
        string statut;
        try
        {
            const double pas = 100.0; // pieds : sonde éloignée pour la précision numérique
            Vecteur3 S(XYZ p)
            {
                ProjectPosition pp = loc.GetProjectPosition(p);
                return ConversionRevit.VersM(new XYZ(pp.EastWest, pp.NorthSouth, pp.Elevation));
            }
            var candidat = TransformationRigide.DepuisImagesDeSonde(
                S(XYZ.Zero), S(new XYZ(pas, 0, 0)), S(new XYZ(0, pas, 0)), S(new XYZ(0, 0, pas)), ConversionRevit.VersM(pas));
            // Contrôle indépendant : BasePoint.SharedPosition est calculé par Revit, pas par BatiScan.
            double ecartBase = candidat.Appliquer(PointBaseInterne).Distance(PointBasePartage);
            double ecartTopo = candidat.Appliquer(PointTopoInterne).Distance(PointTopoPartage);
            if (ecartBase < 1e-4 && ecartTopo < 1e-4)
            {
                retenu = candidat;
                statut = "Confirmé : GetProjectPosition vérifié par le point de base du projet et le point de topographie.";
            }
            else
                statut = $"NON CONFIRMÉ : GetProjectPosition et SharedPosition divergent (point de base {ecartBase * 1000:F2} mm, topographie {ecartTopo * 1000:F2} mm).";
        }
        catch (Exception e) when (e is InvalidOperationException or Autodesk.Revit.Exceptions.ApplicationException)
        {
            statut = "NON CONFIRMÉ : " + e.Message;
        }
        InterneVersPartage = retenu;
        Statut = statut;
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
