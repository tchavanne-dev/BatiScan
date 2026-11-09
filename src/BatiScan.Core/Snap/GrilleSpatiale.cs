using BatiScan.Core.Geometrie;

namespace BatiScan.Core.Snap;

/// <summary>Candidat d'accrochage : point MESURÉ du nuage (jamais interpolé).</summary>
public readonly record struct CandidatSnap(int Indice, Vecteur3 Point, double DistanceM);

/// <summary>
/// E23 – Index spatial par hachage de voxels pour l'accrochage au nuage.
/// <para>
/// Construit une fois sur la zone de recherche explicitement choisie
/// (« préparation du cache »), puis interrogé à chaque clic (« accrochages
/// suivants ») : seuls les voxels voisins du clic sont parcourus, d'où une
/// latence quasi indépendante de la taille du nuage. Un point hors zone n'est
/// jamais dans l'index et ne peut donc pas être accroché (E23 « Périmètre »).
/// </para>
/// </summary>
public sealed class GrilleSpatiale
{
    private readonly float[] _x, _y, _z;
    private readonly Vecteur3 _origine;
    private readonly double _cellule;
    private readonly Dictionary<long, int[]> _cellules;

    public int NombrePoints => _x.Length;
    public double TailleCelluleM => _cellule;
    /// <summary>Clé d'invalidation : source, révision, transformation, zone (E22).</summary>
    public string CleCache { get; }

    /// <param name="points">Points en mètres (repère interne) – stockés en float32 relatifs à <paramref name="origine"/>.</param>
    public GrilleSpatiale(IReadOnlyList<Vecteur3> points, double celluleM, string cleCache)
    {
        if (celluleM <= 0) throw new ArgumentOutOfRangeException(nameof(celluleM));
        _cellule = celluleM;
        CleCache = cleCache;
        _origine = points.Count > 0 ? points[0] : Vecteur3.Zero;
        int n = points.Count;
        _x = new float[n]; _y = new float[n]; _z = new float[n];
        var listes = new Dictionary<long, List<int>>();
        for (int i = 0; i < n; i++)
        {
            var p = points[i] - _origine;
            _x[i] = (float)p.X; _y[i] = (float)p.Y; _z[i] = (float)p.Z;
            long k = Cle(Ix(p.X), Ix(p.Y), Ix(p.Z));
            if (!listes.TryGetValue(k, out var l)) listes[k] = l = new List<int>(8);
            l.Add(i);
        }
        _cellules = listes.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    public Vecteur3 Point(int i) => _origine + new Vecteur3(_x[i], _y[i], _z[i]);

    /// <summary>Point mesuré le plus proche en 3D dans <paramref name="rayonM"/>, ou null (« aucun point valide »).</summary>
    public CandidatSnap? PlusProche3D(Vecteur3 cible, double rayonM) => Chercher(cible, rayonM, double.NegativeInfinity, double.PositiveInfinity, plan: false);

    /// <summary>
    /// Point le plus proche EN PLAN du clic (vue en plan), restreint à la tranche [zMin, zMax].
    /// </summary>
    public CandidatSnap? PlusProcheEnPlan(Vecteur3 clic, double rayonM, double zMin, double zMax) => Chercher(clic, rayonM, zMin, zMax, plan: true);

    private CandidatSnap? Chercher(Vecteur3 cible, double rayon, double zMin, double zMax, bool plan)
    {
        var c = cible - _origine;
        double z0 = zMin - _origine.Z, z1 = zMax - _origine.Z;
        int r = (int)Math.Ceiling(rayon / _cellule);
        int cx = Ix(c.X), cy = Ix(c.Y), cz = Ix(c.Z);
        int zA, zB;
        if (plan)
        {
            if (double.IsInfinity(z0) || double.IsInfinity(z1)) throw new ArgumentException("Tranche d'altitude requise en plan.");
            zA = Ix(z0); zB = Ix(z1);
        }
        else { zA = cz - r; zB = cz + r; }
        double meilleur = rayon * rayon;
        int iMeilleur = -1;
        for (int i = cx - r; i <= cx + r; i++)
            for (int j = cy - r; j <= cy + r; j++)
                for (int k = zA; k <= zB; k++)
                {
                    if (!_cellules.TryGetValue(Cle(i, j, k), out var idx)) continue;
                    foreach (int p in idx)
                    {
                        double dx = _x[p] - c.X, dy = _y[p] - c.Y, dz = plan ? 0 : _z[p] - c.Z;
                        if (plan && (_z[p] < z0 || _z[p] > z1)) continue;
                        double d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 <= meilleur) { meilleur = d2; iMeilleur = p; }
                    }
                }
        return iMeilleur < 0 ? null : new CandidatSnap(iMeilleur, Point(iMeilleur), Math.Sqrt(meilleur));
    }

    private int Ix(double v) => (int)Math.Floor(v / _cellule);

    private static long Cle(int i, int j, int k) =>
        ((long)(i & 0x1FFFFF) << 42) | ((long)(j & 0x1FFFFF) << 21) | (long)(k & 0x1FFFFF);
}

/// <summary>E23 – Mesures de latence publiées séparément : premier accès, cache, accrochages suivants.</summary>
public sealed class StatistiquesLatence
{
    private readonly List<double> _accrochages = [];
    public double? PreparationCacheMs { get; set; }
    public double? PremierAccesMs { get; private set; }
    public IReadOnlyList<double> Accrochages => _accrochages;

    public void Ajouter(double ms)
    {
        if (PremierAccesMs is null) PremierAccesMs = ms;
        else _accrochages.Add(ms);
    }

    public double? Mediane => Percentile(50);
    public double? P95 => Percentile(95);

    public double? Percentile(double p)
    {
        if (_accrochages.Count == 0) return null;
        var s = _accrochages.OrderBy(v => v).ToArray();
        double rang = p / 100 * (s.Length - 1);
        int lo = (int)Math.Floor(rang), hi = (int)Math.Ceiling(rang);
        return s[lo] + (s[hi] - s[lo]) * (rang - lo);
    }

    public string Resume(double cibleP95Ms) =>
        $"Cache : {Fmt(PreparationCacheMs)} ; 1er accès : {Fmt(PremierAccesMs)} ; suivants (n={_accrochages.Count}) : médiane {Fmt(Mediane)}, p95 {Fmt(P95)}"
        + (P95 is double p ? (p <= cibleP95Ms ? $" (≤ cible {cibleP95Ms} ms)" : $" (> cible {cibleP95Ms} ms)") : "");

    private static string Fmt(double? v) => v is null ? "–" : $"{v:F1} ms";
}
