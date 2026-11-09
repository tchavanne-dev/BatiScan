using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;
using BatiScan.Core.Rapports;
using BatiScan.Core.Scan;
using BatiScan.Infrastructure;
using BatiScan.Interface;
using TaskDialog = BatiScan.Interface.BoiteDialogue;  // dialogues WPF du design system

namespace BatiScan.Commandes;

/// <summary>
/// E05 – Zones de travail nommées : enregistrement depuis la zone de coupe 3D, tranche
/// associée à un niveau, rappel dans la vue 3D, suppression. Les coordonnées du nuage
/// ne sont jamais modifiées ; une zone est invalidée si sa source ou sa transformation change.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeZones : CommandeBase
{
    protected override string Titre => "BatiScan – Zones de travail (E05)";

    protected override Result Executer(Contexte ctx)
    {
        var d = new TaskDialog(Titre) { MainInstruction = "Zones de travail", MainContent = "Zone affichée, zone calculée et points exclus sont distincts : seule la zone enregistrée est traitée." };
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Enregistrer la zone de coupe de la vue 3D active");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Créer une tranche horizontale associée à un niveau");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Rappeler une zone dans la vue 3D active");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Supprimer une zone enregistrée");
        switch (d.Show())
        {
            case TaskDialogResult.CommandLink1: EnregistrerDepuisVue(ctx); break;
            case TaskDialogResult.CommandLink2: CreerTranche(ctx); break;
            case TaskDialogResult.CommandLink3: Rappeler(ctx); break;
            case TaskDialogResult.CommandLink4: Supprimer(ctx); break;
            default: return Result.Cancelled;
        }
        return Result.Succeeded;
    }

    private static void Enregistrer(Contexte ctx, BoiteZone z, NuageSource nuage, string? niveau)
    {
        ctx.VerifierEcriture("Enregistrer une zone");
        string? nom = Dialogues.Saisir("BatiScan – Nom de la zone", $"Nom de la zone ({z.Decrire()}) :", z.Nom);
        if (string.IsNullOrWhiteSpace(nom)) throw new ArretCommande("Zone non enregistrée.");
        var etat = ctx.Etat();
        if (etat.Zones.Any(x => x.Nom == nom) && !Contexte.Confirmer("BatiScan", "Zone existante", $"Remplacer la zone « {nom} » ?"))
            throw new ArretCommande("Zone non enregistrée.");
        string cle = nuage.EmpreinteContexte(etat);
        StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Enregistrer la zone (E05)", e =>
        {
            e.Zones.RemoveAll(x => x.Nom == nom);
            e.Zones.Add(new ZoneTravail
            {
                Nom = nom, MinM = z.Min.VersTableau(), MaxM = z.Max.VersTableau(), RotationDeg = z.RotationDeg,
                NiveauAssocie = niveau, CleSource = nuage.Cle, EmpreinteContexte = cle,
            });
        });
        JournalLocal.Ecrire("zone.enregistree", new { nom, zone = z.Decrire(), nuage = nuage.Nom });
        Contexte.Informer("BatiScan", "Zone enregistrée", $"« {nom} » : volume {(z.Max.X - z.Min.X) * (z.Max.Y - z.Min.Y) * z.EpaisseurM:F2} m³, épaisseur {z.EpaisseurM:F2} m.");
    }

    private static void EnregistrerDepuisVue(Contexte ctx)
    {
        if (ctx.Doc.ActiveView is not View3D v || !v.IsSectionBoxActive)
            throw new ArretCommande("Activer une vue 3D avec zone de coupe (Section Box) ajustée sur la partie à traiter.");
        var nuage = ctx.ChoisirNuage();
        Enregistrer(ctx, Contexte.ZoneDepuisVue(v, $"Zone {DateTime.Now:HHmm}"), nuage, null);
    }

    private static void CreerTranche(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var niveau = ctx.ChoisirNiveau("Niveau de référence de la tranche :");
        double[]? v = Dialogues.SaisirNombres("BatiScan – Tranche", "Bas et haut de la tranche, relatifs au niveau (m), ex. « 0,9 ; 1,1 » :", "0,9 ; 1,1");
        if (v is null || v.Length != 2 || v[1] <= v[0]) throw new ArretCommande("Tranche invalide : deux valeurs croissantes attendues.");
        var boite = ExtractionPoints.BoiteNuage(nuage);
        double z0 = Contexte.AltitudeInterneM(niveau);
        var etat = ctx.Etat();
        double rot = etat.NordTravailDeg ?? 0;
        var z = new BoiteZone($"Tranche {niveau.Name} +{v[0]:F2}/+{v[1]:F2}",
            new Vecteur3(boite.Min.X, boite.Min.Y, z0 + v[0]), new Vecteur3(boite.Max.X, boite.Max.Y, z0 + v[1]), 0);
        if (Math.Abs(rot) > 1e-9)
        {
            // Boîte orientée selon le Nord de travail, englobant l'emprise du nuage.
            var c = z.Centre;
            var r = TransformationRigide.RotationZ(-rot * Math.PI / 180, c);
            var coins = new[] { new Vecteur3(boite.Min.X, boite.Min.Y, 0), new Vecteur3(boite.Max.X, boite.Min.Y, 0), new Vecteur3(boite.Max.X, boite.Max.Y, 0), new Vecteur3(boite.Min.X, boite.Max.Y, 0) }
                .Select(r.Appliquer).ToList();
            z = new BoiteZone(z.Nom, new Vecteur3(coins.Min(p => p.X), coins.Min(p => p.Y), z.Min.Z), new Vecteur3(coins.Max(p => p.X), coins.Max(p => p.Y), z.Max.Z), rot);
        }
        Enregistrer(ctx, z, nuage, niveau.Name);
    }

    private static ZoneTravail ChoisirZoneEnregistree(Contexte ctx)
    {
        var zones = ctx.Etat().Zones;
        if (zones.Count == 0) throw new ArretCommande("Aucune zone enregistrée.");
        return Dialogues.Choisir("BatiScan – Zones", "Zone :", zones, z => $"{z.Nom}{(z.Valide ? "" : "  (INVALIDE : " + z.RaisonInvalidite + ")")}")
               ?? throw new ArretCommande("Aucune zone choisie.");
    }

    private static void Rappeler(Contexte ctx)
    {
        if (ctx.Doc.ActiveView is not View3D v) throw new ArretCommande("Activer une vue 3D pour rappeler une zone.");
        var zt = ChoisirZoneEnregistree(ctx);
        if (!zt.Valide && !Contexte.Confirmer("BatiScan", "Zone invalidée", $"{zt.RaisonInvalidite}\n\nRappeler quand même son emprise (affichage seul) ?"))
            return;
        var z = BoiteZone.Depuis(zt);
        var demi = (z.Max - z.Min) / 2;
        var t = TransformationRigide.RotationZ(z.RotationDeg * Math.PI / 180, Vecteur3.Zero);
        var tr = ConversionRevit.VersRevit(new TransformationRigide(t.Rotation, z.Centre));
        var bb = new BoundingBoxXYZ { Transform = tr, Min = ConversionRevit.VersPieds(-demi), Max = ConversionRevit.VersPieds(demi) };
        ctx.VerifierEcriture("Rappeler une zone (vue)");
        ctx.AppliquerLot("BatiScan – Rappeler la zone dans la vue (E05)", () =>
        {
            v.IsSectionBoxActive = true;
            v.SetSectionBox(bb);
            return 0;
        });
    }

    private static void Supprimer(Contexte ctx)
    {
        var zt = ChoisirZoneEnregistree(ctx);
        if (!Contexte.Confirmer("BatiScan", "Supprimer la zone ?", $"« {zt.Nom} » (donnée BatiScan uniquement ; le nuage n'est pas modifié).")) return;
        ctx.VerifierEcriture("Supprimer une zone");
        StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Supprimer la zone (E05)", e => e.Zones.RemoveAll(x => x.Nom == zt.Nom));
    }
}

/// <summary>
/// E23 – Accrochage au nuage avec mesure de latence (premier accès, cache, suivants :
/// médiane et p95) et enregistrement facultatif de points de contrôle (E02, E22).
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeSnap : CommandeBase
{
    protected override string Titre => "BatiScan – Accrochage au nuage (E23)";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var zone = ctx.ChoisirZone(nuage, "recherche d'accrochage");
        ServiceSnap.Preparer(ctx, nuage, zone);
        var controles = new List<PointControle>();
        var rapport = new RapportCsv("Accrochages au nuage (E23)")
            .Contexte("Nuage", nuage.Nom).Contexte("Zone", zone.Decrire()).Contexte("Unités", "m, repère interne Revit")
            .Contexte("Rayon de recherche (m)", RapportCsv.Formater(ctx.Parametres.RayonSnapM))
            .Colonnes("N°", "Clic X", "Clic Y", "Clic Z", "Point X", "Point Y", "Point Z", "Écart au clic (m)", "Affiné", "Précision", "Latence (ms)", "Point de contrôle");
        int n = 0;
        while (true)
        {
            var r = ServiceSnap.Accrocher(ctx, nuage, zone, "BatiScan : cliquer près du point à accrocher (Échap pour terminer)");
            if (r is null) break;
            n++;
            string? nomControle = null;
            if (double.IsNaN(r.EcartClicM))
            {
                TaskDialog.Show(Titre, $"Aucun point valide dans un rayon de {ctx.Parametres.RayonSnapM * 1000:F0} mm autour du clic (zone {zone.Nom}). Aucune valeur n'est retenue.");
            }
            else
            {
                var d = new TaskDialog(Titre)
                {
                    MainInstruction = "Point accroché",
                    MainContent = $"Point mesuré : {ConversionRevit.P(r.Point)} (interne)\nÉcart au clic : {ConversionRevit.Mm(r.EcartClicM)}\n"
                                + $"Précision : {r.Precision}\nLatence : {r.LatenceMs:F1} ms",
                };
                d.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Enregistrer comme point de contrôle (calage / actualisation)");
                d.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Continuer sans enregistrer");
                d.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Terminer");
                var choix = d.Show();
                if (choix == TaskDialogResult.CommandLink1)
                {
                    nomControle = Dialogues.Saisir("BatiScan – Point de contrôle", "Nom du point (identique à celui du fichier de coordonnées de référence) :", $"PC{controles.Count + 1}");
                    if (!string.IsNullOrWhiteSpace(nomControle))
                        controles.Add(new PointControle(nomControle, nuage.TransformationInstance.Inverse().Appliquer(r.Point)));
                }
                rapport.Ligne(n, r.Clic.X, r.Clic.Y, r.Clic.Z, r.Point.X, r.Point.Y, r.Point.Z, r.EcartClicM, r.Affine, r.Precision, r.LatenceMs, nomControle);
                if (choix == TaskDialogResult.CommandLink3 || choix == TaskDialogResult.Close) break;
            }
        }

        var stats = ServiceSnap.Statistiques;
        string resume = stats.Resume(ctx.Parametres.CibleLatenceP95Ms);
        rapport.Contexte("Latences", resume);
        string chemin = RapportCsv.CheminHorodate("snap");
        rapport.Enregistrer(chemin);
        JournalLocal.Ecrire("snap.session", new { nuage = nuage.Nom, accrochages = n, resume, controles = controles.Count });

        if (controles.Count > 0)
        {
            ctx.VerifierEcriture("Enregistrer des points de contrôle");
            StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Points de contrôle (E02/E22)", e =>
            {
                var rev = e.DerniereRevision(nuage.Cle);
                if (rev is null)
                {
                    rev = new RevisionScan { Empreinte = nuage.CalculerEmpreinte(), TransformationInstance = nuage.TransformationInstance.VersMatrice4x4(), Decision = "référence créée lors de l'enregistrement de points de contrôle" };
                    e.AjouterRevision(nuage.Cle, rev);
                }
                foreach (var pc in controles)
                {
                    rev.PointsControle.RemoveAll(p => p.Nom == pc.Nom);
                    rev.PointsControle.Add(pc);
                }
            });
        }
        Contexte.Informer(Titre, $"{n} accrochage(s)", $"{resume}\n\n{controles.Count} point(s) de contrôle enregistré(s).\nRapport : {chemin}");
        return Result.Succeeded;
    }
}
