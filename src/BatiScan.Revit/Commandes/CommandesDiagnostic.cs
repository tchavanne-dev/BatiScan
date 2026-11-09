using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BatiScan.Core.Journal;
using BatiScan.Core.Parametres;
using BatiScan.Core.Scan;
using BatiScan.Infrastructure;
using TaskDialog = BatiScan.Interface.BoiteDialogue;  // dialogues WPF du design system

namespace BatiScan.Commandes;

/// <summary>
/// E01 / E02 / E21 – Diagnostic du projet en LECTURE SEULE : environnement, moteur,
/// repères (interne, point de base, topographie, partagé), nuages, état BatiScan.
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeDiagnostic : CommandeBase
{
    protected override string Titre => "BatiScan – Diagnostic du projet";

    protected override Result Executer(Contexte ctx)
    {
        var doc = ctx.Doc;
        var sb = new StringBuilder();
        sb.AppendLine("LECTURE SEULE – aucune modification du projet.\n");
        sb.AppendLine("── Environnement ──");
        sb.AppendLine($"Revit {ctx.UiApp.Application.VersionNumber} ({ctx.UiApp.Application.VersionBuild}) – BatiScan {JournalLocal.VersionPlugin}");
        sb.AppendLine($"Document : {doc.Title}{(doc.IsWorkshared ? "  ⚠ MODÈLE COLLABORATIF : écritures soumises à confirmation" : "")}");
        var moteur = ctx.Moteur.VerifierDisponibilite(forcer: true);
        sb.AppendLine($"Moteur Python : {(moteur.Disponible ? "disponible – " : "INDISPONIBLE – ")}{moteur.Message}");
        sb.AppendLine($"Journal local : {JournalLocal.CheminDuJour}");
        sb.AppendLine($"Paramètres : {ParametresBatiScan.CheminParDefaut}");

        sb.AppendLine("\n── Repères (E02) ──");
        var repere = RepereProjet.Lire(doc);
        sb.Append(repere.Decrire());

        var etat = ctx.Etat();
        sb.AppendLine("\n── Nuages liés (E01, E22) ──");
        var nuages = NuageSource.Tous(doc);
        if (nuages.Count == 0) sb.AppendLine("Aucun nuage de points lié.");
        foreach (var n in nuages)
        {
            sb.AppendLine($"• {n.Nom} – {n.Chemin} [{n.StatutFichier()}]");
            var t = n.TransformationInstance;
            sb.AppendLine($"  Transformation d'instance : rotation en plan {t.AngleEnPlanDeg():F4}°, inclinaison {t.InclinaisonVerticaleDeg():F4}°, translation {ConversionRevit.P(t.Translation)}");
            var rev = etat.DerniereRevision(n.Cle);
            if (rev is null)
            {
                sb.AppendLine("  Révision : aucune enregistrée → repère NON CONFIRMÉ (lancer « Actualiser le scan » pour enregistrer la référence).");
                continue;
            }
            var emp = n.CalculerEmpreinte();
            var type = AnalyseRevisionScan.Classer(rev, emp, t.VersMatrice4x4());
            sb.AppendLine($"  Révision n° {rev.Numero} du {rev.DateUtc.ToLocalTime():g} – {rev.PointsControle.Count} point(s) de contrôle – état actuel : {type}");
        }

        sb.AppendLine("\n── État BatiScan (E05, E14, E21) ──");
        sb.AppendLine($"Nord de travail : {(etat.NordTravailDeg is double nt ? $"{nt:F3}° ({etat.NordTravailOrigine})" : "non défini")}");
        foreach (var z in etat.Zones)
            sb.AppendLine($"Zone « {z.Nom} » : {(z.Valide ? "valide" : "INVALIDE – " + z.RaisonInvalidite)}");
        foreach (var c in etat.Coupes)
            sb.AppendLine($"Coupe « {c.Nom} » ({c.AltitudesM.Length} altitudes) : {(c.Valide ? "valide" : "INVALIDE – " + c.RaisonInvalidite)}");
        foreach (var a in etat.Analyses)
            sb.AppendLine($"Analyse {a.Operation} du {a.DateUtc.ToLocalTime():g} : {a.Statut}{(a.RaisonInvalidite is null ? "" : " – " + a.RaisonInvalidite)}");
        int crees = StockageBatiScan.ElementsBatiScan<Wall>(doc).Count + StockageBatiScan.ElementsBatiScan<Level>(doc).Count
                  + StockageBatiScan.ElementsBatiScan<FamilyInstance>(doc).Count;
        sb.AppendLine($"Éléments créés par BatiScan (murs, niveaux, poteaux/poutres) : {crees}");

        string texte = sb.ToString();
        var d = new TaskDialog(Titre) { MainInstruction = "Diagnostic du projet", MainContent = texte };
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Fermer");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Copier dans le presse-papiers");
        if (d.Show() == TaskDialogResult.CommandLink2)
        {
            try { System.Windows.Clipboard.SetText(texte); }
            catch (System.Runtime.InteropServices.ExternalException) { /* presse-papiers verrouillé */ }
        }
        return Result.Succeeded;
    }
}

/// <summary>
/// Paramètres et maintenance (E21) : ouvre le fichier de paramètres ; purge des SEULES
/// données BatiScan du projet, après liste détaillée de ce qui sera effacé.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeParametres : CommandeBase
{
    protected override string Titre => "BatiScan – Paramètres et maintenance";

    protected override Result Executer(Contexte ctx)
    {
        var d = new TaskDialog(Titre)
        {
            MainInstruction = "Paramètres et maintenance",
            MainContent = $"Paramètres : {ParametresBatiScan.CheminParDefaut}\nJournal : {JournalLocal.Dossier}\nTravail moteur : {Core.Moteur.ClientMoteur.DossierTravailRacine}",
        };
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Ouvrir le fichier de paramètres (tolérances)");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Ouvrir le dossier du journal local");
        d.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Purger les données BatiScan de ce projet…");
        switch (d.Show())
        {
            case TaskDialogResult.CommandLink1:
                if (!File.Exists(ParametresBatiScan.CheminParDefaut)) ctx.Parametres.Enregistrer();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{ParametresBatiScan.CheminParDefaut}\"") { UseShellExecute = false });
                break;
            case TaskDialogResult.CommandLink2:
                Directory.CreateDirectory(JournalLocal.Dossier);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{JournalLocal.Dossier}\"") { UseShellExecute = false });
                break;
            case TaskDialogResult.CommandLink3:
                Purger(ctx);
                break;
        }
        return Result.Succeeded;
    }

    private static void Purger(Contexte ctx)
    {
        ctx.VerifierEcriture("Purge des données BatiScan");
        var e = ctx.Etat();
        string liste =
            $"Seront effacés (données BatiScan uniquement) :\n• Nord de travail : {(e.NordTravailDeg is null ? "–" : $"{e.NordTravailDeg:F3}°")}\n"
            + $"• {e.Zones.Count} zone(s), {e.Coupes.Count} coupe(s)\n"
            + $"• {e.Revisions.Sum(r => r.Value.Count)} révision(s) de scan et leurs points de contrôle\n"
            + $"• {e.Analyses.Count} analyse(s) enregistrée(s)\n\n"
            + "Ne seront PAS touchés : murs, niveaux, poteaux, poutres, nuages, coordonnées partagées (la provenance des éléments créés est conservée).";
        if (!Contexte.Confirmer("BatiScan – Purge", "Purger les données BatiScan du projet ?", liste)) return;
        int n = ctx.AppliquerLot("BatiScan – Purge des données du plugin (E21)", () => StockageBatiScan.PurgerEtat(ctx.Doc));
        JournalLocal.Ecrire("maintenance.purge", new { elements = n, contenu = liste });
        Contexte.Informer("BatiScan – Purge", "Purge terminée", n == 0 ? "Aucune donnée BatiScan à purger." : "Données BatiScan du projet supprimées.");
    }
}
