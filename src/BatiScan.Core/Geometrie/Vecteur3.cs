namespace BatiScan.Core.Geometrie;

/// <summary>
/// Vecteur / point 3D en <b>mètres</b> (convention de toute la bibliothèque Core).
/// La conversion vers les pieds internes Revit se fait exclusivement dans
/// BatiScan.Revit via <c>UnitUtils</c>.
/// </summary>
public readonly record struct Vecteur3(double X, double Y, double Z)
{
    // ── Constantes ──────────────────────────────────────────────────────────
    public static readonly Vecteur3 Zero = new(0, 0, 0);
    public static readonly Vecteur3 AxeX = new(1, 0, 0);
    public static readonly Vecteur3 AxeY = new(0, 1, 0);
    public static readonly Vecteur3 AxeZ = new(0, 0, 1);

    // ── Opérateurs ──────────────────────────────────────────────────────────
    public static Vecteur3 operator +(Vecteur3 a, Vecteur3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vecteur3 operator -(Vecteur3 a, Vecteur3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vecteur3 operator -(Vecteur3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vecteur3 operator *(Vecteur3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vecteur3 operator *(double k, Vecteur3 a) => a * k;
    public static Vecteur3 operator /(Vecteur3 a, double k) => new(a.X / k, a.Y / k, a.Z / k);

    public double Scalaire(Vecteur3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vecteur3 Vectoriel(Vecteur3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Norme => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double NormePlan => Math.Sqrt(X * X + Y * Y);

    /// <summary>Vecteur unitaire ; lève une exception sur un vecteur nul (géométrie dégénérée).</summary>
    public Vecteur3 Unitaire()
    {
        double n = Norme;
        if (n < 1e-12) throw new InvalidOperationException("Vecteur nul : direction indéfinie.");
        return this / n;
    }

    public double Distance(Vecteur3 b) => (this - b).Norme;

    public static Vecteur3 DepuisTableau(IReadOnlyList<double> v) =>
        v.Count >= 3 ? new(v[0], v[1], v[2]) : new(v[0], v[1], 0);

    public double[] VersTableau() => [X, Y, Z];

    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({X:F4}; {Y:F4}; {Z:F4}) m");
}
