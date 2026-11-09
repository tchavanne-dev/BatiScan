namespace BatiScan.Core.Geometrie;

/// <summary>
/// Transformation rigide p' = R·p + t (rotation orthonormée, translation en mètres).
/// Utilisée pour tous les changements de repère (E02) : repère source, repère
/// de calcul, transformation d'instance, interne Revit, partagé. Toute
/// transformation conserve son inverse exact (réversibilité E02).
/// </summary>
public sealed class TransformationRigide
{
    // ── Données ─────────────────────────────────────────────────────────────
    /// <summary>Matrice de rotation 3×3, ligne par ligne.</summary>
    private readonly double[] _r;

    public Vecteur3 Translation { get; }

    public TransformationRigide(double[] rotationLigneParLigne, Vecteur3 translation)
    {
        if (rotationLigneParLigne.Length != 9) throw new ArgumentException("Rotation 3×3 attendue (9 valeurs).");
        _r = (double[])rotationLigneParLigne.Clone();
        Translation = translation;
    }

    public static TransformationRigide Identite { get; } = new([1, 0, 0, 0, 1, 0, 0, 0, 1], Vecteur3.Zero);

    /// <summary>Rotation d'angle <paramref name="angleRad"/> (sens trigonométrique) autour de l'axe Z passant par <paramref name="centre"/>.</summary>
    public static TransformationRigide RotationZ(double angleRad, Vecteur3 centre)
    {
        double c = Math.Cos(angleRad), s = Math.Sin(angleRad);
        double[] r = [c, -s, 0, s, c, 0, 0, 0, 1];
        var rot = new TransformationRigide(r, Vecteur3.Zero);
        // t = centre − R·centre
        return new TransformationRigide(r, centre - rot.AppliquerVecteur(centre));
    }

    public static TransformationRigide Translater(Vecteur3 t) => new([1, 0, 0, 0, 1, 0, 0, 0, 1], t);

    /// <summary>Construit depuis une matrice 4×4 ligne par ligne (contrat JSON).</summary>
    public static TransformationRigide DepuisMatrice4x4(IReadOnlyList<double> m)
    {
        if (m.Count != 16) throw new ArgumentException("Matrice 4×4 attendue (16 valeurs).");
        return new([m[0], m[1], m[2], m[4], m[5], m[6], m[8], m[9], m[10]], new Vecteur3(m[3], m[7], m[11]));
    }

    public double[] VersMatrice4x4() =>
    [
        _r[0], _r[1], _r[2], Translation.X,
        _r[3], _r[4], _r[5], Translation.Y,
        _r[6], _r[7], _r[8], Translation.Z,
        0, 0, 0, 1,
    ];

    public double[] Rotation => (double[])_r.Clone();

    // ── Application ─────────────────────────────────────────────────────────
    public Vecteur3 AppliquerVecteur(Vecteur3 v) => new(
        _r[0] * v.X + _r[1] * v.Y + _r[2] * v.Z,
        _r[3] * v.X + _r[4] * v.Y + _r[5] * v.Z,
        _r[6] * v.X + _r[7] * v.Y + _r[8] * v.Z);

    public Vecteur3 Appliquer(Vecteur3 p) => AppliquerVecteur(p) + Translation;

    /// <summary>Inverse exact : p = Rᵀ·(p' − t).</summary>
    public TransformationRigide Inverse()
    {
        double[] rt = [_r[0], _r[3], _r[6], _r[1], _r[4], _r[7], _r[2], _r[5], _r[8]];
        var inv = new TransformationRigide(rt, Vecteur3.Zero);
        return new TransformationRigide(rt, -inv.AppliquerVecteur(Translation));
    }

    /// <summary>Composition : (this ∘ autre)(p) = this(autre(p)).</summary>
    public TransformationRigide Apres(TransformationRigide autre)
    {
        var a = _r; var b = autre._r;
        double[] r = new double[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
        return new TransformationRigide(r, Appliquer(autre.Translation));
    }

    // ── Diagnostics ─────────────────────────────────────────────────────────
    /// <summary>Angle de rotation total (degrés), indépendant de l'axe.</summary>
    public double AngleRotationDeg()
    {
        double trace = _r[0] + _r[4] + _r[8];
        return Math.Acos(Math.Clamp((trace - 1) / 2, -1, 1)) * 180 / Math.PI;
    }

    /// <summary>Rotation autour de Z (degrés) projetée en plan.</summary>
    public double AngleEnPlanDeg() => Math.Atan2(_r[3], _r[0]) * 180 / Math.PI;

    /// <summary>Inclinaison de l'axe Z transformé par rapport à la verticale (degrés).</summary>
    public double InclinaisonVerticaleDeg() => Math.Acos(Math.Clamp(_r[8], -1, 1)) * 180 / Math.PI;

    public bool EstIdentite(double toleranceTranslationM = 1e-6, double toleranceAngleDeg = 1e-6) =>
        Translation.Norme <= toleranceTranslationM && AngleRotationDeg() <= toleranceAngleDeg;

    /// <summary>Empreinte textuelle stable (invalidation des caches, E22/E23).</summary>
    public string Empreinte() =>
        string.Join(";", VersMatrice4x4().Take(12).Select(v => Math.Round(v, 9).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
}
