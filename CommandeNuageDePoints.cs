using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using Autodesk.Revit.UI;
using System.Text;

namespace BatiScan;

/// <summary>
/// Commande externe BatiScan – Inventaire des nuages de points liés au projet Revit.
/// <para>
/// Cette commande est en LECTURE SEULE. Elle n'écrit aucune donnée dans le projet
/// et ne modifie jamais les coordonnées partagées (règle de préservation du calage).
/// </para>
/// </summary>
[Transaction(TransactionMode.ReadOnly)]   // ← Garantit qu'aucune transaction d'écriture n'est ouverte
[Regeneration(RegenerationOption.Manual)]
[Journaling(JournalingMode.NoCommandData)]
public class CommandeNuageDePoints : IExternalCommand
{
    // ────────────────────────────────────────────────────────────────
    //  Constantes UI
    // ────────────────────────────────────────────────────────────────
    private const string TITRE_DIALOG       = "BatiScan – Inventaire des nuages de points";
    private const string TITRE_AUCUN        = "BatiScan – Aucun nuage trouvé";
    private const string MSG_AUCUN          = "Aucun nuage de points lié n'a été trouvé dans le projet actif.\n\n"
                                             + "Vérifiez que des nuages RCP/PCG/E57 sont bien insérés.";
    private const string AVERTISSEMENT_CALAGE =
        "⚠️  RÈGLE DE PRÉSERVATION DES COORDONNÉES PARTAGÉES\n"
        + "Cette commande est en LECTURE SEULE. Les coordonnées partagées existantes\n"
        + "du projet ne sont jamais modifiées par BatiScan.\n"
        + "Pour recaler un nuage, utilisez exclusivement la commande dédiée « Calage ».\n\n";

    // ────────────────────────────────────────────────────────────────
    //  Point d'entrée IExternalCommand
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exécute la commande de lecture des nuages de points.
    /// </summary>
    /// <param name="commandData">Données contextuelles fournies par Revit.</param>
    /// <param name="message">Message d'erreur à retourner à Revit en cas d'échec.</param>
    /// <param name="elements">Sélection d'éléments (non utilisée ici – lecture seule).</param>
    /// <returns><see cref="Result.Succeeded"/> si l'inventaire s'est déroulé correctement.</returns>
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        try
        {
            UIApplication uiApp  = commandData.Application;
            UIDocument    uiDoc  = uiApp.ActiveUIDocument;
            Document      doc    = uiDoc.Document;

            // ── 1. Collecte de tous les PointCloudInstance du projet ──────────
            IList<PointCloudInstance> nuages = CollecterNuages(doc);

            // ── 2. Aucun nuage → dialogue informatif ──────────────────────────
            if (nuages.Count == 0)
            {
                TaskDialog.Show(TITRE_AUCUN, MSG_AUCUN);
                return Result.Succeeded;
            }

            // ── 3. Construction du rapport ────────────────────────────────────
            string rapport = ConstruireRapport(doc, nuages);

            // ── 4. Affichage dans un TaskDialog Revit ─────────────────────────
            AfficherRapport(rapport, nuages.Count);

            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // L'utilisateur a annulé : résultat neutre, pas d'erreur affichée
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            message = $"[BatiScan] Erreur inattendue : {ex.Message}\n\nDétails :\n{ex.StackTrace}";
            return Result.Failed;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Méthodes privées
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parcourt le document Revit et retourne tous les <see cref="PointCloudInstance"/> présents.
    /// Inclut les nuages actifs et les nuages désactivés (phase, workset).
    /// </summary>
    private static IList<PointCloudInstance> CollecterNuages(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(PointCloudInstance))
            .WhereElementIsNotElementType()
            .Cast<PointCloudInstance>()
            .ToList();
    }

    /// <summary>
    /// Construit le texte du rapport pour l'ensemble des nuages détectés.
    /// </summary>
    private static string ConstruireRapport(Document doc, IList<PointCloudInstance> nuages)
    {
        var sb = new StringBuilder();

        sb.AppendLine(AVERTISSEMENT_CALAGE);
        sb.AppendLine($"📦  {nuages.Count} nuage(s) de points trouvé(s) dans le projet « {doc.Title} »");
        sb.AppendLine(new string('─', 60));

        int index = 1;
        foreach (PointCloudInstance nuage in nuages)
        {
            sb.AppendLine();
            sb.AppendLine($"── Nuage #{index++} ──────────────────────────────────────────");
            sb.AppendLine(DecrireNuage(doc, nuage));
        }

        sb.AppendLine();
        sb.AppendLine(new string('─', 60));
        sb.AppendLine("Fin du rapport BatiScan (lecture seule).");

        return sb.ToString();
    }

    /// <summary>
    /// Génère la fiche descriptive d'un nuage de points individuel.
    /// </summary>
    private static string DecrireNuage(Document doc, PointCloudInstance nuage)
    {
        var sb = new StringBuilder();

        // ── Nom de l'instance ─────────────────────────────────────────────────
        string nom = nuage.Name;
        if (string.IsNullOrWhiteSpace(nom))
            nom = $"<sans nom – Id {nuage.Id.Value}>";
        sb.AppendLine($"  Nom          : {nom}");

        // ── Chemin du fichier source ──────────────────────────────────────────
        // PointCloudType contient le chemin physique vers le fichier RCP/PCG/E57
        PointCloudType? type = doc.GetElement(nuage.GetTypeId()) as PointCloudType;
        string cheminFichier = type?.get_Parameter(BuiltInParameter.POINT_CLOUD_FILE_PATH)
                                    ?.AsString()
                               ?? "<chemin non disponible>";
        sb.AppendLine($"  Fichier      : {cheminFichier}");

        // ── Unités du document hôte ───────────────────────────────────────────
        // Les nuages sont exprimés dans les unités internes de Revit (pieds).
        // On affiche les unités de projet pour contextualiser.
        FormatValueOptions opts      = new();
        Units              unites    = doc.GetUnits();
        FormatOptions      fmtLength = unites.GetFormatOptions(SpecTypeId.Length);
        string             unitLabel = LabelUtils.GetLabelForUnit(fmtLength.GetUnitTypeId());
        sb.AppendLine($"  Unités projet: {unitLabel}");

        // ── Transformation (position / rotation dans le projet) ───────────────
        Transform t = nuage.GetTransform();
        sb.AppendLine($"  Transformation :");
        sb.AppendLine($"    Origine  : ({FormatCoord(t.Origin.X)}, {FormatCoord(t.Origin.Y)}, {FormatCoord(t.Origin.Z)}) [pieds internes]");
        sb.AppendLine($"    Axe X    : ({FormatCoord(t.BasisX.X)}, {FormatCoord(t.BasisX.Y)}, {FormatCoord(t.BasisX.Z)})");
        sb.AppendLine($"    Axe Y    : ({FormatCoord(t.BasisY.X)}, {FormatCoord(t.BasisY.Y)}, {FormatCoord(t.BasisY.Z)})");
        sb.AppendLine($"    Axe Z    : ({FormatCoord(t.BasisZ.X)}, {FormatCoord(t.BasisZ.Y)}, {FormatCoord(t.BasisZ.Z)})");
        sb.AppendLine($"    Échelle  : {t.Scale:F6}");
        sb.AppendLine($"    Identique: {(t.IsIdentity ? "Oui (aucun déplacement)" : "Non (nuage déplacé/tourné)")}");

        // ── Boîte englobante ──────────────────────────────────────────────────
        try
        {
            BoundingBoxXYZ bb = nuage.get_BoundingBox(null);
            if (bb != null)
            {
                sb.AppendLine($"  Boîte englobante (pieds internes) :");
                sb.AppendLine($"    Min : ({FormatCoord(bb.Min.X)}, {FormatCoord(bb.Min.Y)}, {FormatCoord(bb.Min.Z)})");
                sb.AppendLine($"    Max : ({FormatCoord(bb.Max.X)}, {FormatCoord(bb.Max.Y)}, {FormatCoord(bb.Max.Z)})");
            }
        }
        catch
        {
            sb.AppendLine("  Boîte englobante : non calculable");
        }

        // ── Statut de calage ──────────────────────────────────────────────────
        // La propriété IsShared indique si le nuage est lié au système de coordonnées
        // partagées du projet. Un nuage non calé apparaît comme "référence non confirmée".
        string statutCalage = DetecterStatutCalage(nuage, t);
        sb.AppendLine($"  Statut calage : {statutCalage}");

        // ── Visibilité / activation ───────────────────────────────────────────
        sb.AppendLine($"  Visible      : {(!nuage.IsHidden(null) ? "Oui" : "Non (masqué)")}");

        return sb.ToString();
    }

    /// <summary>
    /// Détermine le statut de calage d'un nuage de points.
    /// <para>
    /// Heuristique : si la transformation est identité, le nuage n'a pas été
    /// repositionné manuellement et son calage reste "référence non confirmée".
    /// Un nuage repositionné (transformation non identité) est potentiellement calé,
    /// mais seul l'opérateur peut valider le calage métier.
    /// </para>
    /// </summary>
    private static string DetecterStatutCalage(PointCloudInstance nuage, Transform transform)
    {
        // Revit ne fournit pas de flag natif "calé / non calé" sur PointCloudInstance.
        // On utilise la transformation comme indicateur fiable.
        if (transform.IsIdentity)
            return "⚠️  Référence non confirmée (transformation identité – nuage non repositionné)";

        // Vérification supplémentaire : si l'origine est très proche du point de base
        // du projet (0,0,0 en coordonnées internes), le calage est suspect.
        const double seuilProximitePieds = 0.001; // ~ 0,3 mm
        bool origineNulle = transform.Origin.GetLength() < seuilProximitePieds;

        return origineNulle
            ? "⚠️  Référence non confirmée (origine à (0,0,0) – vérifier le calage)"
            : "✅  Repositionné (calage à valider avec l'opérateur)";
    }

    /// <summary>
    /// Affiche le rapport dans un <see cref="TaskDialog"/> Revit multi-lignes.
    /// </summary>
    private static void AfficherRapport(string rapport, int nombreNuages)
    {
        TaskDialog dlg = new(TITRE_DIALOG)
        {
            MainInstruction = $"{nombreNuages} nuage(s) de points inventorié(s)",
            MainContent     = rapport,
            // Largeur maximale autorisée par le TaskDialog Revit
            ExpandedContent = "Les coordonnées sont exprimées en pieds internes Revit (unité native de l'API).\n"
                            + "Pour convertir en mètres : valeur × 0,3048.",
        };

        // Bouton principal : Fermer
        dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Fermer le rapport");
        // Bouton secondaire : Copier dans le presse-papiers
        dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Copier le rapport dans le presse-papiers");

        TaskDialogResult resultat = dlg.Show();

        // Action sur le bouton "Copier"
        if (resultat == TaskDialogResult.CommandLink2)
        {
            try
            {
                System.Windows.Clipboard.SetText(rapport);
            }
            catch
            {
                // Le presse-papiers est parfois verrouillé par une autre application.
                // On ignore silencieusement l'erreur.
            }
        }
    }

    /// <summary>Formate une coordonnée avec 4 décimales pour l'affichage.</summary>
    private static string FormatCoord(double valeur) => valeur.ToString("F4");
}
