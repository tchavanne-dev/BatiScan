using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BatiScan.Core.Contrats;
using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;
using BatiScan.Core.Moteur;
using BatiScan.Core.Parametres;
using BatiScan.Interface;
using TaskDialog = BatiScan.Interface.BoiteDialogue;  // dialogues WPF du design system

namespace BatiScan.Infrastructure;

/// <summary>Arrêt propre d'une commande avec message utilisateur (aucune modification effectuée).</summary>
public sealed class ArretCommande(string message) : Exception(message);

/// <summary>
/// Contexte partagé des commandes : paramètres, moteur, garde d'écriture (E21),
/// choix du nuage et de la zone (E05), exécution du moteur avec progression.
/// </summary>
public sealed class Contexte
{
    public UIApplication UiApp { get; }
    public UIDocument UiDoc { get; }
    public Document Doc { get; }
    public ParametresBatiScan Parametres { get; }

    private static ClientMoteur? _moteur;
    private static string? _racineMoteur;

    public Contexte(ExternalCommandData data)
    {
        UiApp = data.Application;
        UiDoc = UiApp.ActiveUIDocument ?? throw new ArretCommande("Aucun document actif.");
        Doc = UiDoc.Document;
        Parametres = ParametresBatiScan.Charger();
        FenetreValidation.ProprietaireRevit = UiApp.MainWindowHandle;
    }

    public ClientMoteur Moteur
    {
        get
        {
            if (_moteur is null || _racineMoteur != Parametres.RacineMoteur)
            {
                _moteur = new ClientMoteur(Parametres.RacineMoteur);
                _racineMoteur = Parametres.RacineMoteur;
            }
            return _moteur;
        }
    }

    // ── Garde d'écriture (E21, worksharing) ─────────────────────────────────

    /// <summary>
    /// À appeler avant toute écriture. Projet collaboratif : avertissement et blocage
    /// sauf confirmation explicite. Document non modifiable : arrêt.
    /// </summary>
    public void VerifierEcriture(string operation)
    {
        if (Doc.IsReadOnly) throw new ArretCommande("Le document est en lecture seule : aucune modification possible.");
        if (Doc.IsFamilyDocument) throw new ArretCommande("BatiScan s'utilise dans un projet, pas dans l'éditeur de familles.");
        if (!Doc.IsWorkshared) return;
        var d = new TaskDialog("BatiScan – Modèle collaboratif")
        {
            MainIcon = TaskDialogIcon.TaskDialogIconWarning,
            MainInstruction = "Modèle collaboratif détecté (worksharing)",
            MainContent = $"L'opération « {operation} » va modifier le modèle partagé. Le worksharing est hors périmètre "
                        + "recommandé de BatiScan : vérifier les sous-projets et emprunts avant de continuer.\n\nContinuer quand même ?",
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            DefaultButton = TaskDialogResult.No,
        };
        if (d.Show() != TaskDialogResult.Yes) throw new ArretCommande("Opération annulée (modèle collaboratif).");
        JournalLocal.Ecrire("worksharing.confirmation", new { operation, document = Doc.Title }, "avertissement");
    }

    /// <summary>E21 – Fonctions avancées indisponibles sans moteur : on l'indique, sans échec.</summary>
    public void ExigerMoteur()
    {
        var e = Moteur.VerifierDisponibilite();
        if (!e.Disponible) throw new ArretCommande("Fonction avancée indisponible.\n\n" + e.Message);
    }

    // ── Nuage et zone ───────────────────────────────────────────────────────

    public NuageSource ChoisirNuage()
    {
        var nuages = NuageSource.Tous(Doc);
        if (nuages.Count == 0) throw new ArretCommande("Aucun nuage de points lié dans le projet (E01).");
        if (nuages.Count == 1) return nuages[0];
        return Dialogues.Choisir("BatiScan – Nuage", "Choisir le nuage de points à utiliser :", nuages,
                   n => $"{n.Nom}  –  {n.Chemin}  [{n.StatutFichier()}]")
               ?? throw new ArretCommande("Aucun nuage choisi.");
    }

    public EtatProjet Etat() => StockageBatiScan.LireEtat(Doc);

    /// <summary>
    /// E05 – Zone EXPLICITE : zone enregistrée valide, zone de coupe de la vue 3D active, ou nuage entier.
    /// Le périmètre réellement traité est toujours annoncé.
    /// </summary>
    public BoiteZone ChoisirZone(NuageSource nuage, string usage)
    {
        var etat = Etat();
        string cle = nuage.EmpreinteContexte(etat);
        var choix = new List<(string libelle, Func<BoiteZone> zone)>();
        if (Doc.ActiveView is View3D v3 && v3.IsSectionBoxActive)
            choix.Add(("Zone de coupe de la vue 3D active", () => ZoneDepuisVue(v3, "Zone de coupe (vue active)")));
        foreach (var z in etat.Zones)
        {
            bool valide = z.Valide && (z.EmpreinteContexte == "" || z.EmpreinteContexte == cle || z.CleSource != nuage.Cle);
            string lib = $"Zone enregistrée « {z.Nom} » ({z.MaxM[0] - z.MinM[0]:F1} × {z.MaxM[1] - z.MinM[1]:F1} × {z.EpaisseurM:F2} m)"
                       + (valide ? "" : $"  – INVALIDE : {z.RaisonInvalidite ?? "source ou transformation modifiée"}");
            if (valide) choix.Add((lib, () => BoiteZone.Depuis(z)));
            else choix.Add((lib, () => throw new ArretCommande($"Zone « {z.Nom} » invalidée : {z.RaisonInvalidite ?? "source ou transformation modifiée depuis sa création"}. Redéfinir la zone.")));
        }
        choix.Add(("Nuage entier (sous-échantillonné, peut être long)", () => ExtractionPoints.BoiteNuage(nuage)));
        int i = Dialogues.ChoisirIndex("BatiScan – Zone de travail", $"Zone explicite pour « {usage} » (E05) :", choix.Select(t => t.libelle).ToList());
        if (i < 0) throw new ArretCommande("Aucune zone choisie.");
        return choix[i].zone();
    }

    public static BoiteZone ZoneDepuisVue(View3D v, string nom)
    {
        BoundingBoxXYZ bb = v.GetSectionBox();
        var t = ConversionRevit.VersM(bb.Transform);
        var min = ConversionRevit.VersM(bb.Min);
        var max = ConversionRevit.VersM(bb.Max);
        var centre = t.Appliquer((min + max) / 2);
        var demi = (max - min) / 2;
        double rot = t.AngleEnPlanDeg();
        if (t.InclinaisonVerticaleDeg() > 1e-3)
            throw new ArretCommande("Zone de coupe inclinée : seules les rotations autour de la verticale sont prises en charge.");
        return new BoiteZone(nom, centre - demi, centre + demi, rot);
    }

    // ── Moteur ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrait les points de la zone (thread Revit), puis exécute le moteur en arrière-plan
    /// avec progression annulable. Erreur moteur ⇒ arrêt sans modification.
    /// </summary>
    public (ReponseMoteur reponse, PointsExtraits points) ExecuterMoteur(
        string operation, NuageSource nuage, BoiteZone zone, System.Text.Json.Nodes.JsonObject? parametres = null, double? densiteM = null)
    {
        ExigerMoteur();
        var extrait = ExtractionPoints.Extraire(nuage, zone, densiteM ?? Parametres.DensiteExtractionM, Parametres.PointsMaxParExtraction);
        if (extrait.Points.Count == 0)
            throw new ArretCommande($"Aucun point du nuage dans la zone {zone.Decrire()} : zone non observée.");
        if (extrait.Repere == RepereGetPoints.Inconnu)
            throw new ArretCommande("Repère des points extraits indéterminé (ni modèle, ni local) : extraction refusée. Voir le journal.");
        var etat = Etat();
        var rev = etat.DerniereRevision(nuage.Cle);
        var repere = RepereProjet.Lire(Doc);
        var req = new RequeteMoteur
        {
            Operation = operation,
            Repere = new Repere
            {
                Nom = "revit_interne_m",
                Description = $"Coordonnées internes Revit converties en mètres ; projet « {Doc.Title} »",
                TransformationVersPartage = repere.InterneVersPartage?.VersMatrice4x4(),
            },
            Source = new SourceNuage
            {
                Id = nuage.Cle,
                Chemin = nuage.Chemin,
                Empreinte = rev is null ? "non_enregistree" : "sha256:" + rev.Empreinte.Sha256,
                Revision = rev?.Numero,
                TransformationInstance = nuage.TransformationInstance.VersMatrice4x4(),
            },
            Points = new SpecPoints { Chemin = "points.bin", DensiteMoyenneM = extrait.DensiteM },
            Zone = zone.VersJson(),
            Parametres = parametres ?? [],
            Graine = Parametres.Graine,
        };
        var delai = TimeSpan.FromSeconds(Parametres.DelaiMaxMoteurS);
        var pts = extrait.Points;
        ReponseMoteur rep;
        try
        {
            rep = FenetreProgression.Executer($"BatiScan – Analyse du nuage : {operation}", (p, ct) =>
            {
                p.Report((0.02, $"{pts.Count:N0} points extraits – lancement du moteur"));
                return Moteur.Executer(req, pts, delai, p, ct);
            });
        }
        catch (OperationCanceledException) { throw new ArretCommande("Calcul annulé par l'utilisateur. Aucune modification."); }
        catch (TimeoutException e) { throw new ArretCommande(e.Message); }
        JournalLocal.Ecrire($"moteur.{operation}", new
        {
            nuage = nuage.Nom, zone = zone.Decrire(), points = pts.Count, plafond = extrait.PlafondAtteint,
            repere_getpoints = extrait.Repere.ToString(), statut = rep.Statut, duree_s = rep.DureeS,
            propositions = rep.Propositions.Count, rep.Avertissements, erreurs = rep.ResumeErreurs(), parametres = rep.Parametres?.ToJsonString(),
        });
        if (rep.EnErreur) throw new ArretCommande("Le moteur a signalé une erreur ; aucune modification.\n\n" + rep.ResumeErreurs());
        return (rep, extrait);
    }

    /// <summary>Texte de contexte commun à afficher avec les propositions (E06, E14).</summary>
    public static string ResumeCalcul(ReponseMoteur r, PointsExtraits e, BoiteZone zone)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Périmètre réellement traité : {zone.Decrire()}");
        sb.AppendLine($"Points : {r.NombrePoints.Recus:N0} extraits (densité demandée {e.DensiteM * 1000:F0} mm{(e.PlafondAtteint ? ", PLAFOND ATTEINT : densité réduite" : "")}), {r.NombrePoints.Traites:N0} traités ; couverture en plan {r.Couverture.Ratio:P0}");
        sb.AppendLine($"Unités : m, degrés – Repère : interne Revit – Moteur {r.Moteur?.Version} / Open3D {r.Moteur?.Open3d} – graine {r.Graine} – {r.DureeS:F1} s");
        foreach (var a in r.Avertissements) sb.AppendLine("⚠ " + a);
        return sb.ToString();
    }

    // ── Niveaux ─────────────────────────────────────────────────────────────

    public List<Level> Niveaux() =>
        new FilteredElementCollector(Doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();

    /// <summary>Altitude interne (m) d'un niveau. ProjectElevation = élévation par rapport à l'origine du projet.</summary>
    public static double AltitudeInterneM(Level l) => ConversionRevit.VersM(l.ProjectElevation);

    public Level ChoisirNiveau(string consigne, double? altitudeProcheM = null)
    {
        var niveaux = Niveaux();
        if (niveaux.Count == 0) throw new ArretCommande("Aucun niveau dans le projet : créer d'abord les niveaux (commande Niveaux).");
        if (altitudeProcheM is double z)
            niveaux = niveaux.OrderBy(l => Math.Abs(AltitudeInterneM(l) - z)).ToList();
        return Dialogues.Choisir("BatiScan – Niveau", consigne, niveaux, l => $"{l.Name}  (z interne {AltitudeInterneM(l):F3} m)")
               ?? throw new ArretCommande("Aucun niveau choisi.");
    }

    /// <summary>Message final (succès / information).</summary>
    public static void Informer(string titre, string instruction, string contenu)
    {
        var d = new TaskDialog(titre) { MainInstruction = instruction, MainContent = contenu };
        d.Show();
    }

    public static bool Confirmer(string titre, string instruction, string contenu)
    {
        var d = new TaskDialog(titre)
        {
            MainInstruction = instruction, MainContent = contenu,
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, DefaultButton = TaskDialogResult.No,
        };
        return d.Show() == TaskDialogResult.Yes;
    }

    /// <summary>Applique un lot dans une transaction nommée ; toute exception ⇒ rollback complet (E21).</summary>
    public T AppliquerLot<T>(string nomTransaction, Func<T> lot)
    {
        using var t = new Transaction(Doc, nomTransaction);
        var opts = t.GetFailureHandlingOptions();
        opts.SetClearAfterRollback(true);
        t.SetFailureHandlingOptions(opts);
        t.Start();
        try
        {
            T r = lot();
            var statut = t.Commit();
            if (statut != TransactionStatus.Committed)
                throw new ArretCommande($"Le lot « {nomTransaction} » n'a pas été validé par Revit ({statut}) : document inchangé.");
            JournalLocal.Ecrire("lot.applique", new { transaction = nomTransaction });
            return r;
        }
        catch (Exception e)
        {
            if (t.HasStarted() && !t.HasEnded()) t.RollBack();
            JournalLocal.Ecrire("lot.annule", new { transaction = nomTransaction, erreur = e.Message }, "erreur");
            if (e is ArretCommande) throw;
            throw new ArretCommande($"Échec du lot « {nomTransaction} » : {e.Message}\n\nLe document est resté dans son état initial (rollback).");
        }
    }
}
