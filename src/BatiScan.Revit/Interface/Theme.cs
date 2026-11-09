using System.Windows;
using System.Windows.Media;

namespace BatiScan.Interface;

/// <summary>
/// Accès au design system (<c>Interface/Theme/BatiScanTheme.xaml</c>, compilé dans l'assembly).
/// Revit n'expose pas d'objet <see cref="Application"/> WPF propre au plugin : le dictionnaire
/// est fusionné dans les ressources de CHAQUE fenêtre BatiScan.
/// </summary>
public static class Theme
{
    private static readonly Uri Source = new("pack://application:,,,/BatiScan;component/Interface/Theme/BatiScanTheme.xaml", UriKind.Absolute);
    private static ResourceDictionary? _partage;

    static Theme()
    {
        // Enregistre le schéma « pack:// » si aucune Application WPF ne l'a encore fait (hôte Revit).
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
    }

    /// <summary>Nouvelle instance du dictionnaire (une par fenêtre).</summary>
    public static ResourceDictionary Charger() => new() { Source = Source };

    /// <summary>Instance partagée, en lecture seule (couleurs du ruban, etc.).</summary>
    public static ResourceDictionary Partage => _partage ??= Charger();

    public static Color Couleur(string cle) =>
        Partage[cle] is Color c ? c : throw new KeyNotFoundException($"Couleur de thème absente : {cle}");
}
