using Autodesk.Revit.DB;
using Autodesk.Revit.DB.PointClouds;
using BatiScan.Core.Etat;
using BatiScan.Core.Geometrie;
using BatiScan.Core.Scan;

namespace BatiScan.Infrastructure;

/// <summary>Mode de coordonnées des points retournés par <c>GetPoints</c>, déterminé à l'exécution.</summary>
public enum RepereGetPoints { Inconnu, Modele, LocalNuage }

/// <summary>
/// E01 – Nuage lié et sa source : instance, type, chemin, transformation, clé de source.
/// </summary>
public sealed class NuageSource
{
    public PointCloudInstance Instance { get; }
    public PointCloudType Type { get; }
    public string Nom { get; }
    public string Chemin { get; }
    /// <summary>Clé stable de la source dans l'état BatiScan (UniqueId du type).</summary>
    public string Cle => Type.UniqueId;
    public TransformationRigide TransformationInstance { get; }

    /// <summary>Mémorisé par source pour la session (déterminé par <see cref="ExtractionPoints"/>).</summary>
    internal static readonly Dictionary<string, RepereGetPoints> RepereDetecte = [];

    private NuageSource(Document doc, PointCloudInstance inst)
    {
        Instance = inst;
        Type = (PointCloudType)doc.GetElement(inst.GetTypeId());
        Nom = string.IsNullOrWhiteSpace(inst.Name) ? Type.Name : inst.Name;
        Chemin = LireChemin(Type);
        TransformationInstance = ConversionRevit.VersM(inst.GetTransform());
    }

    public static string LireChemin(PointCloudType type)
    {
        // GetPath() retourne un ModelPath (API 2027), converti en chemin lisible.
        try
        {
            ModelPath? mp = type.GetPath();
            return mp is null ? "" : ModelPathUtils.ConvertModelPathToUserVisiblePath(mp) ?? "";
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException) { return ""; }
    }

    public static List<NuageSource> Tous(Document doc) =>
        new FilteredElementCollector(doc)
            .OfClass(typeof(PointCloudInstance))
            .WhereElementIsNotElementType()
            .Cast<PointCloudInstance>()
            .Select(i => new NuageSource(doc, i))
            .ToList();

    public static NuageSource Depuis(Document doc, PointCloudInstance inst) => new(doc, inst);

    /// <summary>Clé de contexte d'un résultat : source + contenu + transformation (invalidation E05/E22/E23).</summary>
    public string EmpreinteContexte(EtatProjet etat)
    {
        var rev = etat.DerniereRevision(Cle);
        string contenu = rev?.Empreinte.IdentiteContenu ?? $"non-enregistre:{Chemin}";
        return $"{Cle}|{contenu}|{TransformationInstance.Empreinte()}";
    }

    /// <summary>Statut de fichier lisible (E01 : jamais « géoréférencement vérifié » sans preuve).</summary>
    public string StatutFichier()
    {
        if (string.IsNullOrEmpty(Chemin)) return "chemin non disponible";
        return File.Exists(Chemin) ? "accessible" : "INTROUVABLE";
    }

    public EmpreinteSource CalculerEmpreinte() => AnalyseRevisionScan.Calculer(Chemin);
}
