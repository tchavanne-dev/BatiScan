using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;
using BatiScan.Core.Snap;

namespace BatiScan.Infrastructure;

/// <summary>Point accroché : clic, point MESURÉ retenu, précision annoncée (E23).</summary>
public sealed record ResultatSnap(Vecteur3 Clic, Vecteur3 Point, bool Affine, double EcartClicM, double LatenceMs, string Precision);

/// <summary>
/// E23 – Accrochage au nuage réactif et vérifiable.
/// <list type="number">
/// <item>Préparation du cache : une seule extraction <c>GetPoints</c> sur la zone EXPLICITE,
/// indexée dans une <see cref="GrilleSpatiale"/> (coût publié séparément).</item>
/// <item>Accrochage : recherche locale dans la grille (pas de nouvel appel Revit coûteux).</item>
/// <item>Affinage : nouvel extrait à pleine densité dans une petite boîte autour du candidat ;
/// à défaut, le point est déclaré « approximatif (aperçu) ».</item>
/// </list>
/// Le cache est invalidé dès que la source, sa révision, sa transformation ou la zone change (E22).
/// </summary>
public static class ServiceSnap
{
    private static GrilleSpatiale? _grille;
    public static StatistiquesLatence Statistiques { get; private set; } = new();

    public static void Invalider(string? cleSource = null)
    {
        if (_grille is null) return;
        if (cleSource is null || _grille.CleCache.StartsWith(cleSource, StringComparison.Ordinal))
        {
            JournalLocal.Ecrire("snap.cache_invalide", new { cle = _grille.CleCache });
            _grille = null;
            Statistiques = new StatistiquesLatence();
        }
    }

    private static string Cle(Contexte ctx, NuageSource n, BoiteZone z) =>
        $"{n.EmpreinteContexte(ctx.Etat())}|{z.Min}|{z.Max}|{z.RotationDeg}";

    public static GrilleSpatiale Preparer(Contexte ctx, NuageSource nuage, BoiteZone zone)
    {
        string cle = Cle(ctx, nuage, zone);
        if (_grille is not null && _grille.CleCache == cle) return _grille;
        Invalider();
        var sw = Stopwatch.StartNew();
        var e = ExtractionPoints.Extraire(nuage, zone, ctx.Parametres.DensiteCacheSnapM, ctx.Parametres.PointsMaxParExtraction);
        if (e.Points.Count == 0) throw new ArretCommande($"Aucun point dans la zone de recherche {zone.Decrire()}.");
        _grille = new GrilleSpatiale(e.Points, ctx.Parametres.TailleCelluleSnapM, cle);
        Statistiques = new StatistiquesLatence { PreparationCacheMs = sw.Elapsed.TotalMilliseconds };
        JournalLocal.Ecrire("snap.cache_prepare", new { nuage = nuage.Nom, zone = zone.Decrire(), points = e.Points.Count, plafond = e.PlafondAtteint, ms = sw.Elapsed.TotalMilliseconds });
        return _grille;
    }

    /// <summary>
    /// Demande un clic à l'utilisateur et retourne le point mesuré accroché, ou null si
    /// « aucun point valide » ou Échap. Ne retourne JAMAIS une valeur précédente.
    /// </summary>
    public static ResultatSnap? Accrocher(Contexte ctx, NuageSource nuage, BoiteZone zone, string invite)
    {
        var grille = Preparer(ctx, nuage, zone);
        XYZ clicPieds;
        try { clicPieds = ctx.UiDoc.Selection.PickPoint(ObjectSnapTypes.None, invite); }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return null; }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            throw new ArretCommande("Aucun plan de construction actif dans cette vue : utiliser une vue en plan, ou définir un plan de construction en 3D.");
        }

        var sw = Stopwatch.StartNew();
        var clic = ConversionRevit.VersM(clicPieds);
        bool enPlan = ctx.Doc.ActiveView is ViewPlan;
        double r = ctx.Parametres.RayonSnapM;
        CandidatSnap? c = enPlan ? grille.PlusProcheEnPlan(clic, r, zone.Min.Z, zone.Max.Z) : grille.PlusProche3D(clic, r);
        if (c is null)
        {
            Statistiques.Ajouter(sw.Elapsed.TotalMilliseconds);
            return new ResultatSnap(clic, clic, false, double.NaN, sw.Elapsed.TotalMilliseconds, "AUCUN POINT VALIDE dans le rayon de recherche");
        }

        // Affinage à pleine densité autour du candidat (données de référence).
        var cand = c.Value.Point;
        double d = ctx.Parametres.DemiCoteAffinageM;
        var boite = new BoiteZone("affinage", cand - new Vecteur3(d, d, d), cand + new Vecteur3(d, d, d), 0);
        Vecteur3 retenu = cand;
        bool affine = false;
        try
        {
            var fin = ExtractionPoints.Extraire(nuage, boite, 0.0005, 200_000).Points.Where(zone.Contient).ToList();
            if (fin.Count > 0)
            {
                retenu = enPlan
                    ? fin.Where(p => p.Z >= zone.Min.Z && p.Z <= zone.Max.Z).DefaultIfEmpty(cand).MinBy(p => Math.Pow(p.X - clic.X, 2) + Math.Pow(p.Y - clic.Y, 2))
                    : fin.MinBy(p => p.Distance(clic));
                affine = true;
            }
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { /* boîte d'affinage refusée : on garde l'aperçu */ }
        double ms = sw.Elapsed.TotalMilliseconds;
        Statistiques.Ajouter(ms);
        double ecart = enPlan ? Math.Sqrt(Math.Pow(retenu.X - clic.X, 2) + Math.Pow(retenu.Y - clic.Y, 2)) : retenu.Distance(clic);
        string precision = affine
            ? "point mesuré à pleine densité (affiné)"
            : $"APPROXIMATIF : point de l'aperçu (espacement ≈ {ctx.Parametres.DensiteCacheSnapM * 1000:F0} mm)";
        JournalLocal.Ecrire("snap.accrochage", new { clic = clic.ToString(), point = retenu.ToString(), affine, ecart_m = ecart, ms });
        return new ResultatSnap(clic, retenu, affine, ecart, ms, precision);
    }
}
