namespace BatiScan.Core.Geometrie;

/// <summary>Résultat d'un ajustement rigide par points de contrôle (E02, E22).</summary>
public sealed record ResultatAjustement(
    TransformationRigide Transformation,
    IReadOnlyList<double> Residus,
    double RmsM,
    double MaxM,
    int NombrePoints,
    string Methode)
{
    /// <summary>Indice du point au plus fort résidu.</summary>
    public int IndicePireResidu => Residus.Count == 0 ? -1 : Residus.Select((r, i) => (r, i)).MaxBy(t => t.r).i;
}

/// <summary>
/// Ajustements rigides source → cible au sens des moindres carrés.
/// <list type="bullet">
/// <item><b>4 DDL</b> (rotation autour de Z + translation 3D) : calage d'un nuage de
/// bâtiment dont la verticale est déjà connue (E02, E03).</item>
/// <item><b>6 DDL</b> (méthode de Horn par quaternion) : contrôle d'un nouveau
/// recalage ReCap (E22), pour détecter une éventuelle inclinaison.</item>
/// </list>
/// Aucun facteur d'échelle n'est estimé : un écart d'échelle apparaît dans les résidus.
/// </summary>
public static class AjustementRigide
{
    // ────────────────────────────────────────────────────────────────
    //  4 DDL : rotation en plan + translation
    // ────────────────────────────────────────────────────────────────

    public static ResultatAjustement Ajuster4Ddl(IReadOnlyList<Vecteur3> source, IReadOnlyList<Vecteur3> cible)
    {
        Verifier(source, cible, 2);
        Vecteur3 cs = Centre(source), ct = Centre(cible);
        double num = 0, den = 0, etendue = 0;
        for (int i = 0; i < source.Count; i++)
        {
            Vecteur3 a = source[i] - cs, b = cible[i] - ct;
            num += a.X * b.Y - a.Y * b.X;
            den += a.X * b.X + a.Y * b.Y;
            etendue = Math.Max(etendue, a.NormePlan);
        }
        if (etendue < 1e-3)
            throw new InvalidOperationException("Points de contrôle confondus en plan : rotation indéterminée.");
        double theta = Math.Atan2(num, den);
        var rot = TransformationRigide.RotationZ(theta, Vecteur3.Zero);
        var t = new TransformationRigide(rot.Rotation, ct - rot.AppliquerVecteur(cs));
        return Evaluer(t, source, cible, "4DDL_rotation_Z_translation");
    }

    // ────────────────────────────────────────────────────────────────
    //  6 DDL : méthode de Horn (quaternion unitaire)
    // ────────────────────────────────────────────────────────────────

    public static ResultatAjustement Ajuster6Ddl(IReadOnlyList<Vecteur3> source, IReadOnlyList<Vecteur3> cible)
    {
        Verifier(source, cible, 3);
        Vecteur3 cs = Centre(source), ct = Centre(cible);
        double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
        for (int i = 0; i < source.Count; i++)
        {
            Vecteur3 a = source[i] - cs, b = cible[i] - ct;
            sxx += a.X * b.X; sxy += a.X * b.Y; sxz += a.X * b.Z;
            syx += a.Y * b.X; syy += a.Y * b.Y; syz += a.Y * b.Z;
            szx += a.Z * b.X; szy += a.Z * b.Y; szz += a.Z * b.Z;
        }
        VerifierNonColineaires(source, cs);
        double[,] n =
        {
            { sxx + syy + szz, syz - szy, szx - sxz, sxy - syx },
            { syz - szy, sxx - syy - szz, sxy + syx, szx + sxz },
            { szx - sxz, sxy + syx, -sxx + syy - szz, syz + szy },
            { sxy - syx, szx + sxz, syz + szy, -sxx - syy + szz },
        };
        double[] q = VecteurPropreMax(n);
        double w = q[0], x = q[1], y = q[2], z = q[3];
        double[] r =
        [
            w * w + x * x - y * y - z * z, 2 * (x * y - w * z), 2 * (x * z + w * y),
            2 * (x * y + w * z), w * w - x * x + y * y - z * z, 2 * (y * z - w * x),
            2 * (x * z - w * y), 2 * (y * z + w * x), w * w - x * x - y * y + z * z,
        ];
        var rot = new TransformationRigide(r, Vecteur3.Zero);
        var t = new TransformationRigide(r, ct - rot.AppliquerVecteur(cs));
        return Evaluer(t, source, cible, "6DDL_Horn");
    }

    /// <summary>Résidus d'une transformation donnée (contrôle a posteriori, réversibilité).</summary>
    public static ResultatAjustement Evaluer(TransformationRigide t, IReadOnlyList<Vecteur3> source, IReadOnlyList<Vecteur3> cible, string methode = "controle")
    {
        var residus = new double[source.Count];
        double somme = 0;
        for (int i = 0; i < source.Count; i++)
        {
            residus[i] = t.Appliquer(source[i]).Distance(cible[i]);
            somme += residus[i] * residus[i];
        }
        double rms = source.Count == 0 ? 0 : Math.Sqrt(somme / source.Count);
        return new ResultatAjustement(t, residus, rms, residus.Length == 0 ? 0 : residus.Max(), source.Count, methode);
    }

    // ── Outils ──────────────────────────────────────────────────────────────

    private static void Verifier(IReadOnlyList<Vecteur3> s, IReadOnlyList<Vecteur3> c, int min)
    {
        if (s.Count != c.Count) throw new ArgumentException("Listes source et cible de tailles différentes.");
        if (s.Count < min) throw new ArgumentException($"Au moins {min} points de contrôle sont nécessaires.");
    }

    private static void VerifierNonColineaires(IReadOnlyList<Vecteur3> s, Vecteur3 c)
    {
        Vecteur3 a = s.Select(p => p - c).MaxBy(p => p.Norme);
        if (a.Norme < 1e-3) throw new InvalidOperationException("Points de contrôle confondus.");
        double maxAire = s.Select(p => (p - c).Vectoriel(a).Norme).Max();
        if (maxAire / a.Norme < 1e-3)
            throw new InvalidOperationException("Points de contrôle alignés : rotation 3D indéterminée.");
    }

    private static Vecteur3 Centre(IReadOnlyList<Vecteur3> p)
    {
        var s = Vecteur3.Zero;
        foreach (var v in p) s += v;
        return s / p.Count;
    }

    /// <summary>Vecteur propre de la plus grande valeur propre d'une matrice symétrique 4×4 (Jacobi).</summary>
    internal static double[] VecteurPropreMax(double[,] a0)
    {
        const int n = 4;
        var a = (double[,])a0.Clone();
        var v = new double[n, n];
        for (int i = 0; i < n; i++) v[i, i] = 1;
        for (int balayage = 0; balayage < 100; balayage++)
        {
            double hors = 0;
            for (int p = 0; p < n; p++) for (int q = p + 1; q < n; q++) hors += a[p, q] * a[p, q];
            if (hors < 1e-24) break;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
        }
        int imax = 0;
        for (int i = 1; i < n; i++) if (a[i, i] > a[imax, imax]) imax = i;
        var res = new double[n];
        double norme = 0;
        for (int k = 0; k < n; k++) { res[k] = v[k, imax]; norme += res[k] * res[k]; }
        norme = Math.Sqrt(norme);
        for (int k = 0; k < n; k++) res[k] /= norme;
        return res;
    }
}
