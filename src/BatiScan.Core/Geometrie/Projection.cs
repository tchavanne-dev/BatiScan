namespace BatiScan.Core.Geometrie;

/// <summary>Plan explicite (E06, E24) : origine + normale unitaire, en mètres.</summary>
public sealed record Plan3(Vecteur3 Origine, Vecteur3 Normale)
{
    public Vecteur3 NormaleUnitaire => Normale.Unitaire();
    public double DistanceSignee(Vecteur3 p) => (p - Origine).Scalaire(NormaleUnitaire);

    /// <summary>Plan horizontal d'altitude <paramref name="z"/>.</summary>
    public static Plan3 Horizontal(double z) => new(new Vecteur3(0, 0, z), Vecteur3.AxeZ);

    /// <summary>Plan vertical contenant le segment en plan a→b.</summary>
    public static Plan3 VerticalPar(Vecteur3 a, Vecteur3 b)
    {
        var d = new Vecteur3(b.X - a.X, b.Y - a.Y, 0).Unitaire();
        return new Plan3(new Vecteur3(a.X, a.Y, 0), new Vecteur3(-d.Y, d.X, 0));
    }
}

/// <summary>
/// E24 – Projection contrôlée de points / traces sur un plan cible, le long d'une
/// direction explicite. Les données source ne sont jamais modifiées : la
/// projection produit une NOUVELLE géométrie, identifiée comme dérivée.
/// </summary>
public static class Projection
{
    /// <summary>
    /// Projette <paramref name="p"/> sur <paramref name="plan"/> parallèlement à <paramref name="direction"/>.
    /// Lève une exception si la direction est (quasi) parallèle au plan : projection instable (E06).
    /// </summary>
    public static Vecteur3 SurPlan(Vecteur3 p, Plan3 plan, Vecteur3 direction, double angleMinDeg = 5.0)
    {
        var n = plan.NormaleUnitaire;
        var d = direction.Unitaire();
        double cos = d.Scalaire(n);
        if (Math.Abs(cos) < Math.Sin(angleMinDeg * Math.PI / 180))
            throw new InvalidOperationException("Direction de projection quasi parallèle au plan cible : résultat instable refusé.");
        double k = -plan.DistanceSignee(p) / cos;
        return p + d * k;
    }

    /// <summary>Projection orthogonale (direction = normale du plan).</summary>
    public static Vecteur3 Orthogonale(Vecteur3 p, Plan3 plan) => SurPlan(p, plan, plan.NormaleUnitaire);

    /// <summary>
    /// Décalage horizontal entre deux segments quasi parallèles (traces de coupe à deux altitudes),
    /// mesuré selon la normale en plan du premier segment, au milieu de leur recouvrement.
    /// Positif du côté de la normale gauche de a1→b1.
    /// </summary>
    public static ResultatDecalage DecalageEntreTraces(Vecteur3 a1, Vecteur3 b1, Vecteur3 a2, Vecteur3 b2, double angleMaxDeg = 5.0)
    {
        var u1 = new Vecteur3(b1.X - a1.X, b1.Y - a1.Y, 0).Unitaire();
        var u2 = new Vecteur3(b2.X - a2.X, b2.Y - a2.Y, 0).Unitaire();
        double angle = Math.Acos(Math.Clamp(Math.Abs(u1.Scalaire(u2)), 0, 1)) * 180 / Math.PI;
        if (angle > angleMaxDeg)
            throw new InvalidOperationException($"Traces non parallèles ({angle:F1}° > {angleMaxDeg}°) : décalage non défini.");
        var n = new Vecteur3(-u1.Y, u1.X, 0);
        double s0 = 0, s1 = new Vecteur3(b1.X - a1.X, b1.Y - a1.Y, 0).Norme;
        double t2a = (a2 - a1).Scalaire(u1), t2b = (b2 - a1).Scalaire(u1);
        double r0 = Math.Max(s0, Math.Min(t2a, t2b)), r1 = Math.Min(s1, Math.Max(t2a, t2b));
        if (r1 <= r0) throw new InvalidOperationException("Traces sans recouvrement en plan.");
        double sm = (r0 + r1) / 2;
        // point de la trace 2 à l'abscisse sm
        double f = (sm - t2a) / (t2b - t2a);
        var p2 = a2 + (b2 - a2) * f;
        double decalage = new Vecteur3(p2.X - a1.X, p2.Y - a1.Y, 0).Scalaire(n);
        double dz = ((a2.Z + b2.Z) - (a1.Z + b1.Z)) / 2;
        return new ResultatDecalage(decalage, dz, angle, r1 - r0);
    }
}

/// <summary>Décalage entre traces : <c>DeversDeg</c> = atan(décalage / Δz).</summary>
public sealed record ResultatDecalage(double DecalageM, double DeltaZM, double AngleEntreTracesDeg, double RecouvrementM)
{
    public double DeversDeg => Math.Abs(DeltaZM) < 1e-9 ? double.NaN : Math.Atan(DecalageM / DeltaZM) * 180 / Math.PI;
}
