using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.UI;
using BatiScan.Core.Contrats;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;
using BatiScan.Core.Rapports;
using BatiScan.Core.Scan;
using BatiScan.Infrastructure;
using BatiScan.Interface;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BatiScan.Commandes;

// ════════════════════════════════════════════════════════════════════════════
//  E02 – Calage du nuage par points de contrôle
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Ligne de résidu présentée avant application (E02 « Calage connu »).</summary>
public sealed class LigneResidu
{
    public string Nom { get; init; } = "";
    public string Mesure { get; init; } = "";
    public string Cible { get; init; } = "";
    public double ResiduMm { get; init; }
    public string Statut { get; init; } = "";
}

/// <summary>
/// E02 – Calage : ajustement 4 DDL (rotation autour de Z + translation) des points de contrôle
/// accrochés sur le nuage vers leurs coordonnées de référence (fichier CSV en coordonnées
/// PARTAGÉES). Seul le NUAGE est déplacé, jamais les coordonnées partagées ni la maquette.
/// Résidus et sens de la transformation affichés avant application ; contrôle après application,
/// rollback si la vérification échoue.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeCalage : CommandeBase
{
    protected override string Titre => "BatiScan – Calage du nuage (E02)";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var repere = RepereProjet.Lire(ctx.Doc);
        if (!repere.Confirme)
            throw new ArretCommande($"Relation interne ↔ partagé non confirmée : calage impossible sans risque.\n\n{repere.Decrire()}");
        var etat = ctx.Etat();
        var rev = etat.DerniereRevision(nuage.Cle);
        if (rev is null || rev.PointsControle.Count < 2)
            throw new ArretCommande("Au moins 2 points de contrôle accrochés sur ce nuage sont nécessaires (commande « Accrochage », option « Enregistrer comme point de contrôle »).");

        string? csv = Dialogues.OuvrirFichier("Coordonnées de référence (Nom;X;Y;Z en mètres, coordonnées PARTAGÉES)", "CSV (*.csv;*.txt)|*.csv;*.txt");
        if (csv is null) return Result.Cancelled;
        var cibles = LireCsv(csv);
        var appariés = rev.PointsControle.Where(p => cibles.ContainsKey(p.Nom)).ToList();
        if (appariés.Count < 2)
            throw new ArretCommande($"{appariés.Count} point(s) communs entre le nuage et le fichier (noms identiques requis).");

        // Mesuré : local nuage → interne (transformation actuelle). Cible : partagé → interne.
        var mesures = appariés.Select(p => nuage.TransformationInstance.Appliquer(p.LocalNuageM)).ToList();
        var ciblesInternes = appariés.Select(p => repere.DepuisPartage(cibles[p.Nom])).ToList();
        var aj = AjustementRigide.Ajuster4Ddl(mesures, ciblesInternes);
        double tol = ctx.Parametres.ToleranceResiduCalageM;
        var lignes = appariés.Select((p, i) => new LigneResidu
        {
            Nom = p.Nom,
            Mesure = ConversionRevit.P(repere.VersPartage(mesures[i])),
            Cible = ConversionRevit.P(cibles[p.Nom]),
            ResiduMm = Math.Round(aj.Residus[i] * 1000, 1),
            Statut = aj.Residus[i] <= tol ? "dans la tolérance" : "HORS TOLÉRANCE",
        }).ToList();
        string resume =
            $"Sens : nuage (mesuré) → coordonnées de référence. Méthode {aj.Methode}.\n"
            + $"Rotation en plan : {aj.Transformation.AngleEnPlanDeg():F5}° ; translation : {ConversionRevit.P(aj.Transformation.Translation)} ({aj.Transformation.Translation.Norme * 1000:F1} mm)\n"
            + $"Résidus : rms {aj.RmsM * 1000:F1} mm, max {aj.MaxM * 1000:F1} mm (tolérance {tol * 1000:F1} mm, à valider D07).\n"
            + "Élément déplacé : UNIQUEMENT l'instance du nuage. Coordonnées partagées, point de base et maquette inchangés.";
        var cols = new[]
        {
            new Colonne("Point", nameof(LigneResidu.Nom)), new Colonne("Mesuré (partagé)", nameof(LigneResidu.Mesure)),
            new Colonne("Référence (partagé)", nameof(LigneResidu.Cible)), new Colonne("Résidu (mm)", nameof(LigneResidu.ResiduMm)),
            new Colonne("Statut", nameof(LigneResidu.Statut)),
        };
        if (aj.MaxM > tol)
        {
            FenetreValidation.Afficher(Titre, "CONFLIT : résidus hors tolérance. Aucun calage ne sera appliqué (pas d'écrasement automatique).", lignes, cols, resume, libelleAppliquer: "Fermer");
            JournalLocal.Ecrire("calage.refuse", new { nuage = nuage.Nom, rms = aj.RmsM, max = aj.MaxM, tol }, "avertissement");
            return Result.Cancelled;
        }
        if (!FenetreValidation.Afficher(Titre, "Vérifier les résidus avant d'appliquer le calage du nuage.", lignes, cols, resume, libelleAppliquer: "Appliquer le calage du nuage"))
            return Result.Cancelled;
        ctx.VerifierEcriture("Calage du nuage");

        var attendu = aj.Transformation.Apres(nuage.TransformationInstance);
        ctx.AppliquerLot("BatiScan – Calage du nuage (E02)", () =>
        {
            DeplacerNuage(ctx.Doc, nuage.Instance, aj.Transformation);
            ctx.Doc.Regenerate();
            // Contrôle a posteriori : la transformation obtenue doit être celle annoncée (≤ 1 mm sur les contrôles).
            var obtenue = ConversionRevit.VersM(nuage.Instance.GetTransform());
            double ecart = appariés.Max(p => obtenue.Appliquer(p.LocalNuageM).Distance(attendu.Appliquer(p.LocalNuageM)));
            if (ecart > ctx.Parametres.ToleranceConservationCalageM)
                throw new InvalidOperationException($"Contrôle après déplacement : écart {ecart * 1000:F2} mm > {ctx.Parametres.ToleranceConservationCalageM * 1000:F1} mm.");
            var e = StockageBatiScan.LireEtat(ctx.Doc);
            var r = e.DerniereRevision(nuage.Cle)!;
            e.AjouterRevision(nuage.Cle, new RevisionScan
            {
                Empreinte = r.Empreinte, TransformationInstance = obtenue.VersMatrice4x4(), PointsControle = r.PointsControle,
                Decision = $"calage E02 : rms {aj.RmsM * 1000:F1} mm, max {aj.MaxM * 1000:F1} mm, fichier {Path.GetFileName(csv)}",
            });
            e.InvaliderSource(nuage.Cle, "transformation du nuage modifiée par le calage");
            StockageBatiScan.EcrireEtat(ctx.Doc, e);
            return 0;
        });
        ServiceSnap.Invalider(nuage.Cle);
        var rapport = new RapportCsv("Calage du nuage (E02)").Contexte("Nuage", nuage.Nom).Contexte("Référence", csv).Contexte("Résumé", resume)
            .Colonnes("Point", "Mesuré (partagé)", "Référence (partagé)", "Résidu (mm)", "Statut");
        foreach (var l in lignes) rapport.Ligne(l.Nom, l.Mesure, l.Cible, l.ResiduMm, l.Statut);
        string chemin = RapportCsv.CheminHorodate("calage");
        rapport.Enregistrer(chemin);
        JournalLocal.Ecrire("calage.applique", new { nuage = nuage.Nom, rms = aj.RmsM, max = aj.MaxM, transformation = aj.Transformation.VersMatrice4x4(), rapport = chemin });
        Contexte.Informer(Titre, "Calage appliqué au nuage", $"{resume}\n\nRapport : {chemin}\nAnnulation possible par Ctrl+Z (Revit).");
        return Result.Succeeded;
    }

    /// <summary>Applique p ↦ R·p + t à l'instance (rotation autour de l'axe Z interne puis translation).</summary>
    internal static void DeplacerNuage(Document doc, PointCloudInstance inst, TransformationRigide t)
    {
        if (t.InclinaisonVerticaleDeg() > 1e-6)
        {
            // Rotation 3D (E22) : on remplace directement la transformation via ElementTransformUtils impossible ;
            // on décompose en rotation autour de l'axe propre de la rotation.
            double[] r = t.Rotation;
            double angle = t.AngleRotationDeg() * Math.PI / 180;
            var axe = new XYZ(r[7] - r[5], r[2] - r[6], r[3] - r[1]);
            if (axe.GetLength() < 1e-12) throw new InvalidOperationException("Axe de rotation indéterminé.");
            ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateUnbound(XYZ.Zero, axe.Normalize()), angle);
        }
        else
        {
            double angle = t.AngleEnPlanDeg() * Math.PI / 180;
            if (Math.Abs(angle) > 1e-12)
                ElementTransformUtils.RotateElement(doc, inst.Id, Line.CreateUnbound(XYZ.Zero, XYZ.BasisZ), angle);
        }
        if (t.Translation.Norme > 1e-9)
            ElementTransformUtils.MoveElement(doc, inst.Id, ConversionRevit.VersPieds(t.Translation));
    }

    /// <summary>Lit « Nom;X;Y;Z » (séparateur ; , ou tabulation ; décimale point ou virgule si séparateur ;).</summary>
    internal static Dictionary<string, Vecteur3> LireCsv(string chemin)
    {
        var res = new Dictionary<string, Vecteur3>(StringComparer.OrdinalIgnoreCase);
        foreach (var ligneBrute in File.ReadAllLines(chemin))
        {
            string ligne = ligneBrute.Trim();
            if (ligne.Length == 0 || ligne.StartsWith('#')) continue;
            char sep = ligne.Contains(';') ? ';' : ligne.Contains('\t') ? '\t' : ',';
            var c = ligne.Split(sep).Select(s => s.Trim()).ToArray();
            if (c.Length < 4) continue;
            double V(string s) => double.Parse(sep == ',' ? s : s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
            try { res[c[0]] = new Vecteur3(V(c[1]), V(c[2]), V(c[3])); }
            catch (FormatException) { /* en-tête ignoré */ }
        }
        if (res.Count == 0) throw new ArretCommande("Aucune coordonnée lisible dans le fichier (format attendu : Nom;X;Y;Z en mètres).");
        return res;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  E03 – Orientation assistée (Nord de travail)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// E03 – Nord de travail par deux points accrochés ou par directions détectées (alternatives
/// visibles). Le Nord géographique et les coordonnées partagées ne sont JAMAIS modifiés :
/// le Nord de travail oriente les zones, tranches et régularisations BatiScan. Aucun élément
/// du modèle n'est déplacé.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeOrientation : CommandeBase
{
    protected override string Titre => "BatiScan – Orientation assistée (E03)";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var zone = ctx.ChoisirZone(nuage, "orientation");
        var d = new TaskDialog(Titre) { MainInstruction = "Définir le Nord de travail" };
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Mode assisté : axe par deux points accrochés", "Vue en plan conseillée ; points espacés d'au moins " + ctx.Parametres.DistanceMinAxeM + " m.");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Mode automatique : directions dominantes des murs (moteur)", "Plusieurs alternatives sont proposées ; aucune n'est choisie automatiquement.");
        var choix = d.Show();
        PropositionOrientation? prop;
        if (choix == TaskDialogResult.CommandLink1)
        {
            var p1 = ServiceSnap.Accrocher(ctx, nuage, zone, "BatiScan : premier point de l'axe");
            if (p1 is null || double.IsNaN(p1.EcartClicM)) throw new ArretCommande("Premier point non accroché (aucun point valide).");
            var p2 = ServiceSnap.Accrocher(ctx, nuage, zone, "BatiScan : second point de l'axe");
            if (p2 is null || double.IsNaN(p2.EcartClicM)) throw new ArretCommande("Second point non accroché (aucun point valide).");
            prop = Orientation.DepuisDeuxPoints(p1.Point, p2.Point, ctx.Parametres.DistanceMinAxeM);
        }
        else if (choix == TaskDialogResult.CommandLink2)
        {
            var (rep, ext) = ctx.ExecuterMoteur("directions", nuage, zone);
            var dirs = rep.Lire<PropDirection>("direction");
            if (dirs.Count == 0) throw new ArretCommande("Aucune direction dominante détectée.\n\n" + Contexte.ResumeCalcul(rep, ext, zone));
            var props = dirs.Select(x => Orientation.DepuisDirection(x.AngleDeg, x.PoidsRelatif)).ToList();
            prop = Dialogues.Choisir(Titre, "Directions détectées (alternatives) – choisir l'axe de référence :\n" + Contexte.ResumeCalcul(rep, ext, zone), props,
                p => $"Axe {p.AngleAxeDeg:F2}° – rotation proposée {p.RotationProposeeDeg:F2}° – {p.Origine}");
            if (prop is null) return Result.Cancelled;
        }
        else return Result.Cancelled;

        var repere = RepereProjet.Lire(ctx.Doc);
        double incert = double.IsNaN(prop.DistancePointsM) ? double.NaN : Orientation.IncertitudeDeg(prop.DistancePointsM, 0.002);
        string apercu =
            $"Axe mesuré : {prop.AngleAxeDeg:F3}° (depuis l'axe X interne, sens trigonométrique)\n"
            + $"Nord de travail proposé : {Orientation.RamenerQuart(prop.AngleAxeDeg):F3}° → rotation d'affichage {prop.RotationProposeeDeg:F3}°\n"
            + (double.IsNaN(incert) ? "" : $"Distance entre points : {prop.DistancePointsM:F2} m ; incertitude ≈ ±{incert:F3}° pour 2 mm de pointé\n")
            + $"Nord géographique du projet : {repere.AngleNordGeographiqueDeg:F4}° (NON modifié)\n"
            + (prop.Avertissement.Length > 0 ? "⚠ " + prop.Avertissement + "\n" : "")
            + "\nEffet : zones/tranches et régularisation BatiScan suivront ce Nord de travail. Aucun élément déplacé.";
        if (!Contexte.Confirmer(Titre, "Valider ce Nord de travail ?", apercu)) return Result.Cancelled;
        ctx.VerifierEcriture("Nord de travail");
        double nord = Orientation.RamenerQuart(prop.AngleAxeDeg);
        StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Nord de travail (E03)", e =>
        {
            e.NordTravailDeg = nord;
            e.NordTravailOrigine = $"{prop.Origine} le {DateTime.Now:g}";
        });
        JournalLocal.Ecrire("orientation.validee", new { nord, prop.AngleAxeDeg, prop.Origine, prop.DistancePointsM, prop.Avertissement });
        return Result.Succeeded;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  E22 – Actualisation d'un scan (ré-export complet ReCap)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Contrôle d'un point lors de l'actualisation.</summary>
public sealed class LigneControle
{
    public bool Accepter { get; set; } = true;
    public string Nom { get; init; } = "";
    public string Methode { get; init; } = "";
    public double EcartMm { get; init; }
    public string Ancien { get; init; } = "";
    public string Nouveau { get; init; } = "";
    internal Vecteur3 AncienLocal { get; init; }
    internal Vecteur3 NouveauLocal { get; init; }
}

/// <summary>
/// E22 – Actualise un scan sans casser les liaisons ni les coordonnées.
/// Distingue : chemin seul (fichier déplacé/renommé), contenu modifié (ré-export ReCap complet),
/// transformation modifiée hors BatiScan, fichier absent. Pour un nouveau contenu : caches
/// invalidés, analyses « à recalculer », points de contrôle revérifiés, puis — seulement si le
/// nouveau repère est rigide — compensation proposée sur le NUAGE. Jamais de mise à jour des
/// niveaux ou murs ; filiation des révisions conservée et restauration possible.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeActualiserScan : CommandeBase
{
    protected override string Titre => "BatiScan – Actualiser le scan (E22)";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var etat = ctx.Etat();
        var prec = etat.DerniereRevision(nuage.Cle);

        var menu = new TaskDialog(Titre) { MainInstruction = $"Nuage « {nuage.Nom} »", MainContent = $"Fichier : {nuage.Chemin} [{nuage.StatutFichier()}]\nRévisions enregistrées : {(etat.Revisions.TryGetValue(nuage.Cle, out var l) ? l.Count : 0)}" };
        menu.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Contrôler le fichier lié (ré-export ReCap, déplacement…)");
        menu.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Lier le nouvel export ReCap situé à un autre emplacement…", "Crée un nouveau lien avec le calage actuel ; l'ancien lien est conservé.");
        menu.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Historique : consulter / restaurer la transformation d'une révision");
        switch (menu.Show())
        {
            case TaskDialogResult.CommandLink1: return Controler(ctx, nuage, prec);
            case TaskDialogResult.CommandLink2: return LierNouvelExport(ctx, nuage);
            case TaskDialogResult.CommandLink3: return Historique(ctx, nuage);
            default: return Result.Cancelled;
        }
    }

    private Result Controler(Contexte ctx, NuageSource nuage, RevisionScan? prec)
    {
        var emp = FenetreProgression.Executer("BatiScan – Empreinte du fichier", (p, ct) => { p.Report((0.1, "Calcul SHA-256 (lecture seule)…")); return nuage.CalculerEmpreinte(); });
        var tAct = nuage.TransformationInstance;
        var type = AnalyseRevisionScan.Classer(prec, emp, tAct.VersMatrice4x4());
        JournalLocal.Ecrire("scan.classement", new { nuage = nuage.Nom, type = type.ToString(), emp.Sha256, emp.Chemin });
        switch (type)
        {
            case TypeChangementScan.FichierAbsent:
                throw new ArretCommande($"Fichier introuvable : {nuage.Chemin}\n\nUtiliser « Lier le nouvel export ReCap… » pour pointer vers le nouveau fichier ; le calage sera conservé et contrôlé.");
            case TypeChangementScan.PremiereRevision:
                if (!Contexte.Confirmer(Titre, "Enregistrer la révision de référence ?", $"Aucune révision enregistrée pour ce nuage.\nEmpreinte : {emp.Sha256[..16]}…, {emp.NombreFichiersSupport} fichier(s) .rcs.\n\nConseil : accrocher ensuite au moins 3 points de contrôle non alignés (commande Accrochage) pour pouvoir contrôler les futurs ré-exports."))
                    return Result.Cancelled;
                Enregistrer(ctx, nuage, emp, tAct, [], "révision de référence");
                return Result.Succeeded;
            case TypeChangementScan.Identique:
                Contexte.Informer(Titre, "Aucun changement", "Même fichier, même contenu, même transformation : calage inchangé.");
                return Result.Succeeded;
            case TypeChangementScan.CheminSeul:
                if (!Contexte.Confirmer(Titre, "Fichier identique déplacé ou renommé", $"Contenu identique (SHA-256), chemin : {prec!.Empreinte.Chemin} → {emp.Chemin}.\nLe calage est conservé (transformation inchangée, contrôlée).\n\nEnregistrer ce nouveau chemin ?"))
                    return Result.Cancelled;
                Enregistrer(ctx, nuage, emp, tAct, prec.PointsControle, "chemin seul : calage conservé");
                return Result.Succeeded;
            case TypeChangementScan.TransformationModifiee:
            {
                var delta = TransformationRigide.DepuisMatrice4x4(prec!.TransformationInstance).Inverse().Apres(tAct);
                var d = new TaskDialog(Titre)
                {
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                    MainInstruction = "Le nuage a été déplacé hors BatiScan",
                    MainContent = $"Écart à la dernière révision : rotation {delta.AngleRotationDeg():F4}°, translation {delta.Translation.Norme * 1000:F1} mm.\nAucune correction automatique. Les zones et analyses liées seront marquées à recalculer si vous acceptez la nouvelle position.",
                };
                d.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Accepter la nouvelle position comme référence");
                d.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Restaurer la position de la dernière révision");
                var c = d.Show();
                if (c == TaskDialogResult.CommandLink1) { Enregistrer(ctx, nuage, emp, tAct, prec.PointsControle, "position modifiée hors BatiScan acceptée", invalider: true); return Result.Succeeded; }
                if (c == TaskDialogResult.CommandLink2) { Restaurer(ctx, nuage, prec); return Result.Succeeded; }
                return Result.Cancelled;
            }
            case TypeChangementScan.ContenuModifie:
                return ContenuModifie(ctx, nuage, prec!, emp);
        }
        return Result.Cancelled;
    }

    /// <summary>Ré-export ReCap complet : invalidation, re-mesure des contrôles, verdict de repère.</summary>
    private Result ContenuModifie(Contexte ctx, NuageSource nuage, RevisionScan prec, EmpreinteSource emp)
    {
        ctx.VerifierEcriture("Actualisation du scan");
        // 1. Invalidation immédiate (caches et analyses) : un résultat ancien n'est jamais présenté comme actuel.
        ServiceSnap.Invalider(nuage.Cle);
        int invalides = 0;
        StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Invalidation après ré-export (E22)", e => invalides = e.InvaliderSource(nuage.Cle, $"contenu du scan modifié le {DateTime.Now:g}"));

        if (prec.PointsControle.Count == 0)
        {
            Enregistrer(ctx, nuage, emp, nuage.TransformationInstance, [], "contenu modifié, aucun point de contrôle : repère NON vérifié");
            Contexte.Informer(Titre, "Contenu modifié – repère non vérifiable",
                $"{invalides} zone(s)/analyse(s) marquée(s) à recalculer.\nAucun point de contrôle : impossible de vérifier que le nouvel export est dans le même repère. Contrôler visuellement et accrocher des points de contrôle.");
            return Result.Succeeded;
        }

        // 2. Re-mesure des points de contrôle sur le NOUVEAU contenu (placement actuel conservé).
        var t = nuage.TransformationInstance;
        var tInv = t.Inverse();
        var lignes = new List<LigneControle>();
        bool manuel = Contexte.Confirmer(Titre, "Re-mesure des points de contrôle",
            "Re-mesurer MANUELLEMENT chaque point (clic dans la vue active) ?\n\nOui : recommandé si le recalage ReCap a pu déplacer le nuage de plus de quelques centimètres.\nNon : appariement automatique au point le plus proche (rayon " + ctx.Parametres.RayonRechercheControleM * 1000 + " mm) – indicatif seulement.");
        foreach (var pc in prec.PointsControle)
        {
            var attenduModele = t.Appliquer(pc.LocalNuageM);
            Vecteur3? nouveau = null;
            string methode;
            if (manuel)
            {
                double rr = Math.Max(ctx.Parametres.RayonRechercheControleM * 5, 0.5);
                var zone = new BoiteZone($"Contrôle {pc.Nom}", attenduModele - new Vecteur3(rr, rr, rr), attenduModele + new Vecteur3(rr, rr, rr), 0);
                var r = ServiceSnap.Accrocher(ctx, nuage, zone, $"BatiScan : cliquer le point de contrôle « {pc.Nom} » sur le nouveau scan (Échap = ignorer)");
                ServiceSnap.Invalider(nuage.Cle);
                if (r is not null && !double.IsNaN(r.EcartClicM)) nouveau = r.Point;
                methode = "manuel";
            }
            else
            {
                double rr = ctx.Parametres.RayonRechercheControleM;
                var zone = new BoiteZone("auto", attenduModele - new Vecteur3(rr, rr, rr), attenduModele + new Vecteur3(rr, rr, rr), 0);
                var pts = ExtractionPoints.Extraire(nuage, zone, 0.001, 200_000).Points;
                if (pts.Count > 0) nouveau = pts.MinBy(p => p.Distance(attenduModele));
                methode = "plus proche point (indicatif)";
            }
            if (nouveau is null) continue;
            var nl = tInv.Appliquer(nouveau.Value);
            lignes.Add(new LigneControle
            {
                Nom = pc.Nom, Methode = methode, EcartMm = Math.Round(nl.Distance(pc.LocalNuageM) * 1000, 1),
                Ancien = ConversionRevit.P(attenduModele), Nouveau = ConversionRevit.P(nouveau.Value),
                AncienLocal = pc.LocalNuageM, NouveauLocal = nl,
            });
        }
        if (lignes.Count == 0) throw new ArretCommande("Aucun point de contrôle retrouvé sur le nouveau scan : repère non vérifiable, aucune correction proposée.");
        if (!FenetreValidation.Afficher(Titre, "Points de contrôle re-mesurés : décocher les appariements douteux.", lignes,
                [new Colonne("Utiliser", nameof(LigneControle.Accepter), true, TypeColonne.Case), new Colonne("Point", nameof(LigneControle.Nom)),
                 new Colonne("Méthode", nameof(LigneControle.Methode)), new Colonne("Écart (mm)", nameof(LigneControle.EcartMm)),
                 new Colonne("Ancienne position", nameof(LigneControle.Ancien)), new Colonne("Nouvelle position", nameof(LigneControle.Nouveau))],
                libelleAppliquer: "Analyser le repère"))
            return Result.Cancelled;
        var retenus = lignes.Where(x => x.Accepter).ToList();

        // 3. Verdict de repère.
        var verdict = AnalyseRevisionScan.ControlerRepere(retenus.Select(x => x.AncienLocal).ToList(), retenus.Select(x => x.NouveauLocal).ToList(),
            ctx.Parametres.ToleranceMemeRepereM, ctx.Parametres.ToleranceResiduCalageM);
        var nouveauxControles = retenus.Select(x => new PointControle(x.Nom, x.NouveauLocal)).ToList();
        var rapport = new RapportCsv("Actualisation de scan (E22)").Contexte("Nuage", nuage.Nom).Contexte("Ancien SHA-256", prec.Empreinte.Sha256)
            .Contexte("Nouveau SHA-256", emp.Sha256).Contexte("Verdict", verdict.Verdict.ToString()).Contexte("Explication", verdict.Explication)
            .Colonnes("Point", "Méthode", "Écart (mm)", "Ancienne position", "Nouvelle position", "Résidu après ajustement (mm)");
        for (int i = 0; i < retenus.Count; i++)
            rapport.Ligne(retenus[i].Nom, retenus[i].Methode, retenus[i].EcartMm, retenus[i].Ancien, retenus[i].Nouveau,
                verdict.Ajustement is null ? null : verdict.Ajustement.Residus[i] * 1000);
        string chemin = RapportCsv.CheminHorodate("actualisation-scan");
        rapport.Enregistrer(chemin);

        switch (verdict.Verdict)
        {
            case VerdictRepere.MemeRepere:
                Enregistrer(ctx, nuage, emp, nuage.TransformationInstance, nouveauxControles, "ré-export dans le même repère : " + verdict.Explication);
                Contexte.Informer(Titre, "Même repère", $"{verdict.Explication}\n{invalides} élément(s) à recalculer.\nRapport : {chemin}");
                return Result.Succeeded;
            case VerdictRepere.RepereModifieRigide:
            {
                var tNouvelle = AnalyseRevisionScan.TransformationCompensee(nuage.TransformationInstance, verdict.Ajustement!.Transformation);
                var deplacement = tNouvelle.Apres(nuage.TransformationInstance.Inverse());
                string texte = $"{verdict.Explication}\n\nProposition : déplacer le NUAGE (lien) pour le ramener sur l'ancienne position modèle.\n"
                             + $"Déplacement du lien : rotation {deplacement.AngleRotationDeg():F4}°, translation {deplacement.Translation.Norme * 1000:F1} mm.\n"
                             + "La maquette (niveaux, murs…) n'est PAS déplacée. Toute mise à jour de la maquette serait un lot séparé.\n\nAppliquer ?";
                if (!Contexte.Confirmer(Titre, "Nouveau repère rigide détecté", texte))
                {
                    Enregistrer(ctx, nuage, emp, nuage.TransformationInstance, nouveauxControles, "repère modifié, compensation REFUSÉE par l'utilisateur");
                    return Result.Succeeded;
                }
                ctx.AppliquerLot("BatiScan – Compensation du recalage ReCap (E22)", () =>
                {
                    CommandeCalage.DeplacerNuage(ctx.Doc, nuage.Instance, deplacement);
                    ctx.Doc.Regenerate();
                    var obtenue = ConversionRevit.VersM(nuage.Instance.GetTransform());
                    double ecart = retenus.Max(x => obtenue.Appliquer(x.NouveauLocal).Distance(nuage.TransformationInstance.Appliquer(x.AncienLocal)));
                    if (ecart > ctx.Parametres.ToleranceResiduCalageM)
                        throw new InvalidOperationException($"Contrôle après compensation : écart {ecart * 1000:F1} mm.");
                    var e = StockageBatiScan.LireEtat(ctx.Doc);
                    e.AjouterRevision(nuage.Cle, new RevisionScan { Empreinte = emp, TransformationInstance = obtenue.VersMatrice4x4(), PointsControle = nouveauxControles, Decision = "compensation rigide appliquée : " + verdict.Explication });
                    StockageBatiScan.EcrireEtat(ctx.Doc, e);
                    return 0;
                });
                ServiceSnap.Invalider(nuage.Cle);
                Contexte.Informer(Titre, "Compensation appliquée au nuage", $"Rapport : {chemin}\nAnnulation : Ctrl+Z ou « Historique ».");
                return Result.Succeeded;
            }
            case VerdictRepere.NonRigide:
                Enregistrer(ctx, nuage, emp, nuage.TransformationInstance, nouveauxControles, "RECALAGE NON RIGIDE : aucune correction globale ; " + verdict.Explication);
                Contexte.Informer(Titre, "Recalage non rigide entre stations", $"{verdict.Explication}\n\nAucune rotation/translation unique n'est appliquée. Vérifier les zones affectées.\nRapport : {chemin}");
                return Result.Succeeded;
            default:
                Enregistrer(ctx, nuage, emp, nuage.TransformationInstance, nouveauxControles, "repère indéterminé : " + verdict.Explication);
                Contexte.Informer(Titre, "Repère indéterminé", verdict.Explication + $"\nRapport : {chemin}");
                return Result.Succeeded;
        }
    }

    /// <summary>Nouvel export à un autre chemin : nouveau type + instance avec la transformation actuelle ; ancien lien conservé.</summary>
    private Result LierNouvelExport(Contexte ctx, NuageSource ancien)
    {
        string? chemin = Dialogues.OuvrirFichier("Nouvel export ReCap", "Projet ReCap (*.rcp)|*.rcp|Scan ReCap (*.rcs)|*.rcs");
        if (chemin is null) return Result.Cancelled;
        string moteur = Path.GetExtension(chemin).TrimStart('.').ToLowerInvariant();
        var moteurs = PointCloudEngineRegistry.GetSupportedEngines();
        if (!moteurs.Contains(moteur, StringComparer.OrdinalIgnoreCase))
            throw new ArretCommande($"Format « {moteur} » non pris en charge par Revit (moteurs : {string.Join(", ", moteurs)}).");
        ctx.VerifierEcriture("Lier un nouvel export");
        var t = ancien.Instance.GetTransform();
        var etatAvant = ctx.Etat();
        var revAncienne = etatAvant.DerniereRevision(ancien.Cle);
        var emp = AnalyseRevisionScan.Calculer(chemin);
        var nouvelle = ctx.AppliquerLot("BatiScan – Lier le nouvel export ReCap (E22)", () =>
        {
            var type = PointCloudType.Create(ctx.Doc, moteur, chemin);
            var inst = PointCloudInstance.Create(ctx.Doc, type.Id, t);
            var e = StockageBatiScan.LireEtat(ctx.Doc);
            // Empreinte de l'ANCIEN contenu : le contrôle suivant classera le nouveau lien en
            // « contenu modifié » (points revérifiés) ou « chemin seul » (contenu identique).
            e.AjouterRevision(type.UniqueId, new RevisionScan
            {
                Empreinte = revAncienne is null ? emp : revAncienne.Empreinte with { Chemin = chemin },
                TransformationInstance = ConversionRevit.VersM(t).VersMatrice4x4(),
                // Contrôles de l'ancienne révision repris comme « à revérifier » (nouveau contenu).
                PointsControle = revAncienne?.PointsControle ?? [],
                Decision = $"nouvel export lié avec le calage de « {ancien.Nom} » (révision {revAncienne?.Numero}) ; contenu à contrôler",
            });
            StockageBatiScan.EcrireEtat(ctx.Doc, e);
            return inst;
        });
        JournalLocal.Ecrire("scan.nouvel_export", new { ancien = ancien.Nom, chemin, sha = emp.Sha256 });
        Contexte.Informer(Titre, "Nouvel export lié", $"Nouveau lien créé avec la transformation de « {ancien.Nom} ». L'ancien lien est conservé (à supprimer manuellement après contrôle).\n\n"
            + "Étape suivante : relancer « Actualiser le scan » sur le NOUVEAU lien et choisir « Contrôler » pour revérifier les points de contrôle.");
        return Result.Succeeded;
    }

    private Result Historique(Contexte ctx, NuageSource nuage)
    {
        var etat = ctx.Etat();
        if (!etat.Revisions.TryGetValue(nuage.Cle, out var revs) || revs.Count == 0) throw new ArretCommande("Aucune révision enregistrée.");
        var r = Dialogues.Choisir(Titre, "Révisions (filiation conservée). Choisir une révision pour restaurer sa transformation du nuage :", revs.AsEnumerable().Reverse().ToList(),
            x => $"n° {x.Numero} – {x.DateUtc.ToLocalTime():g} – SHA {x.Empreinte.Sha256[..Math.Min(12, x.Empreinte.Sha256.Length)]} – {x.Decision}");
        if (r is null) return Result.Cancelled;
        Restaurer(ctx, nuage, r);
        return Result.Succeeded;
    }

    private void Restaurer(Contexte ctx, NuageSource nuage, RevisionScan r)
    {
        var cible = TransformationRigide.DepuisMatrice4x4(r.TransformationInstance);
        var deplacement = cible.Apres(nuage.TransformationInstance.Inverse());
        if (!Contexte.Confirmer(Titre, $"Restaurer la transformation de la révision n° {r.Numero} ?",
                $"Déplacement du NUAGE uniquement : rotation {deplacement.AngleRotationDeg():F4}°, translation {deplacement.Translation.Norme * 1000:F1} mm."))
            return;
        ctx.VerifierEcriture("Restaurer une révision");
        ctx.AppliquerLot("BatiScan – Restaurer la transformation du nuage (E22)", () =>
        {
            CommandeCalage.DeplacerNuage(ctx.Doc, nuage.Instance, deplacement);
            var e = StockageBatiScan.LireEtat(ctx.Doc);
            e.InvaliderSource(nuage.Cle, $"transformation restaurée (révision {r.Numero})");
            e.AjouterRevision(nuage.Cle, new RevisionScan { Empreinte = r.Empreinte, TransformationInstance = r.TransformationInstance, PointsControle = r.PointsControle, Decision = $"restauration de la révision {r.Numero}" });
            StockageBatiScan.EcrireEtat(ctx.Doc, e);
            return 0;
        });
        ServiceSnap.Invalider(nuage.Cle);
    }

    private static void Enregistrer(Contexte ctx, NuageSource nuage, EmpreinteSource emp, TransformationRigide t, List<PointControle> controles, string decision, bool invalider = false)
    {
        ctx.VerifierEcriture("Enregistrer une révision de scan");
        StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Révision de scan (E22)", e =>
        {
            if (invalider) e.InvaliderSource(nuage.Cle, decision);
            e.AjouterRevision(nuage.Cle, new RevisionScan { Empreinte = emp, TransformationInstance = t.VersMatrice4x4(), PointsControle = controles, Decision = decision });
        });
        ServiceSnap.Invalider(nuage.Cle);
        JournalLocal.Ecrire("scan.revision", new { nuage = nuage.Nom, decision, emp.Sha256 });
    }
}
