using Autodesk.Revit.UI;

namespace BatiScan;

/// <summary>
/// Application externe BatiScan – point d'entrée du plugin Revit.
/// Crée le panneau de ruban « BatiScan » dans l'onglet « Modules complémentaires ».
/// </summary>
public class Application : IExternalApplication
{
    // ── Constantes de ruban ──────────────────────────────────────────────────
    private const string NOM_ONGLET   = "BatiScan";
    private const string NOM_PANNEAU  = "Nuages de points";
    private const string NOM_BOUTON   = "Inventaire\ndes nuages";
    private const string TOOLTIP      = "Parcourt le projet et liste tous les nuages de points liés "
                                       + "(nom, chemin, unités, transformation, statut de calage).";
    private const string TOOLTIP_LONG = "LECTURE SEULE – Aucune modification des coordonnées partagées.\n\n"
                                       + "Affiche un rapport complet pour chaque PointCloudInstance "
                                       + "présent dans le projet actif.";

    // ────────────────────────────────────────────────────────────────
    //  OnStartup : création du ruban
    // ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            // ── Créer un onglet personnalisé ──────────────────────────────────
            application.CreateRibbonTab(NOM_ONGLET);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // L'onglet existe déjà (rechargement à chaud) → on continue
        }

        // ── Créer le panneau dans l'onglet ────────────────────────────────────
        RibbonPanel panneau = application.CreateRibbonPanel(NOM_ONGLET, NOM_PANNEAU);

        // ── Chemin de l'assembly courant ──────────────────────────────────────
        string cheminAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // ── Données du bouton push ────────────────────────────────────────────
        PushButtonData donnesBouton = new(
            name             : "cmdInventaireNuages",
            text             : NOM_BOUTON,
            assemblyName     : cheminAssembly,
            className        : typeof(CommandeNuageDePoints).FullName!
        )
        {
            ToolTip         = TOOLTIP,
            LongDescription = TOOLTIP_LONG,
            // Image 32×32 (optionnel – décommentez et ajoutez l'image dans /Resources)
            // LargeImage   = ChargerImage("Resources/nuage_32.png"),
            // Image        = ChargerImage("Resources/nuage_16.png"),
        };

        panneau.AddItem(donnesBouton);

        return Result.Succeeded;
    }

    // ────────────────────────────────────────────────────────────────
    //  OnShutdown : nettoyage (rien à libérer ici)
    // ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    // ────────────────────────────────────────────────────────────────
    //  Helpers (optionnel)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Charge une image embarquée comme ressource depuis le dossier du plugin.
    /// </summary>
    // private static System.Windows.Media.ImageSource ChargerImage(string chemin)
    // {
    //     string cheminComplet = System.IO.Path.Combine(
    //         System.IO.Path.GetDirectoryName(
    //             System.Reflection.Assembly.GetExecutingAssembly().Location)!, chemin);
    //     var uri = new Uri(cheminComplet);
    //     return new System.Windows.Media.Imaging.BitmapImage(uri);
    // }
}
