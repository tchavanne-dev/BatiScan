using BatiScan.Core.Geometrie;

namespace BatiScan.Core.Murs;

/// <summary>E24 – Représentation choisie par l'utilisateur pour un mur non plombé.</summary>
public enum ModeMurNonPlombe
{
    /// <summary>Mur Revit « Inclinaison unique » (WallCrossSection.SingleSlanted) fidèle au dévers mesuré.</summary>
    InclineFidele,
    /// <summary>Mur vertical strict + rapport de dévers (écarts documentés).</summary>
    VerticalAvecRapport,
}

/// <summary>Position de l'axe d'un mur vertical de référence sur un mur incliné.</summary>
public enum ReferenceVerticale
{
    /// <summary>Axe à la position mesurée en pied de mur.</summary>
    Base,
    /// <summary>Axe à mi-hauteur : écarts ±dévers/2 répartis en pied et en tête.</summary>
    MiHauteur,
}

/// <summary>Mur proposé (mesures moteur, repère interne, mètres).</summary>
public sealed record MurMesure(
    string Id,
    Vecteur3 Debut,
    Vecteur3 Fin,
    double ZBas,
    double ZHaut,
    double? EpaisseurM,
    double PenteMParM,
    Vecteur3 DirectionInclinaison,
    bool DeversIncertain,
    string Plombage);

/// <summary>Géométrie Revit à créer pour un mur, avec son écart documenté.</summary>
public sealed record MurACreer(
    string Id,
    Vecteur3 Debut,
    Vecteur3 Fin,
    double ZBas,
    double Hauteur,
    double AngleDepuisVerticaleDeg,
    ModeMurNonPlombe Mode,
    double EcartPiedM,
    double EcartTeteM,
    string Remarque);

/// <summary>
/// E24 – Conversion d'une mesure de mur en géométrie Revit selon le mode choisi.
/// Rien n'est « redressé » silencieusement : le mode vertical retourne les écarts
/// en pied et en tête, à reporter dans le rapport de dévers.
/// </summary>
public static class ConversionMurNonPlombe
{
    public static MurACreer Convertir(MurMesure m, ModeMurNonPlombe mode, ReferenceVerticale reference, double tolerancePlombDeg)
    {
        double h = m.ZHaut - m.ZBas;
        double angle = Math.Atan(m.PenteMParM) * 180 / Math.PI;
        bool nonPlombe = Math.Abs(angle) > tolerancePlombDeg;
        if (!nonPlombe || m.DeversIncertain)
        {
            string rq = m.DeversIncertain
                ? "Dévers incertain (face partielle ou irrégulière) : mur vertical, inclinaison non affirmée."
                : "Mur plombé dans la tolérance.";
            return new MurACreer(m.Id, m.Debut, m.Fin, m.ZBas, h, 0, ModeMurNonPlombe.VerticalAvecRapport, 0, m.PenteMParM * h, rq);
        }
        if (mode == ModeMurNonPlombe.InclineFidele)
            // Angle non signé : le sens est porté par DirectionInclinaison (résolu côté Revit).
            return new MurACreer(m.Id, m.Debut, m.Fin, m.ZBas, h, Math.Abs(angle), mode, 0, 0,
                $"Mur incliné de {angle:F2}° (déplacement en tête {m.PenteMParM * h * 1000:F0} mm).");

        // Vertical strict : décalage de l'axe (en plan) selon la référence choisie.
        // Direction d'inclinaison = sens de déplacement de la tête ; pente signée sur cette direction.
        double decalage = reference == ReferenceVerticale.MiHauteur ? Math.Abs(m.PenteMParM) * h / 2 : 0;
        var dir = new Vecteur3(m.DirectionInclinaison.X, m.DirectionInclinaison.Y, 0).Unitaire();
        var d = dir * decalage;
        double deplacementTete = Math.Abs(m.PenteMParM) * h;
        double ecartPied = -decalage;                 // pied mesuré − axe vertical (selon dir)
        double ecartTete = deplacementTete - decalage; // tête mesurée − axe vertical
        return new MurACreer(m.Id, m.Debut + d, m.Fin + d, m.ZBas, h, 0, mode, ecartPied, ecartTete,
            $"Mur vertical de référence ({reference}) ; dévers réel {angle:F2}°, écarts pied {ecartPied * 1000:F0} mm / tête {ecartTete * 1000:F0} mm.");
    }
}

/// <summary>Segment de mur en plan pour la régularisation (E08).</summary>
public sealed record SegmentMur(string Id, Vecteur3 Debut, Vecteur3 Fin);

/// <summary>Résultat de régularisation : état mesuré ET état régularisé, jamais l'un sans l'autre.</summary>
public sealed record ResultatRegularisation(
    SegmentMur Mesure,
    SegmentMur Regularise,
    double RotationDeg,
    double DeplacementMaxM,
    bool DansTolerance,
    string Decision);

/// <summary>
/// E08 – Régularisation sous tolérance (orthogonalisation par rapport au Nord de travail).
/// Un segment hors tolérance n'est PAS modifié : il est retourné « à décider ».
/// </summary>
public static class RegularisationMurs
{
    public static List<ResultatRegularisation> Orthogonaliser(
        IEnumerable<SegmentMur> segments, double nordTravailDeg, double toleranceAngleDeg, double toleranceDeplacementM)
    {
        var res = new List<ResultatRegularisation>();
        foreach (var s in segments)
        {
            var d = s.Fin - s.Debut;
            double angle = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
            double ecart = Orientation.RamenerQuart(angle - nordTravailDeg);
            var milieu = (s.Debut + s.Fin) / 2;
            var rot = TransformationRigide.RotationZ(-ecart * Math.PI / 180, milieu);
            var reg = new SegmentMur(s.Id, rot.Appliquer(s.Debut), rot.Appliquer(s.Fin));
            double depl = Math.Max(reg.Debut.Distance(s.Debut), reg.Fin.Distance(s.Fin));
            bool ok = Math.Abs(ecart) <= toleranceAngleDeg && depl <= toleranceDeplacementM;
            string decision = Math.Abs(ecart) < 1e-9 ? "déjà orthogonal"
                : ok ? "régularisable sous tolérance"
                : $"HORS TOLÉRANCE ({ecart:F2}°, {depl * 1000:F0} mm) : conservé tel que mesuré, décision explicite requise";
            res.Add(new ResultatRegularisation(s, ok ? reg : s, ok ? -ecart : 0, ok ? depl : 0, ok, decision));
        }
        return res;
    }
}

/// <summary>E07 / E21 – Détection des murs déjà créés lors d'une relance (pas de doublon).</summary>
public static class DoublonsMurs
{
    /// <summary>Vrai si les deux axes coïncident à <paramref name="toleranceM"/> près (dans un sens ou l'autre).</summary>
    public static bool Coincident(Vecteur3 a1, Vecteur3 b1, Vecteur3 a2, Vecteur3 b2, double toleranceM)
    {
        static double Dp(Vecteur3 p, Vecteur3 q) => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
        return (Dp(a1, a2) <= toleranceM && Dp(b1, b2) <= toleranceM) || (Dp(a1, b2) <= toleranceM && Dp(b1, a2) <= toleranceM);
    }
}
