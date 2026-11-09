using System.Text.Json.Nodes;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BatiScan.Core.Contrats;
using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;
using BatiScan.Core.Murs;
using BatiScan.Core.Rapports;
using BatiScan.Infrastructure;
using BatiScan.Interface;

namespace BatiScan.Commandes;

// ════════════════════════════════════════════════════════════════════════════
//  E07 / E08 / E24 – Murs
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Ligne de proposition de mur (accepter / rejeter / corriger l'épaisseur).</summary>
public sealed class LigneMur
{
    public bool Accepter { get; set; } = true;
    public string Id { get; init; } = "";
    public double LongueurM { get; init; }
    public double HauteurM { get; init; }
    /// <summary>Épaisseur en mm : mesurée (2 faces) ou HYPOTHÈSE modifiable (1 face).</summary>
    public double EpaisseurMm { get; set; }
    public string Epaisseur { get; init; } = "";
    public string Plombage { get; init; } = "";
    public double DeversDeg { get; init; }
    public double DeplacementTeteMm { get; init; }
    public string Couverture { get; init; } = "";
    public string Existant { get; init; } = "";
    public string Justification { get; init; } = "";
    internal PropMur Prop { get; init; } = new();
}

/// <summary>Ligne de régularisation : état mesuré ET régularisé (E08).</summary>
public sealed class LigneRegularisation
{
    public bool Accepter { get; set; }
    public string Id { get; init; } = "";
    public double RotationDeg { get; init; }
    public double DeplacementMm { get; init; }
    public string Decision { get; init; } = "";
    internal ResultatRegularisation Resultat { get; init; } = null!;
}

/// <summary>
/// E07 / E08 / E24 – Murs droits par zone : propositions du moteur validées par l'utilisateur,
/// puis création de murs Revit éditables avec provenance. Murs non plombés : mode choisi par
/// l'utilisateur (incliné fidèle, ou vertical strict + rapport de dévers). Régularisation
/// facultative sous tolérance, avec état mesuré et régularisé visibles. Relance sans doublon ;
/// types existants réutilisés, jamais modifiés (E13).
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeMurs : CommandeBase
{
    protected override string Titre => "BatiScan – Murs par zone (E07/E24)";

    private const string ModeIncline = "Mur incliné fidèle (Inclinaison unique)";
    private const string ModeVertical = "Mur vertical strict + rapport de dévers";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var zone = ctx.ChoisirZone(nuage, "détection des murs");
        var niveauBase = ctx.ChoisirNiveau("Niveau de base des murs :", zone.Min.Z);
        var typesBase = new FilteredElementCollector(ctx.Doc).OfClass(typeof(WallType)).Cast<WallType>().Where(w => w.Kind == WallKind.Basic).OrderBy(w => w.Name).ToList();
        if (typesBase.Count == 0) throw new ArretCommande("Aucun type de mur de base dans le projet (E13).");
        var typeRef = Dialogues.Choisir(Titre, "Type de mur de référence (réutilisé si l'épaisseur correspond ; sinon DUPLIQUÉ sous un nouveau nom, jamais modifié) :", typesBase,
            w => $"{w.Name} ({ConversionRevit.VersM(w.Width) * 1000:F0} mm)") ?? throw new ArretCommande("Aucun type choisi.");

        var (rep, ext) = ctx.ExecuterMoteur("murs", nuage, zone, new JsonObject { ["tolerance_plomb_deg"] = ctx.Parametres.TolerancePlombDeg });
        var murs = rep.Lire<PropMur>("mur");
        string resumeCalcul = Contexte.ResumeCalcul(rep, ext, zone);
        if (murs.Count == 0) throw new ArretCommande("Aucun mur proposé dans la zone.\n\n" + resumeCalcul);

        // ── Relance : murs déjà créés par BatiScan (pas de doublon, E21) ─────
        var existants = StockageBatiScan.ElementsBatiScan<Wall>(ctx.Doc)
            .Where(t => t.provenance.Categorie == "mur" && t.element.Location is LocationCurve)
            .Select(t => (t.element, c: ((LocationCurve)t.element.Location).Curve)).ToList();
        var lignes = murs.Select(m =>
        {
            var a = Vecteur3.DepuisTableau(m.Debut);
            var b = Vecteur3.DepuisTableau(m.Fin);
            var dbl = existants.FirstOrDefault(x => DoublonsMurs.Coincident(a, b, ConversionRevit.VersM(x.c.GetEndPoint(0)), ConversionRevit.VersM(x.c.GetEndPoint(1)), ctx.Parametres.ToleranceDoublonM));
            return new LigneMur
            {
                Accepter = dbl.element is null && m.LongueurM >= 0.3,
                Id = m.Id, LongueurM = Math.Round(m.LongueurM, 3), HauteurM = Math.Round(m.ZHaut - m.ZBas, 3),
                EpaisseurMm = Math.Round((m.EpaisseurM ?? ctx.Parametres.EpaisseurHypotheseM) * 1000, 0),
                Epaisseur = m.EpaisseurMesuree ? "mesurée (2 faces)" : "HYPOTHÈSE (1 face) – à confirmer",
                Plombage = m.Plombage, DeversDeg = Math.Round(m.Devers.AngleDeg ?? double.NaN, 3),
                DeplacementTeteMm = Math.Round((m.Devers.DeplacementSommetM ?? double.NaN) * 1000, 1),
                Couverture = $"{m.Couverture.Ratio:P0}",
                Existant = dbl.element is null ? "" : $"déjà créé (Id {dbl.element.Id.Value})",
                Justification = m.Justification + (m.Devers.Incertain && !string.IsNullOrEmpty(m.Devers.Raison) ? " ; dévers incertain : " + m.Devers.Raison : ""),
                Prop = m,
            };
        }).ToList();

        var etat = ctx.Etat();
        var options = new List<OptionLot>
        {
            new() { Libelle = "Murs non plombés (E24)", Valeurs = [ModeVertical, ModeIncline] },
            new() { Libelle = "Axe du mur vertical", Valeurs = ["Mi-hauteur (écarts ± répartis)", "Pied de mur"] },
            new() { Libelle = "Régularisation (E08)", Valeurs = ["Suivre l'existant (aucune)", $"Orthogonaliser / Nord de travail {(etat.NordTravailDeg ?? 0):F2}° sous tolérance"] },
            new() { Libelle = "Contrainte haute", Valeurs = ["Hauteur mesurée (non contrainte)", "Niveau supérieur le plus proche (± 0,30 m)"] },
        };
        if (!FenetreValidation.Afficher(Titre, "Propositions de murs : cocher pour accepter ; épaisseur corrigible (✎). Une épaisseur « HYPOTHÈSE » n'est jamais présentée comme mesurée.",
                lignes,
                [new Colonne("Créer", nameof(LigneMur.Accepter), true, TypeColonne.Case), new Colonne("Id", nameof(LigneMur.Id)),
                 new Colonne("Longueur (m)", nameof(LigneMur.LongueurM)), new Colonne("Hauteur (m)", nameof(LigneMur.HauteurM)),
                 new Colonne("Épaisseur (mm)", nameof(LigneMur.EpaisseurMm), true), new Colonne("Origine épaisseur", nameof(LigneMur.Epaisseur)),
                 new Colonne("Plombage", nameof(LigneMur.Plombage)), new Colonne("Dévers (°)", nameof(LigneMur.DeversDeg)),
                 new Colonne("Dépl. tête (mm)", nameof(LigneMur.DeplacementTeteMm)), new Colonne("Couverture", nameof(LigneMur.Couverture)),
                 new Colonne("Relance", nameof(LigneMur.Existant)), new Colonne("Justification", nameof(LigneMur.Justification), Largeur: 320)],
                resumeCalcul, options, "Continuer"))
            return Result.Cancelled;

        var mode = options[0].Valeur == ModeIncline ? ModeMurNonPlombe.InclineFidele : ModeMurNonPlombe.VerticalAvecRapport;
        var reference = options[1].Valeur.StartsWith("Mi") ? ReferenceVerticale.MiHauteur : ReferenceVerticale.Base;
        bool regulariser = options[2].Valeur.StartsWith("Ortho");
        bool contrainte = options[3].Valeur.StartsWith("Niveau");
        var retenus = lignes.Where(l => l.Accepter).ToList();
        if (retenus.Count == 0) return Result.Cancelled;
        if (retenus.Any(l => l.EpaisseurMm <= 0)) throw new ArretCommande("Épaisseur nulle ou négative.");

        // ── Mesures → géométrie Revit selon le mode choisi ──────────────────
        var aCreer = retenus.Select(l =>
        {
            var m = l.Prop;
            var dir = m.Devers.DirectionInclinaison is { Length: 2 } d ? new Vecteur3(d[0], d[1], 0) : new Vecteur3(m.Normale[0], m.Normale[1], 0);
            var mesure = new MurMesure(m.Id, Vecteur3.DepuisTableau(m.Debut) with { Z = m.ZBas }, Vecteur3.DepuisTableau(m.Fin) with { Z = m.ZBas },
                m.ZBas, m.ZHaut, l.EpaisseurMm / 1000, Math.Abs(m.Devers.PenteMParM ?? 0), dir, m.Devers.Incertain, m.Plombage);
            return (ligne: l, mesure, cible: ConversionMurNonPlombe.Convertir(mesure, mode, reference, ctx.Parametres.TolerancePlombDeg));
        }).ToList();

        // ── E08 : régularisation, états mesuré et régularisé présentés ───────
        var regul = new Dictionary<string, ResultatRegularisation>();
        if (regulariser)
        {
            var res = RegularisationMurs.Orthogonaliser(aCreer.Select(x => new SegmentMur(x.cible.Id, x.cible.Debut, x.cible.Fin)),
                etat.NordTravailDeg ?? 0, ctx.Parametres.ToleranceRegularisationAngleDeg, ctx.Parametres.ToleranceRegularisationDeplacementM);
            var lr = res.Select(r => new LigneRegularisation { Accepter = r.DansTolerance && r.RotationDeg != 0, Id = r.Mesure.Id, RotationDeg = Math.Round(r.RotationDeg, 3), DeplacementMm = Math.Round(r.DeplacementMaxM * 1000, 1), Decision = r.Decision, Resultat = r }).ToList();
            if (!FenetreValidation.Afficher("BatiScan – Régularisation (E08)", "Régularisation : seules les lignes cochées passent à l'état régularisé ; les autres suivent l'existant mesuré. Hors tolérance = conservé tel que mesuré.",
                    lr, [new Colonne("Régulariser", nameof(LigneRegularisation.Accepter), true, TypeColonne.Case), new Colonne("Mur", nameof(LigneRegularisation.Id)),
                         new Colonne("Rotation (°)", nameof(LigneRegularisation.RotationDeg)), new Colonne("Déplacement max (mm)", nameof(LigneRegularisation.DeplacementMm)),
                         new Colonne("Décision", nameof(LigneRegularisation.Decision), Largeur: 420)],
                    $"Tolérances : {ctx.Parametres.ToleranceRegularisationAngleDeg}° et {ctx.Parametres.ToleranceRegularisationDeplacementM * 1000:F0} mm. Les éléments hébergés sont créés après (aucune porte/fenêtre BatiScan)."))
                return Result.Cancelled;
            foreach (var r in lr.Where(x => x.Accepter && x.Resultat.DansTolerance)) regul[r.Id] = r.Resultat;
        }

        ctx.VerifierEcriture("Création des murs");
        var niveaux = ctx.Niveaux();
        double zBase = Contexte.AltitudeInterneM(niveauBase);
        var creations = ctx.AppliquerLot("BatiScan – Création des murs (E07/E24)", () =>
        {
            var res = new List<(string id, long elementId, MurACreer cible, string typeNom, string remarque)>();
            foreach (var (ligne, mesure, cible0) in aCreer)
            {
                var cible = cible0;
                string remarque = cible.Remarque;
                if (regul.TryGetValue(cible.Id, out var rg))
                {
                    cible = cible with { Debut = rg.Regularise.Debut, Fin = rg.Regularise.Fin };
                    remarque += $" Régularisé : {rg.RotationDeg:F3}°, {rg.DeplacementMaxM * 1000:F1} mm.";
                }
                var type = TrouverOuCreerType(ctx.Doc, typeRef, ligne.EpaisseurMm / 1000, ctx.Parametres.ToleranceTypeEpaisseurM);
                var a = ConversionRevit.VersPieds(cible.Debut with { Z = zBase });
                var b = ConversionRevit.VersPieds(cible.Fin with { Z = zBase });
                var mur = Wall.Create(ctx.Doc, Line.CreateBound(a, b), type.Id, niveauBase.Id,
                    ConversionRevit.VersPieds(cible.Hauteur), ConversionRevit.VersPieds(cible.ZBas - zBase), false, false);
                if (!ligne.Prop.EpaisseurMesuree)
                {
                    // Face unique : la face observée est la face de finition ; côté matière inconnu.
                    mur.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.Set((int)WallLocationLine.FinishFaceExterior);
                    remarque += " Face unique : ligne de justification = nu fini extérieur sur la face observée ; côté matière à vérifier (retourner si besoin).";
                }
                if (contrainte)
                {
                    var haut = niveaux.Where(n => n.Id != niveauBase.Id).MinBy(n => Math.Abs(Contexte.AltitudeInterneM(n) - (cible.ZBas + cible.Hauteur)));
                    if (haut is not null && Math.Abs(Contexte.AltitudeInterneM(haut) - (cible.ZBas + cible.Hauteur)) <= 0.30)
                    {
                        mur.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(haut.Id);
                        mur.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.Set(ConversionRevit.VersPieds(cible.ZBas + cible.Hauteur - Contexte.AltitudeInterneM(haut)));
                    }
                }
                if (cible.Mode == ModeMurNonPlombe.InclineFidele && cible.AngleDepuisVerticaleDeg > 0)
                    remarque += AppliquerInclinaison(ctx.Doc, mur, cible, mesure.DirectionInclinaison);
                StockageBatiScan.EcrireProvenance(mur, new Provenance
                {
                    Categorie = "mur", Proposition = cible.Id, Operation = rep.IdRequete, Source = nuage.Cle,
                    Empreinte = nuage.EmpreinteContexte(StockageBatiScan.LireEtat(ctx.Doc)),
                    Mesures =
                    {
                        ["epaisseur_m"] = ligne.EpaisseurMm / 1000, ["epaisseur_mesuree"] = ligne.Prop.EpaisseurMesuree,
                        ["devers_deg"] = ligne.Prop.Devers.AngleDeg, ["deplacement_tete_m"] = ligne.Prop.Devers.DeplacementSommetM,
                        ["mode"] = cible.Mode.ToString(), ["ecart_pied_m"] = cible.EcartPiedM, ["ecart_tete_m"] = cible.EcartTeteM,
                        ["couverture"] = ligne.Prop.Couverture.Ratio, ["rms_m"] = ligne.Prop.RmsM,
                    },
                    Hypotheses = ligne.Prop.EpaisseurMesuree ? [] : [$"épaisseur supposée {ligne.EpaisseurMm:F0} mm (une seule face observée)"],
                });
                res.Add((cible.Id, mur.Id.Value, cible, type.Name, remarque));
            }
            var e = StockageBatiScan.LireEtat(ctx.Doc);
            e.Analyses.Add(new AnalyseEnregistree { Operation = "murs", CleSource = nuage.Cle, Resume = $"{res.Count} murs créés ({mode})" });
            StockageBatiScan.EcrireEtat(ctx.Doc, e);
            return res;
        });

        // ── Rapport de dévers (E24) ─────────────────────────────────────────
        var rapport = new RapportCsv("Murs et rapport de dévers (E07/E24)").Contexte("Nuage", nuage.Nom).Contexte("Zone", zone.Decrire())
            .Contexte("Mode non plombé", mode.ToString()).Contexte("Référence verticale", reference.ToString())
            .Contexte("Tolérance plomb (°)", RapportCsv.Formater(ctx.Parametres.TolerancePlombDeg)).Contexte("Régularisation", regulariser ? "oui" : "non")
            .Colonnes("Proposition", "Mur Revit (Id)", "Type", "Longueur (m)", "Hauteur (m)", "Épaisseur (mm)", "Épaisseur mesurée", "Plombage",
                      "Dévers mesuré (°)", "Déplacement tête (mm)", "Angle créé (°)", "Écart pied (mm)", "Écart tête (mm)", "Couverture", "Remarque");
        foreach (var c in creations)
        {
            var l = retenus.First(x => x.Id == c.id);
            rapport.Ligne(c.id, c.elementId, c.typeNom, l.LongueurM, l.HauteurM, l.EpaisseurMm, l.Prop.EpaisseurMesuree, l.Plombage, l.DeversDeg,
                l.DeplacementTeteMm, c.cible.AngleDepuisVerticaleDeg, c.cible.EcartPiedM * 1000, c.cible.EcartTeteM * 1000, l.Couverture, c.remarque);
        }
        string chemin = RapportCsv.CheminHorodate("murs-devers");
        rapport.Enregistrer(chemin);
        JournalLocal.Ecrire("murs.crees", new { nombre = creations.Count, mode = mode.ToString(), rapport = chemin });
        Contexte.Informer(Titre, $"{creations.Count} mur(s) créé(s)", $"Rapport de dévers : {chemin}\nLes murs restent éditables avec les outils natifs ; annulation par Ctrl+Z.");
        return Result.Succeeded;
    }

    /// <summary>
    /// Inclinaison unique (WallCrossSection.SingleSlanted). Le signe de l'angle est VÉRIFIÉ sur la
    /// géométrie (centroïde du solide) plutôt que supposé ; impossible ⇒ exception (rollback du lot).
    /// </summary>
    private static string AppliquerInclinaison(Document doc, Wall mur, MurACreer cible, Vecteur3 directionInclinaison)
    {
        if (!mur.IsWallCrossSectionValid(WallCrossSection.SingleSlanted))
            throw new InvalidOperationException($"Mur {cible.Id} : inclinaison unique non autorisée pour ce type ; choisir le mode vertical.");
        mur.CrossSection = WallCrossSection.SingleSlanted;
        var param = mur.get_Parameter(BuiltInParameter.WALL_SINGLE_SLANT_ANGLE_FROM_VERTICAL)
                    ?? throw new InvalidOperationException("Paramètre « Angle depuis la verticale » introuvable.");
        double angle = ConversionRevit.VersRad(cible.AngleDepuisVerticaleDeg);
        param.Set(angle);
        doc.Regenerate();
        var milieuPied = (cible.Debut + cible.Fin) / 2;
        double Decalage()
        {
            var opts = new Options { DetailLevel = ViewDetailLevel.Fine };
            var solide = mur.get_Geometry(opts).OfType<Solid>().Where(s => s.Volume > 0).MaxBy(s => s.Volume)
                         ?? throw new InvalidOperationException("Géométrie du mur incliné indisponible.");
            var c = ConversionRevit.VersM(solide.ComputeCentroid());
            return new Vecteur3(c.X - milieuPied.X, c.Y - milieuPied.Y, 0).Scalaire(directionInclinaison.Unitaire());
        }
        double d1 = Decalage();
        if (d1 < 0)
        {
            param.Set(-angle);
            doc.Regenerate();
            if (Decalage() < 0) throw new InvalidOperationException($"Mur {cible.Id} : sens d'inclinaison non vérifiable.");
        }
        return " Sens d'inclinaison vérifié sur la géométrie.";
    }

    /// <summary>E13 – Réutilise un type de même épaisseur ; sinon DUPLIQUE le type de référence (jamais de modification d'un type existant).</summary>
    internal static WallType TrouverOuCreerType(Document doc, WallType reference, double epaisseurM, double toleranceM)
    {
        if (Math.Abs(ConversionRevit.VersM(reference.Width) - epaisseurM) <= toleranceM) return reference;
        var existants = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().Where(w => w.Kind == WallKind.Basic).ToList();
        string nom = $"BatiScan – {reference.Name} – {epaisseurM * 1000:F0} mm";
        var equivalent = existants.FirstOrDefault(w => w.Name == nom)
                         ?? existants.FirstOrDefault(w => w.Name.StartsWith("BatiScan – ", StringComparison.Ordinal) && Math.Abs(ConversionRevit.VersM(w.Width) - epaisseurM) <= toleranceM);
        if (equivalent is not null) return equivalent;
        var nouveau = (WallType)reference.Duplicate(nom);
        var cs = nouveau.GetCompoundStructure() ?? throw new InvalidOperationException($"Type « {reference.Name} » sans structure composée.");
        var couches = cs.GetLayers();
        int noyau = cs.GetFirstCoreLayerIndex();
        if (noyau < 0 || noyau >= couches.Count) noyau = 0;
        double autres = couches.Where((_, i) => i != noyau).Sum(c => c.Width);
        double largeur = ConversionRevit.VersPieds(epaisseurM) - autres;
        if (largeur <= ConversionRevit.VersPieds(0.005))
            throw new InvalidOperationException($"Épaisseur {epaisseurM * 1000:F0} mm inférieure aux couches non porteuses du type « {reference.Name} ».");
        cs.SetLayerWidth(noyau, largeur);
        nouveau.SetCompoundStructure(cs);
        return nouveau;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  E12 – Poteaux et poutres
// ════════════════════════════════════════════════════════════════════════════

public sealed class LigneStructure
{
    public bool Accepter { get; set; } = true;
    public string Id { get; init; } = "";
    public string Element { get; init; } = "";
    public string Geometrie { get; init; } = "";
    public string Observe { get; init; } = "";
    public string Section { get; init; } = "";
    public string Justification { get; init; } = "";
    internal PropPoteau? Poteau { get; init; }
    internal PropPoutre? Poutre { get; init; }
}

/// <summary>
/// E12 – Détection et création de poteaux et poutres structurels à partir de familles
/// DÉJÀ chargées (E13). Seule la géométrie observée est rapportée : aucune section, matériau
/// ou qualité n'est affirmé, aucun calcul de structure. Catégorie, axe, rotation et niveaux
/// restent inspectables après création.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeStructure : CommandeBase
{
    protected override string Titre => "BatiScan – Poteaux et poutres (E12)";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var zone = ctx.ChoisirZone(nuage, "détection de la structure");
        var (rep, ext) = ctx.ExecuterMoteur("structure", nuage, zone, densiteM: 0.01);
        var poteaux = rep.Lire<PropPoteau>("poteau");
        var poutres = rep.Lire<PropPoutre>("poutre");
        string resumeCalcul = Contexte.ResumeCalcul(rep, ext, zone);
        if (poteaux.Count + poutres.Count == 0) throw new ArretCommande("Aucun poteau ni poutre proposé.\n\n" + resumeCalcul);

        var lignes = poteaux.Select(p => new LigneStructure
        {
            Id = p.Id, Element = "Poteau", Poteau = p,
            Geometrie = $"centre ({p.Centre[0]:F3} ; {p.Centre[1]:F3}), z {p.ZBas:F2} → {p.ZHaut:F2} m, rotation {p.RotationDeg:F1}°",
            Observe = $"{p.Encombrement.AM * 1000:F0} × {p.Encombrement.BM * 1000:F0} mm (encombrement)",
            Section = p.SectionObservee, Justification = p.Justification,
        }).Concat(poutres.Select(p => new LigneStructure
        {
            Id = p.Id, Element = "Poutre", Poutre = p,
            Geometrie = $"axe ({p.Debut[0]:F2} ; {p.Debut[1]:F2}) → ({p.Fin[0]:F2} ; {p.Fin[1]:F2}), dessus z {p.Debut[2]:F3} m",
            Observe = $"largeur {p.LargeurObserveeM * 1000:F0} mm, retombée {p.RetombeeObserveeM * 1000:F0} mm, L {p.LongueurObserveeM:F2} m",
            Section = "non affirmée", Justification = $"{p.Justification} ; hypothèse : {p.Hypothese}",
        })).ToList();

        if (!FenetreValidation.Afficher(Titre, "Propositions de structure (géométrie observée seulement ; le type Revit choisi ensuite n'est pas déduit du scan).", lignes,
                [new Colonne("Créer", nameof(LigneStructure.Accepter), true, TypeColonne.Case), new Colonne("Id", nameof(LigneStructure.Id)),
                 new Colonne("Élément", nameof(LigneStructure.Element)), new Colonne("Géométrie", nameof(LigneStructure.Geometrie), Largeur: 300),
                 new Colonne("Observé", nameof(LigneStructure.Observe), Largeur: 220), new Colonne("Section", nameof(LigneStructure.Section)),
                 new Colonne("Justification", nameof(LigneStructure.Justification), Largeur: 300)],
                resumeCalcul + "\nAucun calcul de stabilité ni dimensionnement n'est effectué (E12).", libelleAppliquer: "Continuer"))
            return Result.Cancelled;
        var retenus = lignes.Where(l => l.Accepter).ToList();
        if (retenus.Count == 0) return Result.Cancelled;

        FamilySymbol? symPoteau = null, symPoutre = null;
        if (retenus.Any(l => l.Poteau is not null)) symPoteau = ChoisirSymbole(ctx, BuiltInCategory.OST_StructuralColumns, "poteaux structurels");
        if (retenus.Any(l => l.Poutre is not null)) symPoutre = ChoisirSymbole(ctx, BuiltInCategory.OST_StructuralFraming, "poutres (ossature)");

        ctx.VerifierEcriture("Création poteaux/poutres");
        var niveaux = ctx.Niveaux();
        if (niveaux.Count == 0) throw new ArretCommande("Aucun niveau : créer d'abord les niveaux.");
        Level Dessous(double z) => niveaux.Where(n => Contexte.AltitudeInterneM(n) <= z + 0.05).MaxBy(Contexte.AltitudeInterneM) ?? niveaux[0];

        var crees = ctx.AppliquerLot("BatiScan – Poteaux et poutres (E12)", () =>
        {
            var res = new List<string>();
            if (symPoteau is { IsActive: false }) symPoteau.Activate();
            if (symPoutre is { IsActive: false }) symPoutre.Activate();
            ctx.Doc.Regenerate();
            foreach (var l in retenus)
            {
                FamilyInstance fi;
                if (l.Poteau is { } p && symPoteau is not null)
                {
                    var nb = Dessous(p.ZBas);
                    double zb = Contexte.AltitudeInterneM(nb);
                    var pt = ConversionRevit.VersPieds(new Vecteur3(p.Centre[0], p.Centre[1], zb));
                    fi = ctx.Doc.Create.NewFamilyInstance(pt, symPoteau, nb, StructuralType.Column);
                    fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(ConversionRevit.VersPieds(p.ZBas - zb));
                    var nh = niveaux.Where(n => n.Id != nb.Id).MinBy(n => Math.Abs(Contexte.AltitudeInterneM(n) - p.ZHaut));
                    if (nh is not null && Math.Abs(Contexte.AltitudeInterneM(nh) - p.ZHaut) <= 0.5)
                    {
                        fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(nh.Id);
                        fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(ConversionRevit.VersPieds(p.ZHaut - Contexte.AltitudeInterneM(nh)));
                    }
                    if (Math.Abs(p.RotationDeg) > 1e-6)
                        ElementTransformUtils.RotateElement(ctx.Doc, fi.Id, Line.CreateBound(pt, pt + XYZ.BasisZ), ConversionRevit.VersRad(p.RotationDeg));
                }
                else if (l.Poutre is { } b && symPoutre is not null)
                {
                    var nb = Dessous(b.Debut[2]);
                    var a = ConversionRevit.VersPieds(Vecteur3.DepuisTableau(b.Debut));
                    var c = ConversionRevit.VersPieds(Vecteur3.DepuisTableau(b.Fin));
                    fi = ctx.Doc.Create.NewFamilyInstance(Line.CreateBound(a, c), symPoutre, nb, StructuralType.Beam);
                }
                else continue;
                StockageBatiScan.EcrireProvenance(fi, new Provenance
                {
                    Categorie = l.Element.ToLowerInvariant(), Proposition = l.Id, Operation = rep.IdRequete, Source = nuage.Cle,
                    Mesures = { ["observe"] = l.Observe, ["section_observee"] = l.Section, ["geometrie"] = l.Geometrie },
                    Hypotheses = ["section et matériau non observés : type choisi par l'utilisateur", .. l.Poutre is null ? Array.Empty<string>() : [l.Poutre.Hypothese]],
                });
                res.Add($"{l.Element} {l.Id} → Id {fi.Id.Value}");
            }
            return res;
        });

        var rapport = new RapportCsv("Poteaux et poutres (E12)").Contexte("Nuage", nuage.Nom).Contexte("Zone", zone.Decrire())
            .Contexte("Famille poteau", symPoteau is null ? "–" : $"{symPoteau.FamilyName} : {symPoteau.Name}")
            .Contexte("Famille poutre", symPoutre is null ? "–" : $"{symPoutre.FamilyName} : {symPoutre.Name}")
            .Colonnes("Proposition", "Élément", "Géométrie", "Observé", "Section", "Créé", "Justification");
        foreach (var l in lignes) rapport.Ligne(l.Id, l.Element, l.Geometrie, l.Observe, l.Section, l.Accepter, l.Justification);
        string chemin = RapportCsv.CheminHorodate("structure");
        rapport.Enregistrer(chemin);
        Contexte.Informer(Titre, $"{crees.Count} élément(s) structurel(s) créé(s)", string.Join("\n", crees) + $"\n\nComparer la section du type choisi à l'encombrement observé (rapport) : {chemin}");
        return Result.Succeeded;
    }

    private static FamilySymbol ChoisirSymbole(Contexte ctx, BuiltInCategory cat, string libelle)
    {
        var symboles = new FilteredElementCollector(ctx.Doc).OfClass(typeof(FamilySymbol)).OfCategory(cat).Cast<FamilySymbol>()
            .OrderBy(s => s.FamilyName).ThenBy(s => s.Name).ToList();
        if (symboles.Count == 0)
            throw new ArretCommande($"Aucune famille de {libelle} chargée. Charger la famille requise (Insérer › Charger la famille), puis relancer : aucun objet partiel n'a été créé (E13).");
        return Dialogues.Choisir("BatiScan – Famille", $"Type de {libelle} à utiliser (la section du type n'est pas déduite du scan) :", symboles, s => $"{s.FamilyName} : {s.Name}")
               ?? throw new ArretCommande("Aucun type choisi : aucun objet créé.");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  E14 – Écarts et couverture
// ════════════════════════════════════════════════════════════════════════════

public sealed class LigneEcart
{
    public string Reference { get; init; } = "";
    public int Points { get; init; }
    public string Couverture { get; init; } = "";
    public string Statut { get; init; } = "";
    public string Moyenne { get; init; } = "";
    public string P95 { get; init; } = "";
    public string Max { get; init; } = "";
    public string HorsTolerance { get; init; } = "";
}

/// <summary>
/// E14 – Écarts entre le nuage et les faces des murs sélectionnés. Une face sans points est
/// « non observée », jamais conforme ; le statut est une estimation géométrique, pas une
/// certification. Résultat persistant, marqué « à recalculer » si la source change.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeEcarts : CommandeBase
{
    protected override string Titre => "BatiScan – Écarts et couverture (E14)";

    private sealed class FiltreMurs : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is Wall;
        public bool AllowReference(Reference r, XYZ p) => false;
    }

    protected override Result Executer(Contexte ctx)
    {
        var murs = ctx.UiDoc.Selection.GetElementIds().Select(id => ctx.Doc.GetElement(id)).OfType<Wall>().ToList();
        if (murs.Count == 0)
            murs = ctx.UiDoc.Selection.PickObjects(ObjectType.Element, new FiltreMurs(), "BatiScan : sélectionner les murs à contrôler puis Terminer")
                .Select(r => ctx.Doc.GetElement(r)).OfType<Wall>().ToList();
        if (murs.Count == 0) return Result.Cancelled;
        var nuage = ctx.ChoisirNuage();

        // Deux plus grandes faces planes quasi verticales de chaque mur (murs inclinés compris).
        var surfaces = new JsonArray();
        var mini = new Vecteur3(double.MaxValue, double.MaxValue, double.MaxValue);
        var maxi = new Vecteur3(double.MinValue, double.MinValue, double.MinValue);
        foreach (var m in murs)
        {
            var solide = m.get_Geometry(new Options { ComputeReferences = false }).OfType<Solid>().Where(s => s.Volume > 0).MaxBy(s => s.Volume);
            if (solide is null) continue;
            var faces = solide.Faces.OfType<PlanarFace>().Where(f => Math.Abs(f.FaceNormal.Z) < 0.5).OrderByDescending(f => f.Area).Take(2).ToList();
            foreach (var (f, k) in faces.Select((f, k) => (f, k)))
            {
                var bb = f.GetBoundingBox();
                var p00 = ConversionRevit.VersM(f.Evaluate(bb.Min));
                var p10 = ConversionRevit.VersM(f.Evaluate(new UV(bb.Max.U, bb.Min.V)));
                var p01 = ConversionRevit.VersM(f.Evaluate(new UV(bb.Min.U, bb.Max.V)));
                var u = p10 - p00; var v = p01 - p00;
                if (u.Vectoriel(v).Scalaire(ConversionRevit.Direction(f.FaceNormal)) < 0) (u, v, p10, p01) = (v, u, p01, p10);
                surfaces.Add(new JsonObject
                {
                    ["id"] = $"W{m.Id.Value}-F{k + 1}", ["reference"] = $"Mur {m.Id.Value} ({m.Name}) face {k + 1}",
                    ["origine"] = new JsonArray(p00.X, p00.Y, p00.Z), ["u"] = new JsonArray(u.X, u.Y, u.Z), ["v"] = new JsonArray(v.X, v.Y, v.Z),
                    ["longueur_u"] = u.Norme, ["longueur_v"] = v.Norme,
                });
                foreach (var p in new[] { p00, p10, p01, p00 + u + v })
                {
                    mini = new Vecteur3(Math.Min(mini.X, p.X), Math.Min(mini.Y, p.Y), Math.Min(mini.Z, p.Z));
                    maxi = new Vecteur3(Math.Max(maxi.X, p.X), Math.Max(maxi.Y, p.Y), Math.Max(maxi.Z, p.Z));
                }
            }
        }
        if (surfaces.Count == 0) throw new ArretCommande("Aucune face plane exploitable sur les murs sélectionnés.");
        var marge = new Vecteur3(0.3, 0.3, 0.3);
        var zone = new BoiteZone("Faces des murs sélectionnés", mini - marge, maxi + marge, 0);
        var (rep, ext) = ctx.ExecuterMoteur("ecarts", nuage, zone, new JsonObject { ["surfaces"] = surfaces, ["tolerance_m"] = 0.01 }, densiteM: 0.005);
        var ecarts = rep.Lire<PropEcart>("ecart");
        string Mm(double? v) => v is null ? "–" : $"{v * 1000:F1}";
        var lignes = ecarts.Select(e => new LigneEcart
        {
            Reference = e.Reference, Points = e.NombrePoints, Couverture = $"{e.Couverture.Ratio:P0}", Statut = e.Statut,
            Moyenne = Mm(e.MoyenneSigneeM), P95 = Mm(e.P95AbsoluM), Max = Mm(e.MaxAbsoluM),
            HorsTolerance = e.PartHorsTolerance is null ? "–" : $"{e.PartHorsTolerance:P0}",
        }).ToList();
        var rapport = new RapportCsv("Écarts et couverture (E14)").Contexte("Nuage", nuage.Nom).Contexte("Sens", "distance signée positive du côté de la normale extérieure de la face")
            .Contexte("Tolérance (m)", "0,01").Contexte("Avertissement", "estimation géométrique, pas une certification ; « non_observe » ≠ conforme")
            .Colonnes("Surface", "Points", "Couverture", "Statut", "Moyenne signée (mm)", "p95 |d| (mm)", "max |d| (mm)", "Part hors tolérance");
        foreach (var l in lignes) rapport.Ligne(l.Reference, l.Points, l.Couverture, l.Statut, l.Moyenne, l.P95, l.Max, l.HorsTolerance);
        string chemin = RapportCsv.CheminHorodate("ecarts");
        rapport.Enregistrer(chemin);
        ctx.VerifierEcriture("Enregistrer l'analyse d'écarts");
        StockageBatiScan.ModifierEtat(ctx.Doc, "BatiScan – Analyse d'écarts (E14)", e =>
            e.Analyses.Add(new AnalyseEnregistree { Operation = "ecarts", CleSource = nuage.Cle, CheminRapport = chemin, Resume = $"{lignes.Count} faces, {lignes.Count(l => l.Statut == "non_observe")} non observées" }));
        FenetreValidation.Afficher(Titre, "Écarts mesurés (lecture). Score de confiance, écart géométrique et tolérance projet sont des notions distinctes.", lignes,
            [new Colonne("Surface", nameof(LigneEcart.Reference), Largeur: 260), new Colonne("Points", nameof(LigneEcart.Points)), new Colonne("Couverture", nameof(LigneEcart.Couverture)),
             new Colonne("Statut", nameof(LigneEcart.Statut)), new Colonne("Moy. signée (mm)", nameof(LigneEcart.Moyenne)), new Colonne("p95 (mm)", nameof(LigneEcart.P95)),
             new Colonne("Max (mm)", nameof(LigneEcart.Max)), new Colonne("Hors tol.", nameof(LigneEcart.HorsTolerance))],
            Contexte.ResumeCalcul(rep, ext, zone) + $"\nRapport : {chemin}", libelleAppliquer: "Fermer");
        return Result.Succeeded;
    }
}
