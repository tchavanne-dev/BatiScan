using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BatiScan.Core.Contrats;
using BatiScan.Core.Geometrie;

namespace BatiScan.Core.Moteur;

/// <summary>Disponibilité du moteur Python (E21 : le plugin reste utilisable sans lui).</summary>
public sealed record EtatMoteur(bool Disponible, string Message, string? Python, string? DossierEngine, InfoMoteur? Info);

/// <summary>
/// Client du moteur CPython externe. Lance <c>venv\Scripts\python.exe -m batiscan_engine</c>
/// en PROCESSUS SÉPARÉ (jamais dans Revit), échange par fichiers JSON du contrat 1.0.0.
/// <para>
/// Aucune méthode de cette classe n'appelle l'API Revit : elle peut tourner sur un thread
/// d'arrière-plan pendant que Revit affiche la progression.
/// </para>
/// </summary>
public sealed class ClientMoteur
{
    private readonly string? _racineConfiguree;
    private EtatMoteur? _etat;

    public ClientMoteur(string? racineConfiguree) => _racineConfiguree = string.IsNullOrWhiteSpace(racineConfiguree) ? null : racineConfiguree;

    public static string DossierTravailRacine =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatiScan", "travail");

    // ── Localisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Cherche la racine contenant <c>venv\Scripts\python.exe</c> et <c>engine\batiscan_engine</c> :
    /// paramètre utilisateur, variable BATISCAN_RACINE, puis dossiers parents de l'assembly.
    /// </summary>
    public string? TrouverRacine()
    {
        var candidats = new List<string>();
        if (_racineConfiguree is not null) candidats.Add(_racineConfiguree);
        if (Environment.GetEnvironmentVariable("BATISCAN_RACINE") is { Length: > 0 } env) candidats.Add(env);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? asm = Path.GetDirectoryName(typeof(ClientMoteur).Assembly.Location);
        if (asm is not null) dir = new DirectoryInfo(asm);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent) candidats.Add(dir.FullName);
        return candidats.FirstOrDefault(EstRacineValide);
    }

    private static bool EstRacineValide(string r) =>
        File.Exists(Path.Combine(r, "venv", "Scripts", "python.exe"))
        && Directory.Exists(Path.Combine(r, "engine", "batiscan_engine"));

    /// <summary>Vérifie la disponibilité (diagnostic rapide, mis en cache).</summary>
    public EtatMoteur VerifierDisponibilite(bool forcer = false)
    {
        if (_etat is not null && !forcer) return _etat;
        string? racine = TrouverRacine();
        if (racine is null)
            return _etat = new EtatMoteur(false,
                "Moteur Python introuvable (venv\\Scripts\\python.exe + engine\\). Renseigner « RacineMoteur » dans les paramètres ou BATISCAN_RACINE. "
                + "Les fonctions de détection sont indisponibles ; les autres fonctions restent utilisables.", null, null, null);
        string python = Path.Combine(racine, "venv", "Scripts", "python.exe");
        string engine = Path.Combine(racine, "engine");
        try
        {
            var (code, sortie, erreur) = Lancer(python, engine, ["-m", "batiscan_engine", "--diagnostic"], TimeSpan.FromSeconds(60), null, CancellationToken.None);
            if (code != 0) return _etat = new EtatMoteur(false, $"Moteur en erreur (code {code}) : {erreur.Trim()}", python, engine, null);
            string ligne = sortie.Split('\n').Last(l => l.TrimStart().StartsWith('{'));
            using var doc = JsonDocument.Parse(ligne);
            var info = doc.RootElement.GetProperty("moteur").Deserialize<InfoMoteur>(Contrat.Json);
            string version = doc.RootElement.GetProperty("contract_version").GetString() ?? "";
            if (version != Contrat.Version)
                return _etat = new EtatMoteur(false, $"Contrat moteur {version} ≠ plugin {Contrat.Version}.", python, engine, info);
            return _etat = new EtatMoteur(true, $"Moteur {info?.Version} – Open3D {info?.Open3d} – Python {info?.Python}", python, engine, info);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return _etat = new EtatMoteur(false, $"Moteur non lancé : {e.Message}", python, engine, null);
        }
    }

    // ── Exécution ───────────────────────────────────────────────────────────

    /// <summary>
    /// Écrit les points et la requête, lance le moteur, lit la réponse.
    /// Le dossier de travail est conservé (traçabilité) ; il ne contient que des extraits de zone.
    /// </summary>
    public ReponseMoteur Executer(
        RequeteMoteur requete,
        IReadOnlyList<Vecteur3>? points,
        TimeSpan delai,
        IProgress<(double, string)>? progression,
        CancellationToken annulation)
    {
        var etat = VerifierDisponibilite();
        if (!etat.Disponible) throw new InvalidOperationException(etat.Message);
        string dossier = Path.Combine(DossierTravailRacine, $"{DateTime.Now:yyyyMMdd-HHmmss}-{requete.Operation}-{requete.IdRequete[..8]}");
        Directory.CreateDirectory(dossier);
        if (points is not null)
        {
            var origine = EcrirePointsF32(Path.Combine(dossier, "points.bin"), points);
            requete.Points.Chemin = "points.bin";
            requete.Points.Format = "xyz_f32_le";
            requete.Points.Nombre = points.Count;
            requete.Points.OrigineM = origine.VersTableau();
        }
        string chemReq = Path.Combine(dossier, "requete.json");
        string chemRep = Path.Combine(dossier, "reponse.json");
        File.WriteAllText(chemReq, JsonSerializer.Serialize(requete, Contrat.Json), new UTF8Encoding(false));

        int code; string erreur;
        try
        {
            (code, _, erreur) = Lancer(etat.Python!, etat.DossierEngine!, ["-m", "batiscan_engine", chemReq, chemRep], delai, progression, annulation);
        }
        finally
        {
            // Extrait de scan (données privées) : supprimé après calcul ; requête/réponse conservées (traçabilité).
            if (points is not null && Environment.GetEnvironmentVariable("BATISCAN_CONSERVER_POINTS") != "1")
                try { File.Delete(Path.Combine(dossier, "points.bin")); } catch (IOException) { }
            PurgerAnciensDossiers(50);
        }
        if (!File.Exists(chemRep))
            throw new InvalidOperationException($"Le moteur n'a pas produit de réponse (code {code}). {erreur.Trim()}");
        var rep = JsonSerializer.Deserialize<ReponseMoteur>(File.ReadAllText(chemRep, Encoding.UTF8), Contrat.Json)
                  ?? throw new InvalidOperationException("Réponse moteur vide.");
        if (rep.VersionContrat != Contrat.Version)
            throw new InvalidOperationException($"Réponse au contrat {rep.VersionContrat}, attendu {Contrat.Version}.");
        if (rep.IdRequete != requete.IdRequete)
            throw new InvalidOperationException("Réponse ne correspondant pas à la requête (id différent).");
        return rep;
    }

    /// <summary>Ne conserve que les <paramref name="garder"/> dossiers de travail les plus récents.</summary>
    private static void PurgerAnciensDossiers(int garder)
    {
        try
        {
            foreach (var d in new DirectoryInfo(DossierTravailRacine).GetDirectories().OrderByDescending(d => d.CreationTimeUtc).Skip(garder))
                d.Delete(recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Écrit les points en float32 little-endian RELATIFS à une origine (premier point arrondi au mètre),
    /// pour conserver la précision millimétrique loin de l'origine. Retourne l'origine.
    /// </summary>
    public static Vecteur3 EcrirePointsF32(string chemin, IReadOnlyList<Vecteur3> points)
    {
        var o = points.Count == 0 ? Vecteur3.Zero
            : new Vecteur3(Math.Round(points[0].X), Math.Round(points[0].Y), Math.Round(points[0].Z));
        using var f = new BinaryWriter(File.Create(chemin));
        foreach (var p in points)
        {
            f.Write((float)(p.X - o.X));
            f.Write((float)(p.Y - o.Y));
            f.Write((float)(p.Z - o.Z));
        }
        return o;
    }

    private static (int code, string sortie, string erreur) Lancer(
        string python, string dossierEngine, IEnumerable<string> args, TimeSpan delai,
        IProgress<(double, string)>? progression, CancellationToken annulation)
    {
        var psi = new ProcessStartInfo(python)
        {
            WorkingDirectory = dossierEngine,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONPATH"] = dossierEngine;
        psi.Environment["PYTHONNOUSERSITE"] = "1";

        using var p = new Process { StartInfo = psi };
        var sortie = new StringBuilder();
        var erreur = new StringBuilder();
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            sortie.AppendLine(e.Data);
            if (progression is not null && e.Data.StartsWith("PROGRESSION ", StringComparison.Ordinal))
            {
                var morceaux = e.Data.Split(' ', 3);
                if (morceaux.Length >= 2 && double.TryParse(morceaux[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    progression.Report((v, morceaux.Length > 2 ? morceaux[2] : ""));
            }
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) erreur.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        var limite = DateTime.UtcNow + delai;
        while (!p.WaitForExit(100))
        {
            if (annulation.IsCancellationRequested || DateTime.UtcNow > limite)
            {
                try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                annulation.ThrowIfCancellationRequested();
                throw new TimeoutException($"Calcul interrompu après {delai.TotalSeconds:F0} s (délai maximal).");
            }
        }
        p.WaitForExit();
        return (p.ExitCode, sortie.ToString(), erreur.ToString());
    }
}
