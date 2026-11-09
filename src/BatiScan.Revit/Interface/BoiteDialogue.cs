using System.Windows;
using System.Windows.Controls;
using CommandLinkId = Autodesk.Revit.UI.TaskDialogCommandLinkId;
using CommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using DialogIcon = Autodesk.Revit.UI.TaskDialogIcon;
using DialogResult = Autodesk.Revit.UI.TaskDialogResult;

namespace BatiScan.Interface;

/// <summary>
/// Dialogue WPF au design system BatiScan, remplaçant <c>Autodesk.Revit.UI.TaskDialog</c>
/// avec la même surface d'API (MainInstruction, MainContent, ExpandedContent, MainIcon,
/// CommonButtons, DefaultButton, AddCommandLink, Show) et les mêmes valeurs de retour.
/// Les liens de commande deviennent des cartes d'action (titre + description).
/// Fermeture par la croix ou Échap : <see cref="DialogResult.Close"/> ou <see cref="DialogResult.Cancel"/>.
/// </summary>
public sealed class BoiteDialogue(string titre)
{
    public string MainInstruction { get; set; } = "";
    public string MainContent { get; set; } = "";
    public string ExpandedContent { get; set; } = "";
    public DialogIcon MainIcon { get; set; } = DialogIcon.TaskDialogIconNone;
    public CommonButtons CommonButtons { get; set; } = CommonButtons.None;
    public DialogResult DefaultButton { get; set; } = DialogResult.None;

    private readonly List<(CommandLinkId id, string texte, string? description)> _liens = [];

    public void AddCommandLink(CommandLinkId id, string texte) => _liens.Add((id, texte, null));
    public void AddCommandLink(CommandLinkId id, string texte, string description) => _liens.Add((id, texte, description));

    /// <summary>Équivalent de <c>TaskDialog.Show(titre, message)</c>.</summary>
    public static DialogResult Show(string titre, string message) =>
        new BoiteDialogue(titre) { MainInstruction = Premiere(message, out var reste), MainContent = reste }.Show();

    // Glyphes d'état (grille 24)
    private const string GlypheInfo = "M12,10.5 V17 M12,7 L12,7.01";
    private const string GlypheAttention = "M12,8 V13 M12,16.5 L12,16.51";
    private const string GlypheErreur = "M8.5,8.5 L15.5,15.5 M15.5,8.5 L8.5,15.5";

    public DialogResult Show()
    {
        bool long_ = MainContent.Length > 520 || MainContent.Count(c => c == '\n') > 9;
        var fen = Coque.Fenetre(titre, long_ ? 760 : 580, long_ ? 640 : 320);
        fen.SizeToContent = long_ ? SizeToContent.Manual : SizeToContent.Height;
        fen.MaxHeight = 760;
        DialogResult resultat = CommonButtons.HasFlag(CommonButtons.Cancel) ? DialogResult.Cancel : DialogResult.Close;

        var corps = new DockPanel { LastChildFill = true };
        if (!string.IsNullOrWhiteSpace(MainInstruction))
        {
            var instr = Coque.Texte(MainInstruction, "Texte.Instruction", new Thickness(0, 0, 0, 8));
            DockPanel.SetDock(instr, Dock.Top);
            corps.Children.Add(instr);
        }

        // Liens de commande → cartes d'action
        if (_liens.Count > 0)
        {
            var actions = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (var (id, texte, description) in _liens)
            {
                var contenu = new StackPanel();
                contenu.Children.Add(Coque.Texte(texte, "Texte.Corps"));
                ((TextBlock)contenu.Children[0]).FontWeight = FontWeights.SemiBold;
                if (description is not null) contenu.Children.Add(Coque.Texte(description, "Texte.Legende", new Thickness(0, 3, 0, 0)));
                var carte = Coque.Bouton("", "Bouton.Carte");
                carte.Content = contenu;
                var choix = id switch
                {
                    CommandLinkId.CommandLink1 => DialogResult.CommandLink1,
                    CommandLinkId.CommandLink2 => DialogResult.CommandLink2,
                    CommandLinkId.CommandLink3 => DialogResult.CommandLink3,
                    _ => DialogResult.CommandLink4,
                };
                carte.Click += (_, _) => { resultat = choix; fen.DialogResult = true; };
                actions.Children.Add(carte);
            }
            DockPanel.SetDock(actions, Dock.Bottom);
            corps.Children.Add(actions);
        }

        if (!string.IsNullOrWhiteSpace(ExpandedContent))
        {
            var exp = new Expander { Header = "Détails", Margin = new Thickness(0, 10, 0, 0), Content = Coque.Texte(ExpandedContent, "Texte.Legende", new Thickness(0, 6, 0, 0)) };
            DockPanel.SetDock(exp, Dock.Bottom);
            corps.Children.Add(exp);
        }

        if (!string.IsNullOrWhiteSpace(MainContent))
        {
            if (long_)
            {
                var tb = new TextBox { Text = MainContent.TrimEnd() };
                tb.SetResourceReference(FrameworkElement.StyleProperty, "Champ.Lecture");
                corps.Children.Add(tb);
            }
            else corps.Children.Add(Coque.Texte(MainContent.TrimEnd(), "Texte.Secondaire"));
        }
        else corps.Children.Add(new Border());

        // Boutons standard
        var boutons = new List<Button>();
        void Ajouter(string texte, DialogResult r, bool primaire)
        {
            var b = Coque.Bouton(texte, primaire ? "Bouton.Primaire" : "Bouton.Secondaire",
                parDefaut: DefaultButton == r || (DefaultButton == DialogResult.None && primaire && r != DialogResult.Yes),
                annulation: r is DialogResult.No or DialogResult.Cancel or DialogResult.Close);
            b.Click += (_, _) => { resultat = r; fen.DialogResult = true; };
            boutons.Add(b);
        }
        if (CommonButtons.HasFlag(CommonButtons.Cancel)) Ajouter("Annuler", DialogResult.Cancel, false);
        // « Oui » reste l'action visuellement principale ; la touche Entrée suit DefaultButton (souvent « Non »).
        if (CommonButtons.HasFlag(CommonButtons.No)) Ajouter("Non", DialogResult.No, false);
        if (CommonButtons.HasFlag(CommonButtons.Yes)) Ajouter("Oui", DialogResult.Yes, true);
        if (CommonButtons.HasFlag(CommonButtons.Ok)) Ajouter("OK", DialogResult.Ok, true);
        if (CommonButtons.HasFlag(CommonButtons.Close) || boutons.Count == 0)
            Ajouter(_liens.Count > 0 ? "Annuler" : "Fermer", _liens.Count > 0 ? DialogResult.Cancel : DialogResult.Close, _liens.Count == 0);

        Coque.Assembler(fen, titre, null, corps, Coque.Pied(boutons), Icone());
        fen.ShowDialog();
        return resultat;
    }

    private FrameworkElement? Icone() => MainIcon switch
    {
        DialogIcon.TaskDialogIconWarning => Coque.PastilleEtat("Pinceau.AttentionTeinte", "Pinceau.Attention", GlypheAttention),
        DialogIcon.TaskDialogIconError => Coque.PastilleEtat("Pinceau.ErreurTeinte", "Pinceau.Erreur", GlypheErreur),
        DialogIcon.TaskDialogIconInformation => Coque.PastilleEtat("Pinceau.AccentTeinte", "Pinceau.Accent", GlypheInfo),
        _ => null,
    };

    /// <summary>Sépare un message en première phrase (instruction) et reste (contenu).</summary>
    private static string Premiere(string message, out string reste)
    {
        int i = message.IndexOf('\n');
        if (i < 0 || i > 160) { reste = i < 0 ? "" : message; return i < 0 ? message : ""; }
        reste = message[(i + 1)..].TrimStart('\n');
        return message[..i].TrimEnd();
    }
}
