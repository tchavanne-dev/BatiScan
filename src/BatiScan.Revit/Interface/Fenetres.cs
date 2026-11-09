using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

// ════════════════════════════════════════════════════════════════════════════
//  Coque commune des fenêtres
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Structure visuelle commune à toutes les fenêtres BatiScan :
/// en-tête (surtitre, titre, consigne) · contenu · pied d'actions (secondaires à gauche,
/// annulation et action principale à droite). Toutes les valeurs visuelles viennent du thème.
/// </summary>
internal static class Coque
{
    /// <summary>Handle de la fenêtre principale Revit (propriétaire des fenêtres modales).</summary>
    public static IntPtr Proprietaire { get; set; }

    public static Window Fenetre(string titre, double largeur, double hauteur, bool redimensionnable = true)
    {
        var w = new Window
        {
            Title = titre, Width = largeur, Height = hauteur, MinWidth = Math.Min(largeur, 420), MinHeight = Math.Min(hauteur, 200),
            WindowStartupLocation = Proprietaire != IntPtr.Zero ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false, ResizeMode = redimensionnable ? ResizeMode.CanResize : ResizeMode.NoResize,
        };
        w.Resources.MergedDictionaries.Add(Theme.Charger());
        w.SetResourceReference(FrameworkElement.StyleProperty, "Fenetre.BatiScan");
        if (Proprietaire != IntPtr.Zero) new WindowInteropHelper(w).Owner = Proprietaire;
        return w;
    }

    /// <summary>Assemble en-tête, contenu et pied dans la fenêtre.</summary>
    public static void Assembler(Window w, string titre, string? consigne, UIElement contenu, UIElement pied, UIElement? icone = null)
    {
        var grille = new Grid();
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var entete = new DockPanel();
        entete.SetResourceReference(FrameworkElement.MarginProperty, "Espace.Fenetre");
        if (icone is not null)
        {
            DockPanel.SetDock(icone, Dock.Left);
            entete.Children.Add(icone);
        }
        var textes = new StackPanel();
        var (surtitre, principal) = Decouper(titre);
        textes.Children.Add(Texte(surtitre.ToUpperInvariant(), "Texte.Surtitre", new Thickness(0, 0, 0, 4)));
        textes.Children.Add(Texte(principal, "Texte.Titre"));
        if (!string.IsNullOrWhiteSpace(consigne)) textes.Children.Add(Texte(consigne, "Texte.Secondaire", new Thickness(0, 6, 0, 0)));
        entete.Children.Add(textes);
        Grid.SetRow(entete, 0);
        grille.Children.Add(entete);

        var zone = new Border { Margin = new Thickness(28, 18, 28, 22), Child = contenu };
        Grid.SetRow(zone, 1);
        grille.Children.Add(zone);

        Grid.SetRow(pied, 2);
        grille.Children.Add(pied);
        w.Content = grille;
    }

    /// <summary>« BatiScan – Niveaux (E04) » → (« BatiScan · E04 », « Niveaux »).</summary>
    private static (string surtitre, string titre) Decouper(string titre)
    {
        string t = titre.StartsWith("BatiScan – ", StringComparison.Ordinal) ? titre["BatiScan – ".Length..] : titre;
        string sur = "BatiScan";
        int p = t.LastIndexOf(" (E", StringComparison.Ordinal);
        if (p > 0 && t.EndsWith(')'))
        {
            sur += " · " + t[(p + 2)..^1];
            t = t[..p];
        }
        return (sur, t);
    }

    /// <summary>Pied : actions secondaires à gauche ; annulation puis action principale à droite.</summary>
    public static Border Pied(IEnumerable<Button> droite, IEnumerable<Button>? gauche = null, string? note = null)
    {
        var dp = new DockPanel { LastChildFill = true };
        var d = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in droite) d.Children.Add(b);
        DockPanel.SetDock(d, Dock.Right);
        dp.Children.Add(d);
        var g = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var b in gauche ?? []) g.Children.Add(b);
        if (note is not null)
        {
            var t = Texte(note, "Texte.Legende", new Thickness(gauche is null ? 0 : 8, 0, 0, 0));
            t.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(t);
        }
        dp.Children.Add(g);
        var pied = new Border { Child = dp };
        pied.SetResourceReference(FrameworkElement.StyleProperty, "Surface.Pied");
        return pied;
    }

    public static Button Bouton(string texte, string style, bool parDefaut = false, bool annulation = false)
    {
        var b = new Button { Content = texte, IsDefault = parDefaut, IsCancel = annulation };
        b.SetResourceReference(FrameworkElement.StyleProperty, style);
        return b;
    }

    public static TextBlock Texte(string texte, string style, Thickness? marge = null)
    {
        var t = new TextBlock { Text = texte };
        t.SetResourceReference(FrameworkElement.StyleProperty, style);
        if (marge is { } m) t.Margin = m;
        return t;
    }

    /// <summary>Carte (surface blanche, coins arrondis, ombre douce). Le contenu est découpé aux coins.</summary>
    public static Border Carte(UIElement enfant, bool decouper = true, Thickness? interieur = null)
    {
        var carte = new Border();
        carte.SetResourceReference(FrameworkElement.StyleProperty, "Surface.Carte");
        if (interieur is { } i) carte.Padding = i;
        if (decouper && enfant is FrameworkElement fe)
        {
            // Les coins de la carte ne découpent pas l'enfant : on applique un clip arrondi.
            var conteneur = new Border { Child = fe };
            conteneur.SizeChanged += (_, e) =>
                conteneur.Clip = new RectangleGeometry(new Rect(e.NewSize), 9, 9);
            carte.Child = conteneur;
        }
        else carte.Child = enfant;
        return carte;
    }

    /// <summary>Pastille d'icône d'état (cercle teinté + glyphe), pour l'en-tête des dialogues.</summary>
    public static FrameworkElement PastilleEtat(string cleTeinte, string cleCouleur, string glyphe)
    {
        var cercle = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Margin = new Thickness(0, 2, 14, 0), VerticalAlignment = VerticalAlignment.Top };
        cercle.SetResourceReference(Border.BackgroundProperty, cleTeinte);
        var chemin = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(glyphe), StrokeThickness = 1.9, Stretch = Stretch.None,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        chemin.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, cleCouleur);
        cercle.Child = new Viewbox { Width = 18, Height = 18, Child = new Canvas { Width = 24, Height = 24, Children = { chemin } } };
        return cercle;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  E21 – Validation par lot
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// E21 – Fenêtre de validation d'un LOT : toutes les propositions, modifications et
/// suppressions sont visibles avant application ; chaque ligne peut être acceptée,
/// rejetée ou corrigée (colonnes éditables ✎). Rien n'est appliqué sans l'action principale.
/// </summary>
public static class FenetreValidation
{
    /// <summary>Handle de la fenêtre principale Revit (renseigné par chaque commande).</summary>
    public static IntPtr ProprietaireRevit { get => Coque.Proprietaire; set => Coque.Proprietaire = value; }

    public static bool Afficher<T>(
        string titre, string consigne, IList<T> lignes, IEnumerable<Colonne> colonnes,
        string? resume = null, IList<OptionLot>? options = null, string libelleAppliquer = "Appliquer le lot")
    {
        var fen = Coque.Fenetre(titre, 1180, 720);
        var corps = new DockPanel();

        // ── Options du lot ──────────────────────────────────────────────────
        if (options is { Count: > 0 })
        {
            var panneau = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            foreach (var o in options)
            {
                var groupe = new StackPanel { Margin = new Thickness(0, 0, 20, 6), MinWidth = 240 };
                groupe.Children.Add(Coque.Texte(o.Libelle, "Texte.Legende", new Thickness(0, 0, 0, 5)));
                var cb = new ComboBox { ItemsSource = o.Valeurs, SelectedItem = string.IsNullOrEmpty(o.Valeur) ? o.Valeurs[0] : o.Valeur };
                o.Valeur = (string)cb.SelectedItem;
                cb.SelectionChanged += (_, _) => o.Valeur = (string)cb.SelectedItem;
                groupe.Children.Add(cb);
                panneau.Children.Add(groupe);
            }
            DockPanel.SetDock(panneau, Dock.Top);
            corps.Children.Add(panneau);
        }

        // ── Contexte de calcul (E06/E14 : unités, repère, couverture toujours visibles) ──
        if (!string.IsNullOrWhiteSpace(resume))
        {
            var texte = Coque.Texte(resume.TrimEnd(), "Texte.Legende");
            texte.FontFamily = (FontFamily)fen.FindResource("Police.Mono");
            var cadre = new Border { Child = new ScrollViewer { Content = texte, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
            cadre.SetResourceReference(FrameworkElement.StyleProperty, "Surface.Note");
            var exp = new Expander { Header = "Contexte du calcul", IsExpanded = true, Content = cadre, Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(exp, Dock.Bottom);
            corps.Children.Add(exp);
        }

        // ── Grille ──────────────────────────────────────────────────────────
        var grille = new DataGrid
        {
            AutoGenerateColumns = false, ItemsSource = lignes, CanUserAddRows = false, CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Extended,
        };
        var styleCellule = (Style)fen.FindResource("Champ.Cellule");
        foreach (var c in colonnes)
        {
            DataGridColumn col = c.Type switch
            {
                TypeColonne.Case => new DataGridCheckBoxColumn { Binding = Lier(c), IsReadOnly = !c.Editable },
                TypeColonne.Liste => new DataGridComboBoxColumn { SelectedItemBinding = Lier(c), ItemsSource = c.Valeurs, IsReadOnly = !c.Editable },
                _ => new DataGridTextColumn { Binding = Lier(c), IsReadOnly = !c.Editable, EditingElementStyle = styleCellule },
            };
            col.Header = c.Editable ? c.Entete + "  ✎" : c.Entete;
            if (!double.IsNaN(c.Largeur)) col.Width = c.Largeur;
            grille.Columns.Add(col);
        }
        corps.Children.Add(Coque.Carte(grille));

        // ── Pied ────────────────────────────────────────────────────────────
        var champAccepte = typeof(T).GetProperty("Accepter");
        var tout = Coque.Bouton("Tout accepter", "Bouton.Discret");
        var rien = Coque.Bouton("Tout rejeter", "Bouton.Discret");
        var annuler = Coque.Bouton("Annuler", "Bouton.Secondaire", annulation: true);
        var ok = Coque.Bouton(libelleAppliquer, "Bouton.Primaire", parDefaut: true);
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
        string note = $"{lignes.Count} ligne(s) · Annuler ne modifie rien";
        var pied = champAccepte is null
            ? Coque.Pied([annuler, ok], note: note)
            : Coque.Pied([annuler, ok], [tout, rien], note);

        Coque.Assembler(fen, titre, consigne, corps, pied);
        return fen.ShowDialog() == true;
    }

    private static WpfBinding Lier(Colonne c) => new(c.Chemin)
    {
        Mode = c.Editable ? BindingMode.TwoWay : BindingMode.OneWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        ConverterCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR"),
        // Valeur non mesurée (NaN) ou absente : « – », jamais « NaN » ni 0.
        Converter = c.Editable ? null : AffichageValeur.Instance,
    };

    /// <summary>Fenêtre thématisée vide (usage interne).</summary>
    internal static Window Fenetre(string titre, double largeur, double hauteur) => Coque.Fenetre(titre, largeur, hauteur);
}

// ════════════════════════════════════════════════════════════════════════════
//  Progression annulable
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Fenêtre de progression ANNULABLE. Le travail s'exécute sur un thread d'arrière-plan ;
/// il ne doit JAMAIS appeler l'API Revit (seulement le moteur externe / calculs Core).
/// </summary>
public static class FenetreProgression
{
    public static T Executer<T>(string titre, Func<IProgress<(double, string)>, CancellationToken, T> travail)
    {
        var fen = Coque.Fenetre(titre, 520, 300, redimensionnable: false);
        fen.SizeToContent = SizeToContent.Height;
        var panneau = new StackPanel();
        var texte = Coque.Texte("Démarrage…", "Texte.Corps", new Thickness(0, 0, 0, 12));
        var barre = new ProgressBar { Minimum = 0, Maximum = 1 };
        var pourcent = Coque.Texte("", "Texte.Legende", new Thickness(0, 8, 0, 0));
        panneau.Children.Add(texte); panneau.Children.Add(barre); panneau.Children.Add(pourcent);
        var annuler = Coque.Bouton("Annuler le calcul", "Bouton.Secondaire");
        Coque.Assembler(fen, titre, "Calcul en processus séparé : Revit reste intact tant que le lot n'est pas validé.", panneau,
            Coque.Pied([annuler], note: "Aucune modification pendant le calcul"));

        using var cts = new CancellationTokenSource();
        // Marshalage EXPLICITE vers le thread de la fenêtre : Progress<T> capture le contexte de
        // synchronisation à sa création, absent sur le thread Revit avant ShowDialog → mise à jour
        // depuis le thread de calcul et exception « autre thread propriétaire ».
        var progression = new ProgressionInterface<(double v, string m)>(fen.Dispatcher, p =>
        {
            barre.Value = p.v;
            texte.Text = p.m;
            pourcent.Text = $"{p.v:P0}";
        });
        T? resultat = default;
        Exception? erreur = null;
        bool fini = false;
        void Annuler() { cts.Cancel(); texte.Text = "Annulation demandée…"; annuler.IsEnabled = false; }
        annuler.Click += (_, _) => Annuler();
        // Fermer la fenêtre pendant le calcul = demander l'annulation (pas d'abandon silencieux).
        fen.Closing += (_, e) => { if (!fini) { Annuler(); e.Cancel = true; } };
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

/// <summary>Affichage en lecture : NaN et null → « – » (une absence de mesure n'est jamais un zéro).</summary>
internal sealed class AffichageValeur : IValueConverter
{
    public static readonly AffichageValeur Instance = new();

    public object? Convert(object? v, Type t, object? p, System.Globalization.CultureInfo c) => v switch
    {
        null => "–",
        double d when double.IsNaN(d) => "–",
        _ => v,
    };

    public object? ConvertBack(object? v, Type t, object? p, System.Globalization.CultureInfo c) => Binding.DoNothing;
}

/// <summary>Rapport de progression toujours exécuté sur le thread du <see cref="System.Windows.Threading.Dispatcher"/> donné.</summary>
internal sealed class ProgressionInterface<T>(System.Windows.Threading.Dispatcher dispatcher, Action<T> action) : IProgress<T>
{
    public void Report(T valeur)
    {
        if (dispatcher.CheckAccess()) action(valeur);
        else dispatcher.BeginInvoke(action, valeur);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  Dialogues simples
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Dialogues simples (choix, saisie, fichier).</summary>
public static class Dialogues
{
    public static T? Choisir<T>(string titre, string consigne, IList<T> elements, Func<T, string> libelle) where T : class
    {
        int i = ChoisirIndex(titre, consigne, elements.Select(libelle).ToList());
        return i < 0 ? null : elements[i];
    }

    /// <summary>Choix dans une liste de libellés ; −1 si annulé. Double-clic ou Entrée pour valider.</summary>
    public static int ChoisirIndex(string titre, string consigne, IList<string> libelles)
    {
        if (libelles.Count == 0) return -1;
        var fen = Coque.Fenetre(titre, 700, Math.Min(260 + libelles.Count * 52, 640));
        var liste = new ListBox { SelectedIndex = 0 };
        foreach (var l in libelles) liste.Items.Add(new ListBoxItem { Content = Coque.Texte(l, "Texte.Corps") });
        liste.MouseDoubleClick += (_, e) => { if (liste.SelectedIndex >= 0) fen.DialogResult = true; };
        var ok = Coque.Bouton("Choisir", "Bouton.Primaire", parDefaut: true);
        ok.Click += (_, _) => fen.DialogResult = true;
        Coque.Assembler(fen, titre, consigne, liste,
            Coque.Pied([Coque.Bouton("Annuler", "Bouton.Secondaire", annulation: true), ok], note: $"{libelles.Count} choix"));
        fen.Loaded += (_, _) => { liste.Focus(); (liste.ItemContainerGenerator.ContainerFromIndex(0) as UIElement)?.Focus(); };
        return fen.ShowDialog() == true ? liste.SelectedIndex : -1;
    }

    public static string? Saisir(string titre, string consigne, string defaut)
    {
        var fen = Coque.Fenetre(titre, 580, 300, redimensionnable: false);
        var tb = new TextBox { Text = defaut };
        var ok = Coque.Bouton("Valider", "Bouton.Primaire", parDefaut: true);
        ok.Click += (_, _) => fen.DialogResult = true;
        Coque.Assembler(fen, titre, consigne, new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { tb } },
            Coque.Pied([Coque.Bouton("Annuler", "Bouton.Secondaire", annulation: true), ok]));
        fen.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); Keyboard.Focus(tb); };
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
