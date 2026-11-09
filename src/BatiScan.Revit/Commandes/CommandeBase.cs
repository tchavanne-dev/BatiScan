using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BatiScan.Core.Journal;
using BatiScan.Infrastructure;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BatiScan.Commandes;

/// <summary>
/// Base des commandes BatiScan : contexte, arrêt propre (aucune modification) et
/// journalisation locale de toute erreur (E21).
/// </summary>
public abstract class CommandeBase : IExternalCommand
{
    protected abstract string Titre { get; }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var debut = DateTime.Now;
        try
        {
            var ctx = new Contexte(commandData);
            JournalLocal.Ecrire("commande.debut", new { commande = GetType().Name, document = ctx.Doc.Title });
            var r = Executer(ctx);
            JournalLocal.Ecrire("commande.fin", new { commande = GetType().Name, resultat = r.ToString(), duree_s = (DateTime.Now - debut).TotalSeconds });
            return r;
        }
        catch (ArretCommande a)
        {
            JournalLocal.Ecrire("commande.arret", new { commande = GetType().Name, a.Message });
            TaskDialog.Show(Titre, a.Message);
            return Result.Cancelled;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception e)
        {
            JournalLocal.Ecrire("commande.erreur", new { commande = GetType().Name, erreur = e.ToString() }, "erreur");
            message = $"[BatiScan] {e.Message}\n\nDétails dans le journal : {JournalLocal.CheminDuJour}";
            return Result.Failed;
        }
    }

    protected abstract Result Executer(Contexte ctx);
}
