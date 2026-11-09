using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BatiScan.Interface;

/// <summary>Glyphe vectoriel sur grille 24 × 24 : tracé au trait, points pleins, pointillés éventuels.</summary>
public sealed record Glyphe(string Trace, (double x, double y, double r)[]? Points = null, bool Pointille = false);

/// <summary>
/// Icônes du ruban dessinées EN MÉMOIRE (aucun fichier externe) : tuile arrondie teintée par
/// panneau, léger dégradé vertical pour la profondeur, glyphe blanc au trait à extrémités rondes.
/// Rendu séparé en 16 et 32 px avec une épaisseur de trait adaptée (≈ 1 px et ≈ 1,7 px).
/// </summary>
public static class Icones
{
    // ── Bibliothèque de glyphes (grille 24) ─────────────────────────────────
    public static readonly Glyphe Nuage = new("", [(5.5, 14, 1.5), (8.5, 10, 1.5), (12, 7.5, 1.5), (15.5, 9.5, 1.5), (18.5, 13, 1.5),
                                                   (9, 15.5, 1.5), (12.5, 12.5, 1.5), (15.5, 16, 1.5), (12, 18.5, 1.5)]);
    public static readonly Glyphe Diagnostic = new("M10.5,3.5 A7,7 0 1 1 10.49,3.5 Z M15.6,15.6 L20.5,20.5 M6.5,10.5 L8.5,10.5 L10,8 L11.5,13 L13,10.5 L14.5,10.5");
    public static readonly Glyphe Reglages = new("M4,7 H20 M4,12 H20 M4,17 H20", [(9, 7, 2.3), (15.5, 12, 2.3), (11, 17, 2.3)]);
    public static readonly Glyphe Cible = new("M12,5.5 A6.5,6.5 0 1 1 11.99,5.5 Z M12,2.5 V7 M12,17 V21.5 M2.5,12 H7 M17,12 H21.5", [(12, 12, 1.6)]);
    public static readonly Glyphe Boussole = new("M12,3.5 A8.5,8.5 0 1 1 11.99,3.5 Z M12,6.5 L14.6,13.5 L12,12.2 L9.4,13.5 Z M12,12.2 V17");
    public static readonly Glyphe Actualiser = new("M19,9.5 A7.5,7.5 0 0 0 5.4,8.6 M5,14.5 A7.5,7.5 0 0 0 18.6,15.4 M19.2,4.8 V9.6 H14.4 M4.8,19.2 V14.4 H9.6");
    public static readonly Glyphe Zone = new("M4.5,4.5 H19.5 V19.5 H4.5 Z", Pointille: true);
    public static readonly Glyphe Accrochage = new("M3.5,12 H8.5 M15.5,12 H20.5 M12,3.5 V8.5 M12,15.5 V20.5", [(12, 12, 2.4)]);
    public static readonly Glyphe Coupes = new("M3.5,7 H20.5 M3.5,12 H20.5 M3.5,17 H20.5 M9,3.5 L12,20.5");
    public static readonly Glyphe Niveaux = new("M7.5,6 H20.5 M7.5,12 H20.5 M7.5,18 H20.5 M3.5,4.5 L6,6 L3.5,7.5 Z M3.5,10.5 L6,12 L3.5,13.5 Z M3.5,16.5 L6,18 L3.5,19.5 Z");
    public static readonly Glyphe Murs = new("M3.5,5.5 H20.5 V18.5 H3.5 Z M3.5,12 H20.5 M10,5.5 V12 M14,12 V18.5");
    public static readonly Glyphe Structure = new("M3.5,5 H20.5 V8.5 H3.5 Z M10,8.5 V20 M14,8.5 V20 M8,20 H16");
    public static readonly Glyphe Ecarts = new("M3.5,18 H20.5 M3.5,18 V15.5 M7.75,18 V16.5 M12,18 V15.5 M16.25,18 V16.5 M20.5,18 V15.5 M3.5,10 C7,5 9.5,13 13,9 C15.5,6.2 18,7.5 20.5,8");

    // ── Rendu ───────────────────────────────────────────────────────────────

    /// <summary>Paire (16 px, 32 px) pour <c>PushButtonData.Image</c> / <c>LargeImage</c>.</summary>
    public static (BitmapSource petite, BitmapSource grande) Paire(Glyphe g, Color teinte) => (Rendre(g, teinte, 16), Rendre(g, teinte, 32));

    public static BitmapSource Rendre(Glyphe g, Color teinte, int taille)
    {
        var visuel = new DrawingVisual();
        using (var dc = visuel.RenderOpen())
        {
            double rayon = taille * 0.22;
            var tuile = new Rect(0.5, 0.5, taille - 1, taille - 1);
            // Dégradé discret : haut légèrement éclairci → profondeur sans effet « bouton plastique ».
            var fond = new LinearGradientBrush(Eclaircir(teinte, 0.14), teinte, 90);
            fond.Freeze();
            var liseré = new Pen(new SolidColorBrush(Assombrir(teinte, 0.12)), 1);
            liseré.Freeze();
            dc.DrawRoundedRectangle(fond, liseré, tuile, rayon, rayon);

            var blanc = new SolidColorBrush(Theme.Couleur("Couleur.Ruban.Glyphe"));
            blanc.Freeze();
            double zone = taille * (taille <= 16 ? 0.74 : 0.66);
            double echelle = zone / 24.0;
            double decalage = (taille - zone) / 2;
            dc.PushTransform(new MatrixTransform(echelle, 0, 0, echelle, decalage, decalage));
            if (!string.IsNullOrEmpty(g.Trace))
            {
                double trait = (taille <= 16 ? 1.05 : 1.7) / echelle; // épaisseur en pixels écran
                var stylo = new Pen(blanc, trait) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                if (g.Pointille) { stylo.DashStyle = new DashStyle([2.2, 1.8], 0); stylo.DashCap = PenLineCap.Round; }
                stylo.Freeze();
                dc.DrawGeometry(null, stylo, Geometry.Parse(g.Trace));
            }
            if (g.Points is not null)
            {
                double facteur = taille <= 16 ? 1.15 : 1.0; // points un peu plus gros en 16 px pour rester lisibles
                foreach (var (x, y, r) in g.Points) dc.DrawEllipse(blanc, null, new Point(x, y), r * facteur, r * facteur);
            }
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(taille, taille, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visuel);
        bmp.Freeze();
        return bmp;
    }

    private static Color Eclaircir(Color c, double k) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * k), (byte)(c.G + (255 - c.G) * k), (byte)(c.B + (255 - c.B) * k));

    private static Color Assombrir(Color c, double k) =>
        Color.FromRgb((byte)(c.R * (1 - k)), (byte)(c.G * (1 - k)), (byte)(c.B * (1 - k)));
}
