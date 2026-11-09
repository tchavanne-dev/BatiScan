using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Scan;

namespace BatiScan.Core.Tests;

public class TestsEtat
{
    [Fact]
    public void Json_AllerRetour_EtInvalidationParSource()
    {
        var e = new EtatProjet { NordTravailDeg = 12.5 };
        e.Zones.Add(new ZoneTravail { Nom = "RDC", MinM = [0, 0, 0], MaxM = [5, 4, 3], CleSource = "S1" });
        e.Zones.Add(new ZoneTravail { Nom = "Autre", MinM = [0, 0, 0], MaxM = [1, 1, 1], CleSource = "S2" });
        e.Analyses.Add(new AnalyseEnregistree { Operation = "ecarts", CleSource = "S1" });
        e.AjouterRevision("S1", new RevisionScan
        {
            Empreinte = new EmpreinteSource("a.rcp", true, 1, DateTime.UtcNow, "abc", 0, 0),
            TransformationInstance = TransformationRigide.Identite.VersMatrice4x4(),
            PointsControle = [new PointControle("PC1", new Vecteur3(1, 2, 3))],
        });

        var e2 = EtatProjet.DepuisJson(e.VersJson());
        Assert.Equal(12.5, e2.NordTravailDeg);
        Assert.Equal(new Vecteur3(1, 2, 3), e2.DerniereRevision("S1")!.PointsControle[0].LocalNuageM);
        Assert.Equal(2, e2.InvaliderSource("S1", "contenu modifié"));
        Assert.False(e2.Zones[0].Valide);
        Assert.True(e2.Zones[1].Valide);
        Assert.Equal("a_recalculer", e2.Analyses[0].Statut);
    }

    [Fact]
    public void FormatFutur_Refuse() =>
        Assert.Throws<InvalidOperationException>(() => EtatProjet.DepuisJson("{\"Version\":99}"));
}
