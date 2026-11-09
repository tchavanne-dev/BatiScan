using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using WpfBinding = System.Windows.Data.Binding;

namespace BatiScan.Interface;

// ── Colonnes ────────────────────────────────────────────────────────────────

public enum TypeColonne { Texte, Case, Liste }

/// <summary>Définition d'une colonne de la grille de validation.</summary>
public sealed record Colonne(string Entete, string Chemin, bool Editable = false, TypeColonne Type = TypeColonne.Texte, IEnumerable? Valeurs = null, double Largeur = double.NaN);

/// <summary>Option globale du lot (liste déroulante), ex. mode des murs non plombés.</summary>
public sealed class OptionLot
{
    public required string Libelle { get; init; }
    public required IList<string> Valeurs { get; init; }
    public string Valeur { get; set; } = "";
}

/// <summary>
/// E21 – Fenêtre de validation d'un LOT : toutes les propositions, modifications et
/// suppressions sont visibles avant application ; chaque ligne peut être acceptée,
/// rejetée ou corrigée (colonnes éditables). Rien n'est appliqué sans « Appliquer le lot ».
/// </summary>
public static class FenetreValidation
{
    public static bool Afficher<T>(
        string titre, string consigne, IList<T> lignes, IEnumerable<Colonne> colonnes,
        string? resume = null, IList<OptionLot>? options = null, string libelleAppliquer = "Appliquer le lot")
    {
        var fen = Fenetre(titre, 1100, 640);
        var racine = new DockPanel { Margin = new Thickness(10) };

        var entete = new TextBlock { Text = consigne, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(entete, Dock.Top);
        racine.Children.Add(entete);

        var boutons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(boutons, Dock.Bottom);
        var tout = new Button { Content = "Tout accepter", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0) };
        var rien = new Button { Content = "Tout rejeter", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 24, 0) };
        var ok = new Button { Content = libelleAppliquer, Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var annuler = new Button { Content = "Annuler (aucune modification)", Padding = new Thickness(14, 3, 14, 3), IsCancel = true };
        boutons.Children.Add(tout); boutons.Children.Add(rien); boutons.Children.Add(ok); boutons.Children.Add(annuler);
        racine.Children.Add(boutons);

        if (!string.IsNullOrWhiteSpace(resume))
        {
            var r = new TextBox
            {
                Text = resume, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 140,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            };
            DockPanel.SetDock(r, Dock.Bottom);
            racine.Children.Add(r);
        }

        if (options is { Count: > 0 })
        {
            var panneau = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var o in options)
            {
                panneau.Children.Add(new TextBlock { Text = o.Libelle + " : ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
                var cb = new ComboBox { ItemsSource = o.Valeurs, SelectedItem = string.IsNullOrEmpty(o.Valeur) ? o.Valeurs[0] : o.Valeur, MinWidth = 220, Margin = new Thickness(0, 0, 16, 4) };
                o.Valeur = (string)cb.SelectedItem;
                cb.SelectionChanged += (_, _) => o.Valeur = (string)cb.SelectedItem;
                panneau.Children.Add(cb);
            }
            DockPanel.SetDock(panneau, Dock.Top);
            racine.Children.Add(panneau);
        }

        var grille = new DataGrid
        {
            AutoGenerateColumns = false, ItemsSource = lignes, CanUserAddRows = false, CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Extended, HeadersVisibility = DataGridHeadersVisibility.Column,
        };
        foreach (var c in colonnes)
        {
            DataGridColumn col = c.Type switch
            {
                TypeColonne.Case => new DataGridCheckBoxColumn { Binding = Lier(c), IsReadOnly = !c.Editable },
                TypeColonne.Liste => new DataGridComboBoxColumn { SelectedItemBinding = Lier(c), ItemsSource = c.Valeurs, IsReadOnly = !c.Editable },
                _ => new DataGridTextColumn { Binding = Lier(c), IsReadOnly = !c.Editable },
            };
            col.Header = c.Entete + (c.Editable ? " ✎" : "");
            if (!double.IsNaN(c.Largeur)) col.Width = c.Largeur;
            grille.Columns.Add(col);
        }
        racine.Children.Add(grille);

        var champAccepte = typeof(T).GetProperty("Accepter");
        void ToutCocher(bool v)
        {
            if (champAccepte is null) return;
            grille.CommitEdit(DataGridEditingUnit.Row, true);
            foreach (var l in lignes) champAccepte.SetValue(l, v);
            grille.Items.Refresh();
        }
        tout.Click += (_, _) => ToutCocher(true);
        rien.Click += (_, _) => ToutCocher(false);
        ok.Click += (_, _) => { grille.CommitEdit(DataGridEditingUnit.Row, true); fen.DialogResult = true; };

        fen.Content = racine;
        return fen.ShowDialog() == true;
    }

    private static WpfBinding Lier(Colonne c) => new(c.Chemin)
    {
        Mode = c.Editable ? BindingMode.TwoWay : BindingMode.OneWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
    };

    // ── Utilitaires communs ─────────────────────────────────────────────────

    /// <summary>Fenêtre WPF rattachée à la fenêtre principale de Revit.</summary>
    internal static Window Fenetre(string titre, double largeur, double hauteur)
    {
        var w = new Window
        {
            Title = titre, Width = largeur, Height = hauteur, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, ResizeMode = ResizeMode.CanResize,
        };
        if (ProprietaireRevit != IntPtr.Zero) new WindowInteropHelper(w).Owner = ProprietaireRevit;
        return w;
    }

    /// <summary>Handle de la fenêtre principale Revit (renseigné par chaque commande).</summary>
    public static IntPtr ProprietaireRevit { get; set; }
}

/// <summary>
/// Fenêtre de progression ANNULABLE. Le travail s'exécute sur un thread d'arrière-plan ;
/// il ne doit JAMAIS appeler l'API Revit (seulement le moteur externe / calculs Core).
/// </summary>
public static class FenetreProgression
{
    public static T Executer<T>(string titre, Func<IProgress<(double, string)>, CancellationToken, T> travail)
    {
        var fen = FenetreValidation.Fenetre(titre, 480, 170);
        fen.ResizeMode = ResizeMode.NoResize;
        var panneau = new StackPanel { Margin = new Thickness(14) };
        var texte = new TextBlock { Text = "Démarrage…", Margin = new Thickness(0, 0, 0, 8) };
        var barre = new ProgressBar { Height = 18, Minimum = 0, Maximum = 1 };
        var annuler = new Button { Content = "Annuler le calcul", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(10, 3, 10, 3) };
        panneau.Children.Add(texte); panneau.Children.Add(barre); panneau.Children.Add(annuler);
        fen.Content = panneau;

        using var cts = new CancellationTokenSource();
        var progression = new Progress<(double v, string m)>(p => { barre.Value = p.v; texte.Text = p.m; });
        T? resultat = default;
        Exception? erreur = null;
        bool fini = false;
        annuler.Click += (_, _) => { cts.Cancel(); texte.Text = "Annulation demandée…"; annuler.IsEnabled = false; };
        // Fermer la fenêtre pendant le calcul = demander l'annulation (pas d'abandon silencieux).
        fen.Closing += (_, e) => { if (!fini) { cts.Cancel(); e.Cancel = true; texte.Text = "Annulation demandée…"; } };
        fen.Loaded += async (_, _) =>
        {
            try { resultat = await Task.Run(() => travail(progression, cts.Token)); }
            catch (Exception e) { erreur = e; }
            fini = true;
            fen.Close();
        };
        fen.ShowDialog();
        if (erreur is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(erreur).Throw();
        return resultat!;
    }
}

/// <summary>Dialogues simples (choix, saisie).</summary>
public static class Dialogues
{
    public static T? Choisir<T>(string titre, string consigne, IList<T> elements, Func<T, string> libelle) where T : class
    {
        int i = ChoisirIndex(titre, consigne, elements.Select(libelle).ToList());
        return i < 0 ? null : elements[i];
    }

    /// <summary>Choix dans une liste de libellés ; −1 si annulé.</summary>
    public static int ChoisirIndex(string titre, string consigne, IList<string> libelles)
    {
        if (libelles.Count == 0) return -1;
        var fen = FenetreValidation.Fenetre(titre, 640, 420);
        var dp = new DockPanel { Margin = new Thickness(10) };
        var t = new TextBlock { Text = consigne, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(t, Dock.Top);
        dp.Children.Add(t);
        var b = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var ok = new Button { Content = "Choisir", IsDefault = true, Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 8, 0) };
        var an = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(12, 3, 12, 3) };
        b.Children.Add(ok); b.Children.Add(an);
        DockPanel.SetDock(b, Dock.Bottom);
        dp.Children.Add(b);
        var liste = new ListBox { ItemsSource = libelles, SelectedIndex = 0 };
        liste.MouseDoubleClick += (_, _) => fen.DialogResult = true;
        dp.Children.Add(liste);
        ok.Click += (_, _) => fen.DialogResult = true;
        fen.Content = dp;
        return fen.ShowDialog() == true ? liste.SelectedIndex : -1;
    }

    public static string? Saisir(string titre, string consigne, string defaut)
    {
        var fen = FenetreValidation.Fenetre(titre, 560, 200);
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(new TextBlock { Text = consigne, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var tb = new TextBox { Text = defaut };
        sp.Children.Add(tb);
        var b = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "Valider", IsDefault = true, Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 8, 0) };
        var an = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(12, 3, 12, 3) };
        b.Children.Add(ok); b.Children.Add(an);
        sp.Children.Add(b);
        ok.Click += (_, _) => fen.DialogResult = true;
        fen.Content = sp;
        fen.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        return fen.ShowDialog() == true ? tb.Text : null;
    }

    /// <summary>Saisie d'une liste de nombres « 0,5 ; 1 ; 2,25 » (virgule ou point décimal).</summary>
    public static double[]? SaisirNombres(string titre, string consigne, string defaut)
    {
        string? s = Saisir(titre, consigne, defaut);
        if (s is null) return null;
        var res = new List<double>();
        foreach (var morceau in s.Split([';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(morceau.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v))
                throw new FormatException($"Valeur non numérique : « {morceau} ».");
            res.Add(v);
        }
        return res.ToArray();
    }

    public static string? OuvrirFichier(string titre, string filtre)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Title = titre, Filter = filtre, CheckFileExists = true };
        return d.ShowDialog() == true ? d.FileName : null;
    }
}
