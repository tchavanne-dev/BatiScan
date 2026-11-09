using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using BatiScan.Core.Etat;

namespace BatiScan.Infrastructure;

/// <summary>Provenance d'un élément créé par BatiScan (E07, E21 : relance sans doublon).</summary>
public sealed class Provenance
{
    [JsonPropertyName("categorie")] public string Categorie { get; set; } = "";
    [JsonPropertyName("proposition")] public string Proposition { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("empreinte")] public string Empreinte { get; set; } = "";
    [JsonPropertyName("date")] public DateTime Date { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("version")] public string Version { get; set; } = Core.Journal.JournalLocal.VersionPlugin;
    [JsonPropertyName("mesures")] public Dictionary<string, object?> Mesures { get; set; } = [];
    [JsonPropertyName("hypotheses")] public List<string> Hypotheses { get; set; } = [];
}

/// <summary>
/// Stockage extensible BatiScan : un DataStorage pour l'état projet, une entité par
/// élément créé (provenance). Accès en écriture réservé au fournisseur BATISCAN
/// (cf. VendorId du manifeste). Écriture uniquement dans une transaction ouverte.
/// </summary>
public static class StockageBatiScan
{
    private static readonly Guid GuidEtat = new("8f1d3c52-6b0e-4a8b-9a4e-2f7c1d9e5b01");
    private static readonly Guid GuidProvenance = new("8f1d3c52-6b0e-4a8b-9a4e-2f7c1d9e5b02");
    private const string Vendeur = "BATISCAN";
    private const string Champ = "Json";

    // ── Schémas ─────────────────────────────────────────────────────────────

    private static Schema SchemaEtat() => Schema.Lookup(GuidEtat) ?? Construire(GuidEtat, "BatiScanEtatProjet", "État du projet BatiScan (zones, révisions de scan, Nord de travail)");
    private static Schema SchemaProvenance() => Schema.Lookup(GuidProvenance) ?? Construire(GuidProvenance, "BatiScanProvenance", "Provenance d'un élément créé par BatiScan");

    private static Schema Construire(Guid guid, string nom, string doc)
    {
        var b = new SchemaBuilder(guid);
        b.SetSchemaName(nom);
        b.SetDocumentation(doc);
        b.SetVendorId(Vendeur);
        b.SetReadAccessLevel(AccessLevel.Public);
        b.SetWriteAccessLevel(AccessLevel.Vendor);
        b.AddSimpleField(Champ, typeof(string));
        return b.Finish();
    }

    // ── État projet ─────────────────────────────────────────────────────────

    private static DataStorage? TrouverStockage(Document doc)
    {
        var s = Schema.Lookup(GuidEtat);
        if (s is null) return null;
        return new FilteredElementCollector(doc)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .FirstOrDefault(d => d.GetEntity(s).IsValid());
    }

    public static EtatProjet LireEtat(Document doc)
    {
        var ds = TrouverStockage(doc);
        if (ds is null) return new EtatProjet();
        var e = ds.GetEntity(SchemaEtat());
        return EtatProjet.DepuisJson(e.Get<string>(Champ));
    }

    /// <summary>Écrit l'état ; à appeler DANS une transaction ouverte.</summary>
    public static void EcrireEtat(Document doc, EtatProjet etat)
    {
        var schema = SchemaEtat();
        var ds = TrouverStockage(doc) ?? DataStorage.Create(doc);
        var e = new Entity(schema);
        e.Set(Champ, etat.VersJson());
        ds.SetEntity(e);
    }

    /// <summary>Lit, modifie et réécrit l'état dans une transaction nommée dédiée.</summary>
    public static void ModifierEtat(Document doc, string nomTransaction, Action<EtatProjet> modification)
    {
        var etat = LireEtat(doc);
        modification(etat);
        using var t = new Transaction(doc, nomTransaction);
        t.Start();
        EcrireEtat(doc, etat);
        t.Commit();
    }

    /// <summary>E21 – Purge : supprime UNIQUEMENT le DataStorage BatiScan. Retourne le nombre d'éléments supprimés.</summary>
    public static int PurgerEtat(Document doc)
    {
        var ds = TrouverStockage(doc);
        if (ds is null) return 0;
        doc.Delete(ds.Id);
        return 1;
    }

    // ── Provenance ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static void EcrireProvenance(Element el, Provenance p)
    {
        var e = new Entity(SchemaProvenance());
        e.Set(Champ, JsonSerializer.Serialize(p, Json));
        el.SetEntity(e);
    }

    public static Provenance? LireProvenance(Element el)
    {
        var s = Schema.Lookup(GuidProvenance);
        if (s is null) return null;
        var e = el.GetEntity(s);
        if (e is null || !e.IsValid()) return null;
        try { return JsonSerializer.Deserialize<Provenance>(e.Get<string>(Champ), Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Éléments créés par BatiScan, d'une classe donnée, avec leur provenance.</summary>
    public static List<(T element, Provenance provenance)> ElementsBatiScan<T>(Document doc) where T : Element
    {
        var res = new List<(T, Provenance)>();
        if (Schema.Lookup(GuidProvenance) is null) return res;
        foreach (T el in new FilteredElementCollector(doc).OfClass(typeof(T)).WhereElementIsNotElementType().Cast<T>())
            if (LireProvenance(el) is { } p) res.Add((el, p));
        return res;
    }
}
