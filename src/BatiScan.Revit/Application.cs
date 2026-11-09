using System.Windows.Media;
using Autodesk.Revit.UI;
using BatiScan.Commandes;
using BatiScan.Interface;

namespace BatiScan;

/// <summary>
/// Application externe BatiScan – point d'entrée du plugin Revit 2027.
/// Crée l'onglet « BatiScan » et ses panneaux : Diagnostic, Calage, Coupes &amp; Snap, Modélisation.
/// Icônes 16/32 px dessinées en mémoire (<see cref="Icones"/>), teinte par panneau issue du thème.
/// </summary>
public class Application : IExternalApplication
{
    // ── Constantes de ruban ──────────────────────────────────────────────────
    private const string NOM_ONGLET = "BatiScan";

    /// <summary>
    /// Bouton : <paramref name="Infobulle"/> = ce que fait la commande (une phrase) ;
    /// <paramref name="Description"/> = comment, garanties et exigence (survol prolongé).
    /// </summary>
    private sealed record Bouton(string Nom, string Texte, Type Commande, Glyphe Glyphe, string Infobulle, string Description);

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

        Panneau(application, "Diagnostic", "Couleur.Ruban.Diagnostic",
            new Bouton("cmdInventaireNuages", "Inventaire\ndes nuages", typeof(CommandeNuageDePoints), Icones.Nuage,
                "Liste les nuages de points liés au projet et leur état de calage.",
                "Pour chaque nuage : fichier source, unités, transformation d'instance, boîte englobante et statut de calage.\n\n"
                + "Lecture seule – aucune modification du projet. (E01)"),
            new Bouton("cmdDiagnostic", "Diagnostic\nprojet", typeof(CommandeDiagnostic), Icones.Diagnostic,
                "Contrôle complet du projet avant tout travail sur le nuage.",
                "Repères (interne, point de base, topographie, coordonnées partagées), disponibilité du moteur de calcul, "
                + "révisions de scan, zones et analyses à recalculer.\n\nLecture seule. (E01, E02, E21)"),
            new Bouton("cmdParametres", "Paramètres", typeof(CommandeParametres), Icones.Reglages,
                "Tolérances, journal local et maintenance du plugin.",
                "Ouvre le fichier de tolérances et le journal local. La purge efface uniquement les données BatiScan du projet, "
                + "après en avoir présenté la liste. (E21)"));

        Panneau(application, "Calage", "Couleur.Ruban.Calage",
            new Bouton("cmdCalage", "Caler\nle nuage", typeof(CommandeCalage), Icones.Cible,
                "Cale le nuage sur des points de contrôle de coordonnées connues.",
                "Ajustement rotation + translation ; les résidus et le sens de la transformation sont affichés avant application. "
                + "Seul le nuage est déplacé : coordonnées partagées et maquette restent intactes. (E02)"),
            new Bouton("cmdOrientation", "Nord de\ntravail", typeof(CommandeOrientation), Icones.Boussole,
                "Définit le Nord de travail par deux points ou par les directions détectées.",
                "Mode assisté (deux points accrochés) ou automatique (alternatives proposées, jamais choisies seules). "
                + "Le Nord géographique n'est pas modifié et aucun élément n'est déplacé. (E03)"),
            new Bouton("cmdActualiser", "Actualiser\nle scan", typeof(CommandeActualiserScan), Icones.Actualiser,
                "Met à jour un nuage après un ré-export ReCap, sans casser le calage.",
                "Distingue chemin modifié, contenu modifié et repère modifié ; revérifie les points de contrôle et refuse "
                + "toute correction globale si le recalage n'est pas rigide. Historique restaurable. (E22)"));

        Panneau(application, "Coupes & Snap", "Couleur.Ruban.Coupes",
            new Bouton("cmdZones", "Zones de\ntravail", typeof(CommandeZones), Icones.Zone,
                "Enregistre et rappelle des zones et des tranches de travail nommées.",
                "Depuis la zone de coupe de la vue 3D ou par tranche liée à un niveau. Une zone est invalidée si la source "
                + "ou sa transformation change. (E05)"),
            new Bouton("cmdSnap", "Accrochage\nau nuage", typeof(CommandeSnap), Icones.Accrochage,
                "Accroche un point mesuré du nuage, rapidement et de façon vérifiable.",
                "Recherche limitée à une zone explicite, point affiné à pleine densité ou signalé approximatif, latences mesurées "
                + "(médiane, p95). Enregistre des points de contrôle. (E23)"),
            new Bouton("cmdCoupes", "Coupes\nmultiples", typeof(CommandeCoupes), Icones.Coupes,
                "Compare des traces de coupe à plusieurs altitudes et mesure le dévers.",
                "Traces mesurées, projections sur un plan explicite et plans de référence restent trois représentations distinctes. "
                + "Le nuage source n'est jamais modifié. (E05, E24)"));

        Panneau(application, "Modélisation", "Couleur.Ruban.Modelisation",
            new Bouton("cmdNiveaux", "Niveaux", typeof(CommandeNiveaux), Icones.Niveaux,
                "Propose des niveaux à partir des surfaces horizontales du nuage.",
                "Vous choisissez le zéro ±0,00 et la convention ; les niveaux existants sont réutilisés, sans doublon. "
                + "Création après validation du récapitulatif. (E04)"),
            new Bouton("cmdMurs", "Murs\npar zone", typeof(CommandeMurs), Icones.Murs,
                "Crée des murs droits validés à partir d'une zone du nuage.",
                "Épaisseur mesurée ou hypothèse affichée ; murs non plombés en mur incliné fidèle ou vertical avec rapport de dévers ; "
                + "régularisation facultative sous tolérance. Types existants jamais modifiés. (E07, E08, E24)"),
            new Bouton("cmdStructure", "Poteaux\npoutres", typeof(CommandeStructure), Icones.Structure,
                "Détecte et crée des poteaux et des poutres à partir de familles chargées.",
                "Seule la géométrie observée est rapportée : aucune section ni matériau affirmé, aucun calcul de structure. (E12)"),
            new Bouton("cmdEcarts", "Écarts\nnuage", typeof(CommandeEcarts), Icones.Ecarts,
                "Mesure l'écart entre le nuage et les faces des murs sélectionnés.",
                "Statistiques par face et couverture ; une face sans points reste « non observée », jamais « conforme ». "
                + "Estimation géométrique, pas une certification. (E14)"));

        return Result.Succeeded;
    }

    private static void Panneau(UIControlledApplication app, string nom, string cleTeinte, params Bouton[] boutons)
    {
        RibbonPanel panneau = app.CreateRibbonPanel(NOM_ONGLET, nom);
        string cheminAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
        Color teinte = Theme.Couleur(cleTeinte);
        foreach (var b in boutons)
        {
            var (petite, grande) = Icones.Paire(b.Glyphe, teinte);
            panneau.AddItem(new PushButtonData(b.Nom, b.Texte, cheminAssembly, b.Commande.FullName!)
            {
                ToolTip = b.Infobulle,
                LongDescription = b.Description,
                Image = petite,
                LargeImage = grande,
            });
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  OnShutdown : nettoyage (rien à libérer ici)
    // ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
