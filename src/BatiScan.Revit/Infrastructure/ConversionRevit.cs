using Autodesk.Revit.DB;
using BatiScan.Core.Geometrie;

namespace BatiScan.Infrastructure;

/// <summary>
/// Conversions EXPLICITES pieds internes Revit ↔ mètres (Core).
/// Toute conversion de longueur passe par <see cref="UnitUtils"/> (règle CLAUDE.md).
/// </summary>
public static class ConversionRevit
{
    // ── Longueurs ───────────────────────────────────────────────────────────
    public static double VersM(double pieds) => UnitUtils.ConvertFromInternalUnits(pieds, UnitTypeId.Meters);
    public static double VersPieds(double metres) => UnitUtils.ConvertToInternalUnits(metres, UnitTypeId.Meters);

    // ── Angles (interne Revit = radians) ─────────────────────────────────────
    public static double VersDeg(double radians) => UnitUtils.ConvertFromInternalUnits(radians, UnitTypeId.Degrees);
    public static double VersRad(double degres) => UnitUtils.ConvertToInternalUnits(degres, UnitTypeId.Degrees);

    // ── Points ──────────────────────────────────────────────────────────────
    public static Vecteur3 VersM(XYZ p) => new(VersM(p.X), VersM(p.Y), VersM(p.Z));
    public static XYZ VersPieds(Vecteur3 v) => new(VersPieds(v.X), VersPieds(v.Y), VersPieds(v.Z));

    /// <summary>Vecteur de direction (sans unité) : aucune conversion de longueur.</summary>
    public static Vecteur3 Direction(XYZ d) => new(d.X, d.Y, d.Z);
    public static XYZ Direction(Vecteur3 d) => new(d.X, d.Y, d.Z);

    // ── Transformations ─────────────────────────────────────────────────────
    /// <summary>
    /// Revit : p' = Origin + x·BasisX + y·BasisY + z·BasisZ (bases en colonnes).
    /// Lève une exception si la transformation n'est pas rigide (échelle ≠ 1) : jamais ignorée.
    /// </summary>
    public static TransformationRigide VersM(Transform t)
    {
        if (Math.Abs(t.Scale - 1.0) > 1e-9)
            throw new InvalidOperationException($"Transformation avec échelle {t.Scale:F6} : non rigide, non prise en charge.");
        double[] r =
        [
            t.BasisX.X, t.BasisY.X, t.BasisZ.X,
            t.BasisX.Y, t.BasisY.Y, t.BasisZ.Y,
            t.BasisX.Z, t.BasisY.Z, t.BasisZ.Z,
        ];
        return new TransformationRigide(r, VersM(t.Origin));
    }

    public static Transform VersRevit(TransformationRigide t)
    {
        double[] r = t.Rotation;
        Transform res = Transform.Identity;
        res.BasisX = new XYZ(r[0], r[3], r[6]);
        res.BasisY = new XYZ(r[1], r[4], r[7]);
        res.BasisZ = new XYZ(r[2], r[5], r[8]);
        res.Origin = VersPieds(t.Translation);
        return res;
    }

    // ── Formatage ───────────────────────────────────────────────────────────
    private static readonly System.Globalization.CultureInfo Fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    public static string M(double v, int dec = 3) => v.ToString("F" + dec, Fr) + " m";
    public static string Mm(double v) => (v * 1000).ToString("F1", Fr) + " mm";
    public static string Deg(double v, int dec = 2) => v.ToString("F" + dec, Fr) + "°";
    public static string P(Vecteur3 p) => $"({p.X.ToString("F3", Fr)} ; {p.Y.ToString("F3", Fr)} ; {p.Z.ToString("F3", Fr)}) m";
}
