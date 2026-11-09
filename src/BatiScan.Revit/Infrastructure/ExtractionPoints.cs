using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;

namespace BatiScan.Infrastructure;

/// <summary>Boîte de zone orientée autour de Z (mètres, repère interne) – centre de boîte.</summary>
public sealed record BoiteZone(string Nom, Vecteur3 Min, Vecteur3 Max, double RotationDeg)
{
    public Vecteur3 Centre => (Min + Max) / 2;
    public double EpaisseurM => Max.Z - Min.Z;

    public static BoiteZone Depuis(ZoneTravail z) =>
        new(z.Nom, Vecteur3.DepuisTableau(z.MinM), Vecteur3.DepuisTableau(z.MaxM), z.RotationDeg);

    public bool Contient(Vecteur3 p)
    {
        var c = Centre;
        double a = -RotationDeg * Math.PI / 180;
        double dx = p.X - c.X, dy = p.Y - c.Y;
        double x = c.X + Math.Cos(a) * dx - Math.Sin(a) * dy;
        double y = c.Y + Math.Sin(a) * dx + Math.Cos(a) * dy;
        return x >= Min.X && x <= Max.X && y >= Min.Y && y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;
    }

    public Core.Contrats.ZoneJson VersJson() => new()
    {
        Nom = Nom,
        Min = Min.VersTableau(),
        Max = Max.VersTableau(),
        RotationDeg = Math.Abs(RotationDeg) > 1e-12 ? RotationDeg : null,
    };

    public string Decrire() =>
        $"« {Nom} » : {Max.X - Min.X:F2} × {Max.Y - Min.Y:F2} m, épaisseur {EpaisseurM:F2} m (z {Min.Z:F3} → {Max.Z:F3} m), rotation {RotationDeg:F2}°";
}

/// <summary>Résultat d'extraction : points de zone en mètres (repère interne Revit) et diagnostic.</summary>
public sealed record PointsExtraits(List<Vecteur3> Points, int Demandes, bool PlafondAtteint, RepereGetPoints Repere, double DensiteM);

/// <summary>
/// Extraction des points d'une zone EXPLICITE (E05, E23) via <c>PointCloudInstance.GetPoints</c>.
/// Doit être appelée sur le thread Revit (API). Lecture seule.
/// <para>
/// La doc API indique que le filtre est en coordonnées du modèle ; le repère des points
/// RETOURNÉS n'y est pas précisé. Il est donc déterminé à l'exécution (points dans la boîte
/// tels quels, ou après application de la transformation d'instance) et journalisé.
/// </para>
/// </summary>
public static class ExtractionPoints
{
    public static PointsExtraits Extraire(NuageSource nuage, BoiteZone zone, double densiteM, int maxPoints)
    {
        maxPoints = Math.Clamp(maxPoints, 1, 1_000_000); // limite documentée de GetPoints
        var filtre = PointCloudFilterFactory.CreateMultiPlaneFilter(PlansBoite(zone));
        PointCollection collection = nuage.Instance.GetPoints(filtre, ConversionRevit.VersPieds(densiteM), maxPoints);

        var bruts = new List<Vecteur3>(collection.Count);
        foreach (CloudPoint cp in collection)
            bruts.Add(new Vecteur3(ConversionRevit.VersM(cp.X), ConversionRevit.VersM(cp.Y), ConversionRevit.VersM(cp.Z)));

        var repere = DeterminerRepere(nuage, zone, bruts);
        var t = nuage.TransformationInstance;
        var points = repere == RepereGetPoints.LocalNuage ? bruts.Select(t.Appliquer).ToList() : bruts;
        // Le C# ne transmet que la zone demandée : un point hors zone ne peut pas être utilisé (E23).
        points = points.Where(zone.Contient).ToList();
        return new PointsExtraits(points, maxPoints, collection.Count >= maxPoints, repere, densiteM);
    }

    /// <summary>Six plans orientés vers l'intérieur de la boîte (pieds, coordonnées modèle).</summary>
    public static IList<Plane> PlansBoite(BoiteZone z)
    {
        var c = z.Centre;
        var rot = TransformationRigide.RotationZ(z.RotationDeg * Math.PI / 180, c);
        var ux = rot.AppliquerVecteur(Vecteur3.AxeX);
        var uy = rot.AppliquerVecteur(Vecteur3.AxeY);
        double hx = (z.Max.X - z.Min.X) / 2, hy = (z.Max.Y - z.Min.Y) / 2;
        Plane P(Vecteur3 normale, Vecteur3 origine) =>
            Plane.CreateByNormalAndOrigin(ConversionRevit.Direction(normale), ConversionRevit.VersPieds(origine));
        return
        [
            P(ux, c - ux * hx), P(-ux, c + ux * hx),
            P(uy, c - uy * hy), P(-uy, c + uy * hy),
            P(Vecteur3.AxeZ, new Vecteur3(c.X, c.Y, z.Min.Z)), P(-Vecteur3.AxeZ, new Vecteur3(c.X, c.Y, z.Max.Z)),
        ];
    }

    private static RepereGetPoints DeterminerRepere(NuageSource nuage, BoiteZone zone, List<Vecteur3> bruts)
    {
        if (NuageSource.RepereDetecte.TryGetValue(nuage.Cle, out var connu) && connu != RepereGetPoints.Inconnu) return connu;
        if (bruts.Count == 0 || nuage.TransformationInstance.EstIdentite(1e-6, 1e-6)) return RepereGetPoints.Modele;
        var echant = bruts.Where((_, i) => i % Math.Max(1, bruts.Count / 2000) == 0).ToList();
        double dedansModele = echant.Count(zone.Contient) / (double)echant.Count;
        double dedansLocal = echant.Select(nuage.TransformationInstance.Appliquer).Count(zone.Contient) / (double)echant.Count;
        var r = dedansModele >= dedansLocal ? RepereGetPoints.Modele : RepereGetPoints.LocalNuage;
        if (Math.Max(dedansModele, dedansLocal) < 0.5)
        {
            JournalLocal.Ecrire("extraction.repere_indetermine", new { nuage = nuage.Nom, dedansModele, dedansLocal }, "avertissement");
            return RepereGetPoints.Inconnu;
        }
        NuageSource.RepereDetecte[nuage.Cle] = r;
        JournalLocal.Ecrire("extraction.repere_detecte", new { nuage = nuage.Nom, repere = r.ToString(), dedansModele, dedansLocal });
        return r;
    }

    /// <summary>Boîte englobante du nuage (modèle) en zone axis-alignée.</summary>
    public static BoiteZone BoiteNuage(NuageSource nuage)
    {
        BoundingBoxXYZ bb = nuage.Instance.get_BoundingBox(null)
            ?? throw new InvalidOperationException("Boîte englobante du nuage indisponible.");
        return new BoiteZone($"Nuage entier – {nuage.Nom}", ConversionRevit.VersM(bb.Min), ConversionRevit.VersM(bb.Max), 0);
    }
}
