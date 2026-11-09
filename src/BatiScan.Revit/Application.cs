using Autodesk.Revit.UI;
using BatiScan.Commandes;

namespace BatiScan;

/// <summary>
/// Application externe BatiScan – point d'entrée du plugin Revit 2027.
/// Crée l'onglet « BatiScan » et ses panneaux : Diagnostic, Calage, Coupes &amp; Snap, Modélisation.
/// </summary>
public class Application : IExternalApplication
{
    // ── Constantes de ruban ──────────────────────────────────────────────────
    private const string NOM_ONGLET = "BatiScan";

    /// <summary>Définition d'un bouton : nom interne, texte, classe de commande, info-bulle.</summary>
    private sealed record Bouton(string Nom, string Texte, Type Commande, string Infobulle, string Description = "");

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

        Panneau(application, "Diagnostic",
            new Bouton("cmdInventaireNuages", "Inventaire\ndes nuages", typeof(CommandeNuageDePoints),
                "Liste les nuages liés (nom, chemin, unités, transformation, statut de calage). E01.", "LECTURE SEULE."),
            new Bouton("cmdDiagnostic", "Diagnostic\nprojet", typeof(CommandeDiagnostic),
                "Repères, coordonnées partagées, moteur Python, révisions de scan, zones et analyses. E01/E02/E21.", "LECTURE SEULE."),
            new Bouton("cmdParametres", "Paramètres", typeof(CommandeParametres),
                "Tolérances, journal local, purge des seules données BatiScan. E21."));

        Panneau(application, "Calage",
            new Bouton("cmdCalage", "Caler\nle nuage", typeof(CommandeCalage),
                "Calage par points de contrôle : résidus affichés avant application ; seul le nuage est déplacé. E02.",
                "Les coordonnées partagées ne sont jamais modifiées."),
            new Bouton("cmdOrientation", "Nord de\ntravail", typeof(CommandeOrientation),
                "Orientation assistée (deux points) ou automatique (alternatives). E03.", "Le Nord géographique n'est pas modifié."),
            new Bouton("cmdActualiser", "Actualiser\nle scan", typeof(CommandeActualiserScan),
                "Ré-export ReCap : chemin / contenu / repère distingués, contrôles revérifiés, historique. E22.",
                "Aucune mise à jour automatique de la maquette."));

        Panneau(application, "Coupes & Snap",
            new Bouton("cmdZones", "Zones de\ntravail", typeof(CommandeZones), "Zones nommées, tranches, rappel dans la vue 3D. E05."),
            new Bouton("cmdSnap", "Accrochage\nau nuage", typeof(CommandeSnap),
                "Snap borné à une zone explicite, affiné à pleine densité, latence mesurée (médiane, p95). E23."),
            new Bouton("cmdCoupes", "Coupes\nmultiples", typeof(CommandeCoupes),
                "Traces à plusieurs altitudes, dévers, projection sur plan explicite, plans de référence. E05/E24."));

        Panneau(application, "Modélisation",
            new Bouton("cmdNiveaux", "Niveaux", typeof(CommandeNiveaux),
                "Détection des surfaces horizontales, zéro architectural, création validée sans doublon. E04."),
            new Bouton("cmdMurs", "Murs\npar zone", typeof(CommandeMurs),
                "Murs droits proposés et validés ; murs non plombés : incliné fidèle ou vertical + rapport de dévers. E07/E08/E24."),
            new Bouton("cmdStructure", "Poteaux\npoutres", typeof(CommandeStructure),
                "Poteaux et poutres à partir de familles chargées ; section non affirmée, aucun calcul. E12."),
            new Bouton("cmdEcarts", "Écarts\nnuage", typeof(CommandeEcarts),
                "Écarts et couverture des faces de murs ; « non observé » distinct de « conforme ». E14."));

        return Result.Succeeded;
    }

    private static void Panneau(UIControlledApplication app, string nom, params Bouton[] boutons)
    {
        RibbonPanel panneau = app.CreateRibbonPanel(NOM_ONGLET, nom);
        string cheminAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
        foreach (var b in boutons)
        {
            panneau.AddItem(new PushButtonData(b.Nom, b.Texte, cheminAssembly, b.Commande.FullName!)
            {
                ToolTip = b.Infobulle,
                LongDescription = b.Description,
            });
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  OnShutdown : nettoyage (rien à libérer ici)
    // ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
