# BatiScan – Règles de développement

Plugin Revit Scan-to-BIM (TFE). Référence fonctionnelle : `cahier-des-charges-plugin-scan-bim.md` (exigences E01–E24), `choix-technique-et-planning-tfe.md` (étapes A–G), `analyse-faro-arbitrage-fonctionnel.pplx.md` (matrice F/N). Citer l'exigence E concernée dans les commits et le code non trivial.

## Arborescence

```
BatiScan.slnx
src/BatiScan.Core/       Calculs C# sans Revit (transformations, révisions de scan, Snap, contrats moteur), testables seuls
src/BatiScan.Revit/      UI et intégration Revit (C#) : ruban, commandes, validation, écriture du document
engine/batiscan_engine/  Moteur de calcul (Python + Open3D) : analyse de nuages, propositions
engine/tests/            Tests du moteur (hors Revit)
tests/BatiScan.Core.Tests/ Tests xUnit du Core + intégration C# ↔ moteur
contracts/               Schémas versionnés des fichiers d'échange C# ↔ Python
tests/data/              Jeux de test synthétiques J01–J09 (pas de scans privés)
venv/                    Environnement Python local (non versionné)
```

## API Revit 2027 stricte

- Cible unique : **Revit 2027**, `net10.0-windows`. Aucun code multi-version, aucun `#if REVIT20xx`.
- Références `RevitAPI.dll` / `RevitAPIUI.dll` depuis `C:\Program Files\Autodesk\Revit 2027\`, avec `<Private>False</Private>` (jamais copiées dans la sortie).
- N'utiliser que des membres existant dans l'API 2027. Pas d'API obsolète (`[Obsolete]`), pas d'API supprimée (ex. `DisplayUnitType`, `ElementId.IntegerValue`) : utiliser `ForgeTypeId`/`UnitTypeId`, `ElementId.Value`. En cas de doute sur un membre, le signaler plutôt que l'inventer.
- Unités internes de l'API = **pieds**. Toute conversion passe par `UnitUtils.ConvertToInternalUnits` / `ConvertFromInternalUnits` et est explicite.
- Commandes de diagnostic : `[Transaction(TransactionMode.ReadOnly)]`.
- Écriture du document uniquement via `Transaction`/`TransactionGroup` nommées, après validation utilisateur d'un lot (E21). Un échec laisse le document cohérent (rollback).
- Jamais de modification silencieuse des coordonnées partagées, du calage d'un nuage, ni d'un type partagé existant (E02, E07, E22).
- Aucun appel à l'API Revit hors du thread Revit : les traitements longs passent par `IExternalEventHandler`.
- Modèle collaboratif détecté (`Document.IsWorkshared`) : avertir et bloquer les opérations non validées.

## Séparation C# / Python

- **C# (`src/BatiScan.Revit`)** : interface, lecture du document, conversion de repères/unités, validation, création des objets Revit. Seul composant autorisé à modifier le document.
- **Python (`engine/`)** : calcul pur sur les nuages. N'importe rien de Revit, ne tourne **jamais** dans le processus Revit (pas de pythonnet, IronPython ni pyRevit).
- Communication : le C# lance `venv\Scripts\python.exe -m batiscan_engine` en processus séparé ; échange par fichiers JSON conformes à `contracts/` (version, unités, repère, source, paramètres, erreurs).
- Le moteur retourne des **propositions** (niveaux candidats, plans, murs) ; il ne décide pas. Le C# les présente pour acceptation/rejet/correction.
- Le plugin reste utilisable sans moteur Python : les fonctions avancées indiquent alors leur indisponibilité (E21).
- Calculs géométriques simples (transformations, conversions) : en C#, testables sans Revit.

## Open3D

- Version figée dans `requirements.txt` (`open3d==0.20.0`, Python 3.13). Pas de mise à jour sans relancer les tests.
- Utiliser Open3D pour : lecture de nuages, sous-échantillonnage (`voxel_down_sample`), normales, `KDTreeFlann`, segmentation de plans (`segment_plane`/RANSAC), filtrage des points aberrants.
- Fichier source **jamais modifié ni écrasé** : nettoyage = masque réversible (E17).
- Tout résultat déclare unités, repère, paramètres, nombre de points et couverture (E06, E14). Les zones sans points restent « non observées », jamais « conformes ».
- Graines aléatoires fixées (RANSAC) pour des résultats reproductibles.
- Traitement par zone explicite (E05) ; aucune promesse de taille illimitée.

## Données et sécurité

- Scans réels privés : jamais versionnés, jamais envoyés à un service externe.
- Pas de télémétrie distante ; journal local uniquement.

## Commandes

```powershell
dotnet build BatiScan.slnx -c Debug          # compiler le plugin
.\venv\Scripts\Activate.ps1                   # activer l'environnement Python
pip install -r requirements.txt               # installer les dépendances du moteur
python -m pytest engine/tests                 # tests du moteur
dotnet test tests/BatiScan.Core.Tests         # tests du Core C# (+ intégration moteur)
```

## Style

- Interface et commentaires en français, vocabulaire BIM. Respecter le style existant (en-têtes `// ── … ──`, XML doc).
- Petites modifications vérifiées ; une sortie d'IA ne valide pas une fonction : vérification dans Revit requise.
