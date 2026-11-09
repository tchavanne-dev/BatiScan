namespace BatiScan.Core.Geometrie;

/// <summary>Proposition d'orientation du projet de travail (E03).</summary>
public sealed record PropositionOrientation(
    double AngleAxeDeg,
    double RotationProposeeDeg,
    double DistancePointsM,
    string Origine,
    string Avertissement);

/// <summary>
/// E03 – Orientation assistée. Distingue le <b>Nord de travail</b> (orientation de
/// travail des zones, coupes et régularisations BatiScan) de la référence
/// géographique (Nord géographique du projet), qui n'est jamais modifiée ici.
/// </summary>
public static class Orientation
{
    /// <summary>
    /// Axe défini par deux points en plan. La rotation proposée aligne l'axe sur la direction
    /// orthogonale la plus proche (X ou Y), soit un angle dans ]−45°, 45°].
    /// </summary>
    public static PropositionOrientation DepuisDeuxPoints(Vecteur3 p1, Vecteur3 p2, double distanceMinM)
    {
        var d = new Vecteur3(p2.X - p1.X, p2.Y - p1.Y, 0);
        double lg = d.Norme;
        if (lg < 1e-6) throw new InvalidOperationException("Les deux points sont confondus en plan.");
        double angle = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
        string avert = lg < distanceMinM
            ? $"Points espacés de {lg:F2} m < {distanceMinM:F2} m : précision angulaire insuffisante pour la cible 0,1°."
            : "";
        double dz = Math.Abs(p2.Z - p1.Z);
        if (dz > 0.05 * lg)
            avert += (avert.Length > 0 ? " " : "") + $"Dénivelé de {dz:F2} m entre les points : axe mesuré en plan uniquement.";
        return new PropositionOrientation(angle, -RamenerQuart(angle), lg, "deux_points", avert);
    }

    /// <summary>Proposition depuis une direction dominante détectée (angle dans [0°, 90°[).</summary>
    public static PropositionOrientation DepuisDirection(double angleDeg, double poids) =>
        new(angleDeg, -RamenerQuart(angleDeg), double.NaN, $"detection (poids {poids:P0})", "");

    /// <summary>Ramène un angle dans ]−45°, 45°] (équivalence modulo 90°).</summary>
    public static double RamenerQuart(double angleDeg)
    {
        double a = ((angleDeg % 90) + 90) % 90;
        return a > 45 ? a - 90 : a;
    }

    /// <summary>Incertitude angulaire (degrés) due à une erreur de pointé <paramref name="erreurPointeM"/> sur chaque point.</summary>
    public static double IncertitudeDeg(double distanceM, double erreurPointeM) =>
        Math.Atan2(Math.Sqrt(2) * erreurPointeM, distanceM) * 180 / Math.PI;
}
