using BatiScan.Core.Contrats;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Moteur;
using BatiScan.Core.Murs;

namespace BatiScan.Core.Tests;

/// <summary>
/// Aller-retour réel C# → processus CPython (venv) → C# sur données synthétiques (J07),
/// sans Revit. Vérifie le contrat 1.0.0, la précision float32 décalée et le mode E24.
/// </summary>
public class TestsIntegrationMoteur
{
    private static readonly ClientMoteur Client = new(null);

    private static List<Vecteur3> MurIncline(Random rng, double y0, double angleDeg, double epaisseur, Vecteur3 decalage)
    {
        var pts = new List<Vecteur3>();
        double k = Math.Tan(angleDeg * Math.PI / 180);
        foreach (double face in new[] { 0.0, epaisseur })
            for (int i = 0; i < 60_000; i++)
            {
                double x = rng.NextDouble() * 5, z = rng.NextDouble() * 2.6, bruit = (rng.NextDouble() - 0.5) * 0.004;
                pts.Add(new Vecteur3(x, y0 + face + k * z + bruit, z) + decalage);
            }
        return pts;
    }

    private static RequeteMoteur Requete(string op) => new()
    {
        Operation = op,
        Repere = new Repere { Description = "test d'intégration" },
        Source = new SourceNuage { Id = "synthetique", Chemin = "memoire", Empreinte = "sha256:test" },
        Points = new SpecPoints { Chemin = "points.bin" },
    };

    [Fact]
    public void Murs_J07_AllerRetourMoteurReel()
    {
        var etat = Client.VerifierDisponibilite();
        if (!etat.Disponible) return; // poste sans venv : test sans objet (signalé par le diagnostic)

        var decalage = new Vecteur3(152_345, 167_890, 120); // coordonnées « géoréférencées »
        var rng = new Random(1);
        var pts = MurIncline(rng, 0, 1.0, 0.20, decalage);
        var rep = Client.Executer(Requete("murs"), pts, TimeSpan.FromMinutes(5), null, CancellationToken.None);

        Assert.Equal("1.0.0", rep.VersionContrat);
        Assert.False(rep.EnErreur, rep.ResumeErreurs());
        Assert.Equal(pts.Count, rep.NombrePoints.Recus);
        var mur = Assert.Single(rep.Lire<PropMur>("mur"));
        Assert.True(mur.EpaisseurMesuree);
        Assert.InRange(mur.EpaisseurM!.Value, 0.195, 0.205);
        Assert.InRange(Math.Abs(mur.Devers.AngleDeg!.Value), 0.95, 1.05);
        Assert.Equal("non_plombe", mur.Plombage);
        Assert.InRange(mur.ZBas, 119.99, 120.01); // précision conservée à 150 km

        // E24 : conversion côté C# dans les deux modes
        var mesure = new MurMesure(mur.Id, Vecteur3.DepuisTableau(mur.Debut), Vecteur3.DepuisTableau(mur.Fin), mur.ZBas, mur.ZHaut, mur.EpaisseurM,
            Math.Abs(mur.Devers.PenteMParM!.Value), new Vecteur3(mur.Devers.DirectionInclinaison![0], mur.Devers.DirectionInclinaison[1], 0), mur.Devers.Incertain, mur.Plombage);
        var incline = ConversionMurNonPlombe.Convertir(mesure, ModeMurNonPlombe.InclineFidele, ReferenceVerticale.Base, 0.2);
        var vertical = ConversionMurNonPlombe.Convertir(mesure, ModeMurNonPlombe.VerticalAvecRapport, ReferenceVerticale.MiHauteur, 0.2);
        Assert.InRange(incline.AngleDepuisVerticaleDeg, 0.95, 1.05);
        Assert.Equal(0, vertical.AngleDepuisVerticaleDeg);
        Assert.InRange(vertical.EcartTeteM - vertical.EcartPiedM, 0.040, 0.051); // tan(1°)·2,6 m ≈ 45 mm
    }

    [Fact]
    public void ErreurMoteur_RetourneeDansLeContrat()
    {
        if (!Client.VerifierDisponibilite().Disponible) return;
        var req = Requete("niveaux");
        req.Parametres["parametre_inexistant"] = 1;
        var rep = Client.Executer(req, [new Vecteur3(0, 0, 0)], TimeSpan.FromMinutes(1), null, CancellationToken.None);
        Assert.True(rep.EnErreur);
        Assert.Equal("PARAMETRE_INCONNU", rep.Erreurs[0].Code);
    }

    [Fact]
    public void Annulation_TermineLeProcessus()
    {
        if (!Client.VerifierDisponibilite().Disponible) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var pts = MurIncline(new Random(2), 0, 0, 0.2, Vecteur3.Zero);
        Assert.ThrowsAny<OperationCanceledException>(() => Client.Executer(Requete("murs"), pts, TimeSpan.FromMinutes(5), null, cts.Token));
    }
}
