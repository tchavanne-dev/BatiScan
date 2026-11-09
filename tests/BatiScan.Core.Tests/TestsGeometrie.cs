using BatiScan.Core.Geometrie;

namespace BatiScan.Core.Tests;

/// <summary>J01 – transformations, conversions, orientation (cibles : aller-retour ≤ 0,1 mm, orientation ≤ 0,1°).</summary>
public class TestsGeometrie
{
    private static readonly Vecteur3[] Carre =
    [
        new(0, 0, 0), new(10, 0, 0.2), new(10, 8, 0.1), new(0, 8, 3.0), new(5, 4, 6.0),
    ];

    [Fact]
    public void AllerRetour_TransformationEtInverse_SousDixiemeDeMillimetre()
    {
        var t = TransformationRigide.RotationZ(0.7, new Vecteur3(152345.12, 167890.55, 120.3))
            .Apres(TransformationRigide.Translater(new Vecteur3(-12.5, 3.25, 1.1)));
        foreach (var p in Carre)
        {
            var q = p + new Vecteur3(152_000, 167_000, 100);
            Assert.True(t.Inverse().Appliquer(t.Appliquer(q)).Distance(q) <= 1e-4);
        }
    }

    [Fact]
    public void Matrice4x4_AllerRetour()
    {
        var t = TransformationRigide.RotationZ(-1.2, new Vecteur3(3, 4, 0));
        var t2 = TransformationRigide.DepuisMatrice4x4(t.VersMatrice4x4());
        Assert.True(t.Inverse().Apres(t2).EstIdentite(1e-9, 1e-7));
    }

    [Fact]
    public void Ajustement4Ddl_RetrouveRotationEtTranslation()
    {
        var vraie = TransformationRigide.RotationZ(23.0 * Math.PI / 180, Vecteur3.Zero)
            .Apres(TransformationRigide.Translater(new Vecteur3(100, -50, 2)));
        var cible = Carre.Select(vraie.Appliquer).ToArray();
        var aj = AjustementRigide.Ajuster4Ddl(Carre, cible);
        Assert.True(aj.MaxM < 1e-9);
        Assert.Equal(23.0, aj.Transformation.AngleEnPlanDeg(), 6);
    }

    [Fact]
    public void Ajustement6Ddl_Horn_RetrouveRotation3D()
    {
        // rotation inclinée (0,3° autour de X) composée avec rotation en plan
        double a = 0.3 * Math.PI / 180;
        var rx = new TransformationRigide([1, 0, 0, 0, Math.Cos(a), -Math.Sin(a), 0, Math.Sin(a), Math.Cos(a)], new Vecteur3(1, 2, 3));
        var vraie = rx.Apres(TransformationRigide.RotationZ(0.4, Vecteur3.Zero));
        var cible = Carre.Select(vraie.Appliquer).ToArray();
        var aj = AjustementRigide.Ajuster6Ddl(Carre, cible);
        Assert.True(aj.MaxM < 1e-8, $"max {aj.MaxM}");
        Assert.True(aj.Transformation.InclinaisonVerticaleDeg() > 0.29);
    }

    [Fact]
    public void Ajustement_PointsAlignes_Refuse()
    {
        Vecteur3[] alignes = [new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)];
        Assert.Throws<InvalidOperationException>(() => AjustementRigide.Ajuster6Ddl(alignes, alignes));
    }

    [Fact]
    public void Residus_SignalentUnPointFaux()
    {
        var cible = Carre.ToArray();
        cible[2] += new Vecteur3(0.03, 0, 0);
        var aj = AjustementRigide.Ajuster4Ddl(Carre, cible);
        Assert.Equal(2, aj.IndicePireResidu);
        Assert.True(aj.MaxM > 0.015);
    }

    [Fact]
    public void Orientation_DeuxPoints_Precision()
    {
        double angle = 17.3;
        var p1 = new Vecteur3(10, 5, 0);
        var p2 = p1 + new Vecteur3(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180), 0) * 6;
        var o = Orientation.DepuisDeuxPoints(p1, p2, 2.0);
        Assert.True(Math.Abs(o.AngleAxeDeg - angle) <= 0.1);
        Assert.Equal(-17.3, o.RotationProposeeDeg, 6);
        Assert.Equal("", o.Avertissement);
        Assert.NotEqual("", Orientation.DepuisDeuxPoints(p1, p1 + new Vecteur3(0.5, 0, 0), 2.0).Avertissement);
        Assert.True(Orientation.IncertitudeDeg(6, 0.002) < 0.1);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(80, -10)]
    [InlineData(-30, -30)]
    [InlineData(135, 45)]
    public void RamenerQuart(double entree, double attendu) => Assert.Equal(attendu, Orientation.RamenerQuart(entree), 9);

    [Fact]
    public void Projection_SurPlanHorizontal_EtRefusDirectionRasante()
    {
        var p = new Vecteur3(1, 2, 3);
        var proj = Projection.SurPlan(p, Plan3.Horizontal(0), Vecteur3.AxeZ);
        Assert.Equal(new Vecteur3(1, 2, 0), proj);
        Assert.Equal(new Vecteur3(1, 2, 3), p); // source inchangée
        Assert.Throws<InvalidOperationException>(() => Projection.SurPlan(p, Plan3.Horizontal(0), new Vecteur3(1, 0, 0.01)));
    }

    [Fact]
    public void DecalageEntreTraces_DeversJ07_SousLeMillimetre()
    {
        double angle = 1.5 * Math.PI / 180, z1 = 0.5, z2 = 2.0;
        double dy = Math.Tan(angle) * (z2 - z1);
        var r = Projection.DecalageEntreTraces(new(0, 0, z1), new(5, 0, z1), new(0.2, dy, z2), new(4.8, dy, z2));
        Assert.True(Math.Abs(r.DecalageM - dy) <= 0.001);
        Assert.True(Math.Abs(r.DeversDeg - 1.5) <= 0.01);
    }
}
