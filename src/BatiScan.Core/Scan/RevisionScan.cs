using System.Security.Cryptography;
using BatiScan.Core.Geometrie;

namespace BatiScan.Core.Scan;

/// <summary>Empreinte d'un fichier de nuage (E22) : contenu, pas seulement chemin.</summary>
public sealed record EmpreinteSource(string Chemin, bool Existe, long TailleOctets, DateTime ModificationUtc, string Sha256, int NombreFichiersSupport, long TailleSupportOctets)
{
    /// <summary>Identité de contenu indépendante du chemin (sert à reconnaître un fichier déplacé/renommé).</summary>
    public string IdentiteContenu => $"{Sha256}|{TailleSupportOctets}|{NombreFichiersSupport}";
}

/// <summary>Point de contrôle mémorisé dans le repère LOCAL du nuage (indépendant de son placement).</summary>
public sealed record PointControle(string Nom, Vecteur3 LocalNuageM);

/// <summary>Révision enregistrée d'une source de nuage.</summary>
public sealed class RevisionScan
{
    public int Numero { get; set; }
    public required EmpreinteSource Empreinte { get; set; }
    /// <summary>Transformation de l'instance (local nuage → interne Revit), matrice 4×4 en mètres.</summary>
    public required double[] TransformationInstance { get; set; }
    public List<PointControle> PointsControle { get; set; } = [];
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public string? RevisionPrecedente { get; set; }
    public string Decision { get; set; } = "";
}

public enum TypeChangementScan
{
    PremiereRevision,
    Identique,
    /// <summary>Même contenu, chemin différent : réparation du lien, calage conservé.</summary>
    CheminSeul,
    /// <summary>Contenu différent (ré-export ReCap) : contrôles à refaire, caches invalidés.</summary>
    ContenuModifie,
    /// <summary>Transformation de l'instance modifiée hors BatiScan.</summary>
    TransformationModifiee,
    FichierAbsent,
}

public enum VerdictRepere
{
    /// <summary>Points de contrôle retrouvés au même endroit : repère inchangé.</summary>
    MemeRepere,
    /// <summary>Repère modifié de façon rigide : correction globale proposable.</summary>
    RepereModifieRigide,
    /// <summary>Résidus incompatibles avec une transformation globale : vérification par zones requise.</summary>
    NonRigide,
    /// <summary>Pas assez de points de contrôle pour conclure.</summary>
    Indetermine,
}

public sealed record ControleRepere(VerdictRepere Verdict, ResultatAjustement? Ajustement, string Explication);

/// <summary>
/// E22 – Analyse d'une actualisation de scan (ré-export complet ReCap).
/// Distingue chemin / contenu / repère ; ne déclenche jamais de mise à jour
/// des niveaux ou murs : toute correction est un lot séparé à approuver.
/// </summary>
public static class AnalyseRevisionScan
{
    // ── Empreinte ───────────────────────────────────────────────────────────

    /// <summary>
    /// Calcule l'empreinte d'un fichier. Pour un projet ReCap (.rcp), inclut la liste des
    /// fichiers .rcs du dossier « &lt;nom&gt; Support » s'il existe. Lecture seule (E17).
    /// </summary>
    public static EmpreinteSource Calculer(string chemin)
    {
        if (!File.Exists(chemin))
            return new EmpreinteSource(chemin, false, 0, DateTime.MinValue, "", 0, 0);
        var fi = new FileInfo(chemin);
        string sha;
        using (var flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
            sha = Convert.ToHexString(SHA256.HashData(flux)).ToLowerInvariant();
        int nb = 0; long taille = 0;
        string support = Path.Combine(fi.DirectoryName ?? "", Path.GetFileNameWithoutExtension(chemin) + " Support");
        if (Directory.Exists(support))
            foreach (var f in Directory.EnumerateFiles(support, "*.rcs", SearchOption.AllDirectories))
            {
                nb++;
                taille += new FileInfo(f).Length;
            }
        return new EmpreinteSource(chemin, true, fi.Length, fi.LastWriteTimeUtc, sha, nb, taille);
    }

    // ── Classification ──────────────────────────────────────────────────────

    public static TypeChangementScan Classer(RevisionScan? precedente, EmpreinteSource actuelle, double[] transformationActuelle, double toleranceM = 1e-6)
    {
        if (!actuelle.Existe) return TypeChangementScan.FichierAbsent;
        if (precedente is null) return TypeChangementScan.PremiereRevision;
        bool memeContenu = precedente.Empreinte.IdentiteContenu == actuelle.IdentiteContenu;
        bool memeChemin = string.Equals(Path.GetFullPath(precedente.Empreinte.Chemin), Path.GetFullPath(actuelle.Chemin), StringComparison.OrdinalIgnoreCase);
        if (!memeContenu) return TypeChangementScan.ContenuModifie;
        var tPrec = TransformationRigide.DepuisMatrice4x4(precedente.TransformationInstance);
        var tAct = TransformationRigide.DepuisMatrice4x4(transformationActuelle);
        if (!tPrec.Inverse().Apres(tAct).EstIdentite(toleranceM, 1e-6)) return TypeChangementScan.TransformationModifiee;
        return memeChemin ? TypeChangementScan.Identique : TypeChangementScan.CheminSeul;
    }

    // ── Contrôle du repère ──────────────────────────────────────────────────

    /// <summary>
    /// Compare les points de contrôle de l'ancienne révision (repère local ancien) à leur
    /// position re-mesurée sur le nouveau nuage (repère local nouveau).
    /// <para>
    /// L'ajustement 6 DDL ancien → nouveau sert de diagnostic ; s'il est rigide, la correction
    /// à appliquer à l'instance est T_instance_nouvelle = T_instance_ancienne ∘ T⁻¹ (le
    /// nouveau nuage revient alors exactement à l'ancienne position modèle). La maquette n'est
    /// jamais déplacée.
    /// </para>
    /// </summary>
    public static ControleRepere ControlerRepere(
        IReadOnlyList<Vecteur3> anciensLocaux, IReadOnlyList<Vecteur3> nouveauxLocaux, double toleranceMemeRepereM, double toleranceResiduM)
    {
        if (anciensLocaux.Count < 3)
            return new ControleRepere(VerdictRepere.Indetermine, null,
                $"{anciensLocaux.Count} point(s) de contrôle : au moins 3 points non alignés sont nécessaires pour conclure.");
        // Même repère ? Contrôle direct sans ajustement.
        var identite = AjustementRigide.Evaluer(TransformationRigide.Identite, anciensLocaux, nouveauxLocaux, "controle_identite");
        if (identite.MaxM <= toleranceMemeRepereM)
            return new ControleRepere(VerdictRepere.MemeRepere, identite,
                $"Points de contrôle retrouvés à {identite.MaxM * 1000:F1} mm max (≤ {toleranceMemeRepereM * 1000:F1} mm) : repère inchangé.");
        ResultatAjustement aj;
        try { aj = AjustementRigide.Ajuster6Ddl(anciensLocaux, nouveauxLocaux); }
        catch (InvalidOperationException e) { return new ControleRepere(VerdictRepere.Indetermine, null, e.Message); }
        if (aj.MaxM > toleranceResiduM)
            return new ControleRepere(VerdictRepere.NonRigide, aj,
                $"Résidu max {aj.MaxM * 1000:F1} mm (point n° {aj.IndicePireResidu + 1}) > {toleranceResiduM * 1000:F1} mm : "
                + "une transformation globale unique n'explique pas le nouveau recalage. Vérifier les zones affectées ; aucune correction globale n'est proposée.");
        return new ControleRepere(VerdictRepere.RepereModifieRigide, aj,
            $"Nouveau repère rigide : rotation {aj.Transformation.AngleRotationDeg():F4}°, translation {aj.Transformation.Translation.Norme * 1000:F1} mm, "
            + $"résidus rms {aj.RmsM * 1000:F1} mm / max {aj.MaxM * 1000:F1} mm.");
    }

    /// <summary>Transformation d'instance compensant un nouveau repère rigide (voir <see cref="ControlerRepere"/>).</summary>
    public static TransformationRigide TransformationCompensee(TransformationRigide instanceAncienne, TransformationRigide ancienVersNouveau) =>
        instanceAncienne.Apres(ancienVersNouveau.Inverse());
}
