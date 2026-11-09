using System.Text.Json.Nodes;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BatiScan.Core.Contrats;
using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Journal;
using BatiScan.Core.Niveaux;
using BatiScan.Core.Rapports;
using BatiScan.Infrastructure;
using BatiScan.Interface;

namespace BatiScan.Commandes;

// ════════════════════════════════════════════════════════════════════════════
//  E04 – Niveaux
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// E04 – Détecte les surfaces horizontales (moteur), présente les niveaux existants,
/// demande le zéro architectural ±0,00 et la convention, puis crée les niveaux VALIDÉS
/// (et vues en plan optionnelles) sans doublon. Le point le plus bas n'est pas présumé RDC.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeNiveaux : CommandeBase
{
    protected override string Titre => "BatiScan – Niveaux (E04)";

    private static readonly string[] Conventions = ["sol_fini", "structure", "sous_face"];

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var zone = ctx.ChoisirZone(nuage, "détection des niveaux");
        var (rep, ext) = ctx.ExecuterMoteur("niveaux", nuage, zone, densiteM: 0.02);
        var props = rep.Lire<PropNiveau>("niveau");
        string resumeCalcul = Contexte.ResumeCalcul(rep, ext, zone);
        if (props.Count == 0) throw new ArretCommande("Aucune surface horizontale candidate.\n\n" + resumeCalcul);

        var existants = ctx.Niveaux();
        // ── Zéro architectural : choix explicite ─────────────────────────────
        var libelles = props.Select(p => $"Candidat {p.Id} : z interne {p.AltitudeM:F3} m – {p.TypeSuppose} – {p.SurfaceObserveeM2:F1} m² – {p.Justification}").ToList();
        libelles.AddRange(existants.Select(l => $"Niveau existant « {l.Name} » : z interne {Contexte.AltitudeInterneM(l):F3} m"));
        int iz = Dialogues.ChoisirIndex(Titre, "Choisir la surface de référence ±0,00 (zéro architectural). Le point le plus bas n'est PAS présumé être le rez-de-chaussée.", libelles);
        if (iz < 0) return Result.Cancelled;
        double zero = iz < props.Count ? props[iz].AltitudeM : Contexte.AltitudeInterneM(existants[iz - props.Count]);
        int ic = Dialogues.ChoisirIndex(Titre, "Convention des niveaux créés :", ["Sol fini (SF)", "Structure / brut (BS)", "Sous-face (SsF)"]);
        if (ic < 0) return Result.Cancelled;

        var repere = RepereProjet.Lire(ctx.Doc);
        double? dzPartage = repere.Confirme ? repere.InterneVersPartage!.Translation.Z : null;
        var lignes = AssociationNiveaux.Associer(
            props.Select(p => new CandidatNiveau(p.Id, p.AltitudeM, p.TypeSuppose, p.Justification, p.NombrePoints, p.SurfaceObserveeM2)).ToList(),
            existants.Select(l => new NiveauExistant(l.Id.Value, l.Name, Contexte.AltitudeInterneM(l))).ToList(),
            zero, ctx.Parametres.ToleranceAssociationNiveauM, dzPartage, Conventions[ic]);

        var cols = new[]
        {
            new Colonne("Action", nameof(LigneNiveau.Action), true, TypeColonne.Liste, Enum.GetValues<ActionNiveau>()),
            new Colonne("Nom", nameof(LigneNiveau.NomPropose), true, Largeur: 170),
            new Colonne("Vue en plan", nameof(LigneNiveau.CreerVueEnPlan), true, TypeColonne.Case),
            new Colonne("Haut. relative (m)", nameof(LigneNiveau.HauteurRelativeM)),
            new Colonne("z interne (m)", nameof(LigneNiveau.AltitudeInterneM)),
            new Colonne("Altitude partagée (m)", nameof(LigneNiveau.AltitudePartageeM)),
            new Colonne("Type supposé", nameof(LigneNiveau.TypeSuppose)),
            new Colonne("Niveau existant", nameof(LigneNiveau.NiveauExistantNom)),
            new Colonne("Justification", nameof(LigneNiveau.Justification), Largeur: 300),
        };
        string resume = resumeCalcul + $"Zéro ±0,00 : z interne {zero:F3} m ; convention {Conventions[ic]} ; association aux niveaux existants à ±{ctx.Parametres.ToleranceAssociationNiveauM * 1000:F0} mm."
                      + (dzPartage is null ? "\nAltitudes partagées indisponibles (relation interne ↔ partagé non confirmée)." : "");
        if (!FenetreValidation.Afficher(Titre, "Récapitulatif avant création : « Creer » crée un niveau, « AssocierExistant » réutilise le niveau existant (pas de doublon), « Ignorer » écarte le candidat.", lignes, cols, resume))
            return Result.Cancelled;

        var aCreer = lignes.Where(l => l.Action == ActionNiveau.Creer).ToList();
        if (aCreer.Count == 0)
        {
            Contexte.Informer(Titre, "Aucun niveau à créer", "Associations retenues uniquement ; le document n'est pas modifié.");
            return Result.Succeeded;
        }
        ctx.VerifierEcriture("Création des niveaux");
        var noms = new HashSet<string>(existants.Select(l => l.Name));
        var typeVue = new FilteredElementCollector(ctx.Doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);
        var crees = ctx.AppliquerLot("BatiScan – Création des niveaux (E04)", () =>
        {
            var res = new List<string>();
            foreach (var l in aCreer)
            {
                double cible = ConversionRevit.VersPieds(l.AltitudeInterneM);
                Level niv = Level.Create(ctx.Doc, cible);
                ctx.Doc.Regenerate();
                // Contrôle de la convention d'élévation (Elevation vs ProjectElevation) : jamais supposée.
                double ecart = cible - niv.ProjectElevation;
                if (Math.Abs(ecart) > 1e-7)
                {
                    niv.Elevation += ecart;
                    ctx.Doc.Regenerate();
                    if (Math.Abs(cible - niv.ProjectElevation) > 1e-7)
                        throw new InvalidOperationException("Élévation du niveau créé non conforme à l'altitude validée.");
                }
                string nom = AssociationNiveaux.NomUnique(l.NomPropose, noms);
                niv.Name = nom;
                noms.Add(nom);
                StockageBatiScan.EcrireProvenance(niv, new Provenance
                {
                    Categorie = "niveau", Proposition = l.CandidatId, Operation = rep.IdRequete, Source = nuage.Cle,
                    Empreinte = nuage.EmpreinteContexte(StockageBatiScan.LireEtat(ctx.Doc)),
                    Mesures = { ["altitude_interne_m"] = l.AltitudeInterneM, ["hauteur_relative_m"] = l.HauteurRelativeM, ["altitude_partagee_m"] = l.AltitudePartageeM, ["type_suppose"] = l.TypeSuppose, ["convention"] = Conventions[ic] },
                    Hypotheses = { $"zéro architectural à z interne {zero:F3} m" },
                });
                if (l.CreerVueEnPlan && typeVue is not null) ViewPlan.Create(ctx.Doc, typeVue.Id, niv.Id);
                res.Add($"{nom} (z interne {l.AltitudeInterneM:F3} m)");
            }
            return res;
        });

        var rapport = new RapportCsv("Niveaux (E04)").Contexte("Nuage", nuage.Nom).Contexte("Zone", zone.Decrire()).Contexte("Zéro (z interne m)", RapportCsv.Formater(zero))
            .Contexte("Convention", Conventions[ic]).Contexte("Moteur", $"{rep.Moteur?.Version} / Open3D {rep.Moteur?.Open3d}")
            .Colonnes("Candidat", "Action", "Nom", "z interne (m)", "Hauteur relative (m)", "Altitude partagée (m)", "Type supposé", "Niveau existant", "Écart existant (m)", "Justification");
        foreach (var l in lignes) rapport.Ligne(l.CandidatId, l.Action, l.NomPropose, l.AltitudeInterneM, l.HauteurRelativeM, l.AltitudePartageeM, l.TypeSuppose, l.NiveauExistantNom, l.EcartNiveauExistantM, l.Justification);
        string chemin = RapportCsv.CheminHorodate("niveaux");
        rapport.Enregistrer(chemin);
        Contexte.Informer(Titre, $"{crees.Count} niveau(x) créé(s)", string.Join("\n", crees) + $"\n\nRapport : {chemin}");
        return Result.Succeeded;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  E05 / E24 – Coupes multiples et projection
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Comparaison de deux traces d'un même mur (E24 « Dévers »).</summary>
public sealed class LigneDevers
{
    public string Segment { get; init; } = "";
    public double ZBas { get; init; }
    public double ZHaut { get; init; }
    public double DecalageMm { get; init; }
    public double DeversDeg { get; init; }
    public double RecouvrementM { get; init; }
    public string Statut { get; init; } = "";
}

/// <summary>
/// E05 / E24 – Traces de coupe à plusieurs altitudes (chacune conservée, jamais écrasée),
/// mesure du dévers entre traces, projection contrôlée sur un plan EXPLICITE et plans de
/// référence. Trois représentations distinctes : points mesurés (nuage), traces mesurées
/// (sous-catégorie « BatiScan – Traces mesurées »), géométrie projetée (« BatiScan – Projections »).
/// Le nuage source n'est jamais modifié.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandeCoupes : CommandeBase
{
    protected override string Titre => "BatiScan – Coupes multiples (E24)";

    protected override Result Executer(Contexte ctx)
    {
        var nuage = ctx.ChoisirNuage();
        var zone = ctx.ChoisirZone(nuage, "coupes multiples");
        var niveau = ctx.ChoisirNiveau("Niveau de référence des altitudes de coupe :", zone.Min.Z);
        double z0 = Contexte.AltitudeInterneM(niveau);
        double[]? rel = Dialogues.SaisirNombres(Titre, $"Altitudes de coupe relatives à « {niveau.Name} » (m), séparées par « ; » :", "0,30 ; 1,20 ; 2,20");
        if (rel is null || rel.Length == 0) return Result.Cancelled;
        double[]? ep = Dialogues.SaisirNombres(Titre, "Épaisseur de chaque tranche (m) :", "0,02");
        if (ep is null || ep.Length != 1 || ep[0] <= 0) throw new ArretCommande("Épaisseur invalide.");
        var altitudes = rel.Select(r => z0 + r).OrderBy(z => z).ToArray();

        var param = new JsonObject { ["altitudes_m"] = new JsonArray(altitudes.Select(a => (JsonNode)a).ToArray()), ["epaisseur_m"] = ep[0] };
        var (rep, ext) = ctx.ExecuterMoteur("coupes", nuage, zone, param, densiteM: 0.005);
        var traces = rep.Lire<PropTrace>("trace");

        // ── Dévers entre la trace basse et les suivantes ─────────────────────
        var lignes = new List<LigneDevers>();
        var basse = traces.FirstOrDefault(t => t.Segments.Count > 0);
        if (basse is not null)
            foreach (var (s, i) in basse.Segments.Select((s, i) => (s, i)))
                foreach (var autre in traces.Where(t => t != basse))
                {
                    var a1 = Vecteur3.DepuisTableau(s.Debut) with { Z = basse.Z };
                    var b1 = Vecteur3.DepuisTableau(s.Fin) with { Z = basse.Z };
                    LigneDevers? meilleure = null;
                    foreach (var s2 in autre.Segments)
                    {
                        try
                        {
                            var r = Projection.DecalageEntreTraces(a1, b1, Vecteur3.DepuisTableau(s2.Debut) with { Z = autre.Z }, Vecteur3.DepuisTableau(s2.Fin) with { Z = autre.Z });
                            if (Math.Abs(r.DecalageM) > 0.30) continue;
                            if (meilleure is null || Math.Abs(r.DecalageM * 1000) < Math.Abs(meilleure.DecalageMm))
                                meilleure = new LigneDevers
                                {
                                    Segment = $"{basse.Id}-S{i + 1}", ZBas = basse.Z - z0, ZHaut = autre.Z - z0, DecalageMm = Math.Round(r.DecalageM * 1000, 1),
                                    DeversDeg = Math.Round(r.DeversDeg, 3), RecouvrementM = Math.Round(r.RecouvrementM, 2),
                                    Statut = r.RecouvrementM < 0.5 ? "recouvrement faible : incertain" : "mesuré",
                                };
                        }
                        catch (InvalidOperationException) { /* traces non comparables */ }
                    }
                    if (meilleure is not null) lignes.Add(meilleure);
                }

        var options = new List<OptionLot>
        {
            new() { Libelle = "Traces mesurées", Valeurs = ["Dessiner à leur altitude réelle", "Ne pas dessiner"] },
            new() { Libelle = "Projection", Valeurs = ["Aucune", $"Sur le plan horizontal de « {niveau.Name} » (direction verticale)"] },
            new() { Libelle = "Plans de référence", Valeurs = ["Aucun", "Plan vertical par segment de la trace basse (vue en plan active)"] },
        };
        string resume = Contexte.ResumeCalcul(rep, ext, zone)
            + string.Join("\n", traces.Select(t => $"{t.Id} : z = {t.Z - z0:+0.000;−0.000} m / {niveau.Name} – {t.Segments.Count} segment(s), {t.NombrePoints} points"));
        if (!FenetreValidation.Afficher(Titre, "Dévers mesurés entre la trace basse et les autres altitudes (décalage positif à gauche du segment bas). Choisir les représentations à créer.",
                lignes, [new Colonne("Segment", nameof(LigneDevers.Segment)), new Colonne("z bas (m)", nameof(LigneDevers.ZBas)), new Colonne("z haut (m)", nameof(LigneDevers.ZHaut)),
                         new Colonne("Décalage (mm)", nameof(LigneDevers.DecalageMm)), new Colonne("Dévers (°)", nameof(LigneDevers.DeversDeg)),
                         new Colonne("Recouvrement (m)", nameof(LigneDevers.RecouvrementM)), new Colonne("Statut", nameof(LigneDevers.Statut))],
                resume, options, "Créer les représentations"))
            return Result.Cancelled;

        bool dessiner = options[0].Valeur.StartsWith("Dessiner");
        bool projeter = !options[1].Valeur.StartsWith("Aucune");
        bool plansRef = !options[2].Valeur.StartsWith("Aucun");
        if (plansRef && ctx.Doc.ActiveView is not ViewPlan) throw new ArretCommande("Plans de référence : activer une vue en plan.");
        ctx.VerifierEcriture("Traces de coupe");
        string nomCoupe = $"Coupe {DateTime.Now:yyyyMMdd-HHmm} ({zone.Nom})";
        int n = ctx.AppliquerLot("BatiScan – Traces de coupe et projection (E24)", () =>
        {
            int nb = 0;
            var styleTrace = StyleLigne(ctx.Doc, "BatiScan – Traces mesurées", new Color(0, 112, 192));
            var styleProj = StyleLigne(ctx.Doc, "BatiScan – Projections", new Color(230, 120, 0));
            double courtMin = ctx.UiApp.Application.ShortCurveTolerance;
            var planCible = Plan3.Horizontal(z0);
            var plans = new Dictionary<double, SketchPlane>(); // un plan d'esquisse par altitude
            foreach (var t in traces)
            {
                foreach (var (s, i) in t.Segments.Select((s, i) => (s, i)))
                {
                    var a = Vecteur3.DepuisTableau(s.Debut) with { Z = t.Z };
                    var b = Vecteur3.DepuisTableau(s.Fin) with { Z = t.Z };
                    if (ConversionRevit.VersPieds(a.Distance(b)) < courtMin) continue;
                    if (dessiner)
                    {
                        Courbe(ctx.Doc, plans, a, b, t.Z, styleTrace, $"Trace mesurée {t.Id}-S{i + 1} z={t.Z - z0:+0.000} m / {niveau.Name} – {nomCoupe}");
                        nb++;
                    }
                    if (projeter)
                    {
                        // Géométrie DÉRIVÉE : nouvelle courbe, la trace et le nuage restent inchangés.
                        var pa = Projection.SurPlan(a, planCible, Vecteur3.AxeZ);
                        var pb = Projection.SurPlan(b, planCible, Vecteur3.AxeZ);
                        Courbe(ctx.Doc, plans, pa, pb, z0, styleProj, $"Projection verticale de {t.Id}-S{i + 1} (z={t.Z - z0:+0.000} m) sur {niveau.Name} – {nomCoupe}");
                        nb++;
                    }
                    if (plansRef && t == basse)
                    {
                        var rp = ctx.Doc.Create.NewReferencePlane(ConversionRevit.VersPieds(a), ConversionRevit.VersPieds(b), XYZ.BasisZ, ctx.Doc.ActiveView);
                        rp.Name = $"BatiScan {t.Id}-S{i + 1} {DateTime.Now:HHmmss}";
                        nb++;
                    }
                }
            }
            var e = StockageBatiScan.LireEtat(ctx.Doc);
            e.Coupes.Add(new CoupeEnregistree { Nom = nomCoupe, Zone = zone.Nom, AltitudesM = altitudes, EpaisseurM = ep[0], NiveauReference = niveau.Name, CleSource = nuage.Cle });
            e.Analyses.Add(new AnalyseEnregistree { Operation = "coupes", CleSource = nuage.Cle, Resume = $"{traces.Count} traces, {lignes.Count} dévers mesurés" });
            StockageBatiScan.EcrireEtat(ctx.Doc, e);
            return nb;
        });

        var rapport = new RapportCsv("Coupes multiples et dévers (E24)").Contexte("Nuage", nuage.Nom).Contexte("Zone", zone.Decrire())
            .Contexte("Niveau de référence", $"{niveau.Name} (z interne {z0:F3} m)").Contexte("Épaisseur de tranche (m)", RapportCsv.Formater(ep[0]))
            .Contexte("Plan de projection", projeter ? $"horizontal z={z0:F3} m, direction verticale" : "aucun")
            .Colonnes("Segment", "z bas relatif (m)", "z haut relatif (m)", "Décalage (mm)", "Dévers (°)", "Recouvrement (m)", "Statut");
        foreach (var l in lignes) rapport.Ligne(l.Segment, l.ZBas, l.ZHaut, l.DecalageMm, l.DeversDeg, l.RecouvrementM, l.Statut);
        string chemin = RapportCsv.CheminHorodate("coupes");
        rapport.Enregistrer(chemin);
        JournalLocal.Ecrire("coupes.creees", new { nomCoupe, elements = n, traces = traces.Count, devers = lignes.Count, rapport = chemin });
        Contexte.Informer(Titre, $"{n} élément(s) créé(s)", $"Coupe « {nomCoupe} » enregistrée (rappelable).\nRapport : {chemin}");
        return Result.Succeeded;
    }

    internal static GraphicsStyle StyleLigne(Document doc, string nom, Color couleur)
    {
        Category lignes = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        Category sc = lignes.SubCategories.Contains(nom) ? lignes.SubCategories.get_Item(nom) : doc.Settings.Categories.NewSubcategory(lignes, nom);
        if (!sc.LineColor.IsValid || sc.LineColor.Red != couleur.Red || sc.LineColor.Green != couleur.Green || sc.LineColor.Blue != couleur.Blue)
            sc.LineColor = couleur;
        return sc.GetGraphicsStyle(GraphicsStyleType.Projection);
    }

    internal static void Courbe(Document doc, Dictionary<double, SketchPlane> plans, Vecteur3 a, Vecteur3 b, double z, GraphicsStyle style, string commentaire)
    {
        if (!plans.TryGetValue(z, out var sp))
            plans[z] = sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, ConversionRevit.VersPieds(z))));
        var mc = doc.Create.NewModelCurve(Line.CreateBound(ConversionRevit.VersPieds(a), ConversionRevit.VersPieds(b)), sp);
        mc.LineStyle = style;
        mc.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(commentaire);
    }
}
