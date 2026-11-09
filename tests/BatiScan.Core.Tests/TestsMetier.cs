using System.Text.Json;
using BatiScan.Core.Contrats;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Murs;
using BatiScan.Core.Niveaux;
using BatiScan.Core.Scan;
using BatiScan.Core.Snap;

namespace BatiScan.Core.Tests;

public class TestsNiveaux
{
    [Fact]
    public void Association_ZeroChoisi_PasDeDoublon_MobilierIgnore()
    {
        var candidats = new[]
        {
            new CandidatNiveau("N1", -0.40, "sol", "", 1000, 30),   // cave : plus bas, mais pas le zéro
            new CandidatNiveau("N2", 0.00, "sol", "", 1000, 30),
            new CandidatNiveau("N3", 0.75, "mobilier_ou_plan_de_travail", "", 100, 0.8),
            new CandidatNiveau("N4", 2.856, "sol", "", 1000, 30),
        };
        var existants = new[] { new NiveauExistant(11, "Niveau 1", 0.0), new NiveauExistant(12, "Niveau 2", 2.85) };
        var l = AssociationNiveaux.Associer(candidats, existants, 0.0, 0.02, 120.0, "sol_fini");
        Assert.Equal(ActionNiveau.Creer, l[0].Action);
        Assert.Equal("Niveau −0,40 (SF)", l[0].NomPropose);
        Assert.Equal(ActionNiveau.AssocierExistant, l[1].Action);
        Assert.Equal(11, l[1].NiveauExistantId);
        Assert.Equal(ActionNiveau.Ignorer, l[2].Action);
        Assert.Equal(ActionNiveau.AssocierExistant, l[3].Action);
        Assert.Equal(122.856, l[3].AltitudePartageeM!.Value, 9);
        Assert.Equal(2.856, l[3].HauteurRelativeM, 9);
    }

    [Fact]
    public void NomUnique() =>
        Assert.Equal("Niveau +2,85 (2)", AssociationNiveaux.NomUnique("Niveau +2,85", new HashSet<string> { "Niveau +2,85" }));
}

public class TestsMurs
{
    private static MurMesure Mur(double pente, bool incertain = false) =>
        new("M1", new(0, 0, 0), new(5, 0, 0), 0, 2.6, 0.2, pente, new Vecteur3(0, 1, 0), incertain, "non_plombe");

    [Fact]
    public void Incline_ConserveLaBaseEtLAngle()
    {
        var m = ConversionMurNonPlombe.Convertir(Mur(Math.Tan(Math.PI / 180)), ModeMurNonPlombe.InclineFidele, ReferenceVerticale.Base, 0.2);
        Assert.Equal(1.0, m.AngleDepuisVerticaleDeg, 9);
        Assert.Equal(new Vecteur3(0, 0, 0), m.Debut);
    }

    [Fact]
    public void Vertical_MiHauteur_EcartsSymetriques()
    {
        double pente = 0.02;
        var m = ConversionMurNonPlombe.Convertir(Mur(pente), ModeMurNonPlombe.VerticalAvecRapport, ReferenceVerticale.MiHauteur, 0.2);
        Assert.Equal(0, m.AngleDepuisVerticaleDeg);
        Assert.Equal(pente * 2.6 / 2, m.Debut.Y, 9);
        Assert.Equal(-pente * 1.3, m.EcartPiedM, 9);
        Assert.Equal(pente * 1.3, m.EcartTeteM, 9);
    }

    [Fact]
    public void DeversIncertain_JamaisIncline()
    {
        var m = ConversionMurNonPlombe.Convertir(Mur(0.05, incertain: true), ModeMurNonPlombe.InclineFidele, ReferenceVerticale.Base, 0.2);
        Assert.Equal(0, m.AngleDepuisVerticaleDeg);
        Assert.Contains("incertain", m.Remarque);
    }

    [Fact]
    public void Regularisation_SousToleranceEtHorsTolerance()
    {
        var ok = new SegmentMur("A", new(0, 0, 0), new(4, 0.01, 0));        // 0,14°
        var ko = new SegmentMur("B", new(0, 0, 0), new(4, 0.40, 0));        // 5,7°
        var r = RegularisationMurs.Orthogonaliser([ok, ko], 0, 1.0, 0.02);
        Assert.True(r[0].DansTolerance);
        Assert.Equal(0, r[0].Regularise.Fin.Y - r[0].Regularise.Debut.Y, 9);
        Assert.False(r[1].DansTolerance);
        Assert.Equal(ko, r[1].Regularise); // conservé tel que mesuré
        Assert.Equal(ko, r[1].Mesure);
    }

    [Fact]
    public void Doublon_SensInverse() =>
        Assert.True(DoublonsMurs.Coincident(new(0, 0, 0), new(5, 0, 0), new(5.01, 0, 0), new(0, 0.02, 0), 0.05));
}

public class TestsSnap
{
    [Fact]
    public void PlusProche_EtHorsZone_EtAucunPointValide()
    {
        var pts = new List<Vecteur3>();
        for (int i = 0; i < 100; i++) for (int j = 0; j < 100; j++) pts.Add(new(150_000 + i * 0.01, 160_000 + j * 0.01, 1.0));
        var g = new GrilleSpatiale(pts, 0.05, "test");
        var c = g.PlusProche3D(new(150_000.503, 160_000.497, 1.002), 0.05);
        Assert.NotNull(c);
        Assert.True(c!.Value.Point.Distance(new(150_000.50, 160_000.50, 1.0)) < 1e-3); // précision float32 relative conservée
        Assert.Null(g.PlusProche3D(new(150_010, 160_000, 1), 0.05));                    // « aucun point valide »
        Assert.Null(g.PlusProcheEnPlan(new(150_000.5, 160_000.5, 0), 0.05, 2.0, 3.0)); // tranche sans points
        Assert.NotNull(g.PlusProcheEnPlan(new(150_000.5, 160_000.5, 0), 0.05, 0.5, 1.5));
    }

    [Fact]
    public void Latence_MedianeP95_PremierAccesSepare()
    {
        var s = new StatistiquesLatence();
        s.Ajouter(500);
        foreach (var v in Enumerable.Range(1, 100)) s.Ajouter(v);
        Assert.Equal(500, s.PremierAccesMs);
        Assert.Equal(50.5, s.Mediane!.Value, 6);
        Assert.Equal(95.05, s.P95!.Value, 6);
    }
}

public class TestsRevisionScan
{
    private static readonly Vecteur3[] Controles = [new(0, 0, 0), new(8, 0, 0.1), new(8, 6, 2.5), new(0, 6, 0.3)];

    private static RevisionScan Rev(string chemin, string sha) => new()
    {
        Empreinte = new EmpreinteSource(chemin, true, 10, DateTime.UtcNow, sha, 2, 100),
        TransformationInstance = TransformationRigide.Identite.VersMatrice4x4(),
    };

    [Fact]
    public void Classer_CheminSeul_Contenu_Transformation()
    {
        var prec = Rev(@"C:\scans\maison.rcp", "abc");
        var id = TransformationRigide.Identite.VersMatrice4x4();
        Assert.Equal(TypeChangementScan.CheminSeul, AnalyseRevisionScan.Classer(prec, prec.Empreinte with { Chemin = @"D:\autre\maison_v1.rcp" }, id));
        Assert.Equal(TypeChangementScan.ContenuModifie, AnalyseRevisionScan.Classer(prec, prec.Empreinte with { Sha256 = "def" }, id));
        Assert.Equal(TypeChangementScan.TransformationModifiee,
            AnalyseRevisionScan.Classer(prec, prec.Empreinte, TransformationRigide.Translater(new(0.5, 0, 0)).VersMatrice4x4()));
        Assert.Equal(TypeChangementScan.FichierAbsent, AnalyseRevisionScan.Classer(prec, prec.Empreinte with { Existe = false }, id));
    }

    [Fact]
    public void J08_RecalageRigide_CompensationRetrouveLaPositionModele()
    {
        var instance = TransformationRigide.RotationZ(0.3, Vecteur3.Zero).Apres(TransformationRigide.Translater(new(10, 20, 0)));
        var ancienVersNouveau = TransformationRigide.RotationZ(0.02, new Vecteur3(4, 3, 0)).Apres(TransformationRigide.Translater(new(0.15, -0.08, 0.01)));
        var nouveaux = Controles.Select(ancienVersNouveau.Appliquer).ToArray();
        var c = AnalyseRevisionScan.ControlerRepere(Controles, nouveaux, 0.005, 0.01);
        Assert.Equal(VerdictRepere.RepereModifieRigide, c.Verdict);
        var compensee = AnalyseRevisionScan.TransformationCompensee(instance, c.Ajustement!.Transformation);
        for (int i = 0; i < Controles.Length; i++)
            Assert.True(compensee.Appliquer(nouveaux[i]).Distance(instance.Appliquer(Controles[i])) <= 0.001); // cible ≤ 1 mm
    }

    [Fact]
    public void J08_ResidusNonUniformes_RefusCorrectionGlobale()
    {
        var nouveaux = Controles.ToArray();
        nouveaux[2] += new Vecteur3(0.05, 0, 0); // une station recalée différemment
        var c = AnalyseRevisionScan.ControlerRepere(Controles, nouveaux, 0.005, 0.01);
        Assert.Equal(VerdictRepere.NonRigide, c.Verdict);
    }

    [Fact]
    public void MemeRepere_EtIndetermine()
    {
        Assert.Equal(VerdictRepere.MemeRepere, AnalyseRevisionScan.ControlerRepere(Controles, Controles, 0.005, 0.01).Verdict);
        Assert.Equal(VerdictRepere.Indetermine, AnalyseRevisionScan.ControlerRepere(Controles[..2], Controles[..2], 0.005, 0.01).Verdict);
    }

    [Fact]
    public void Empreinte_FichierDeplace_MemeIdentite()
    {
        string d = Path.Combine(Path.GetTempPath(), "batiscan-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        try
        {
            string a = Path.Combine(d, "a.rcp"), b = Path.Combine(d, "b.rcp");
            File.WriteAllBytes(a, [1, 2, 3, 4]);
            File.Copy(a, b);
            Assert.Equal(AnalyseRevisionScan.Calculer(a).IdentiteContenu, AnalyseRevisionScan.Calculer(b).IdentiteContenu);
            Assert.False(AnalyseRevisionScan.Calculer(Path.Combine(d, "absent.rcp")).Existe);
        }
        finally { Directory.Delete(d, true); }
    }
}

public class TestsContrat
{
    [Fact]
    public void Requete_SerialiseLesChampsDuContrat()
    {
        var r = new RequeteMoteur
        {
            Operation = "niveaux",
            Repere = new Repere(),
            Source = new SourceNuage { Id = "1", Chemin = "x.rcp", Empreinte = "sha256:0" },
            Points = new SpecPoints { Chemin = "points.bin" },
        };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(r, Contrat.Json));
        var root = doc.RootElement;
        Assert.Equal("1.0.0", root.GetProperty("contract_version").GetString());
        Assert.Equal("m", root.GetProperty("unites").GetProperty("longueur").GetString());
        Assert.Equal("revit_interne_m", root.GetProperty("repere").GetProperty("nom").GetString());
        Assert.False(root.TryGetProperty("zone", out _));
    }

    [Fact]
    public void Reponse_LirePropositionsTypees()
    {
        const string json = """
        {"contract_version":"1.0.0","id_requete":"a","operation":"murs","statut":"ok",
         "nombre_points":{"recus":1,"dans_zone":1,"traites":1},"couverture":{"methode":"g","ratio":0.5},
         "propositions":[{"id":"M001","type":"mur","debut":[0,0],"fin":[1,0],"z_bas":0,"z_haut":2.5,"epaisseur_m":null,
           "epaisseur_mesuree":false,"devers":{"incertain":true}},{"id":"N1","type":"niveau","altitude_m":1.0}],
         "avertissements":[],"erreurs":[],"duree_s":0.1}
        """;
        var rep = JsonSerializer.Deserialize<ReponseMoteur>(json, Contrat.Json)!;
        var murs = rep.Lire<PropMur>("mur");
        Assert.Single(murs);
        Assert.Null(murs[0].EpaisseurM);
        Assert.Single(rep.Lire<PropNiveau>("niveau"));
    }
}
