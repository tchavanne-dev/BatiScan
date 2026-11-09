# BatiScan

Plugin Revit 2027 Scan-to-BIM (TFE) : calage et coordonnées, niveaux, murs (y compris non plombés), coupes multiples, accrochage au nuage, actualisation des scans ReCap, poteaux/poutres. Référence fonctionnelle : `cahier-des-charges-plugin-scan-bim.md` (E01–E24).

> Statut : code compilé et testé **hors Revit** (tests unitaires C#, tests moteur Python, aller-retour C# ↔ Python). **Aucune commande n'a encore été vérifiée dans Revit** : une sortie d'IA ne valide pas une fonction (voir « Vérifications à faire dans Revit »).

## Architecture

```
BatiScan.slnx
src/BatiScan.Core/        C# sans Revit : transformations, ajustements, révisions de scan, Snap, niveaux,
                          murs non plombés, contrats moteur, client moteur, journal, CSV  (testable seul)
src/BatiScan.Revit/       Plugin : ruban, commandes, validation par lots, stockage extensible, écriture du document
engine/batiscan_engine/   Moteur CPython + Open3D (processus séparé, jamais dans Revit)
engine/tests/             Tests du moteur sur jeux synthétiques
tests/BatiScan.Core.Tests Tests xUnit du Core + intégration réelle avec le moteur
contracts/                Contrat d'échange JSON 1.0.0 (requête / réponse)
```

Échange : le C# extrait les points de la **zone explicite** (`GetPoints`, ≤ 1 000 000 points), écrit `points.bin` + `requete.json` dans `%LOCALAPPDATA%\BatiScan\travail\…`, lance `venv\Scripts\python.exe -m batiscan_engine`, lit `reponse.json`. Le moteur retourne des **propositions** ; seul le C# modifie le document, après validation d'un lot, dans une transaction nommée (rollback en cas d'échec). Sans moteur, les fonctions de détection indiquent leur indisponibilité ; le reste fonctionne.

## Commandes (onglet BatiScan)

| Panneau | Commande | Exigences | Écrit dans le document |
|---|---|---|---|
| Diagnostic | Inventaire des nuages | E01 | non (lecture seule) |
| | Diagnostic projet | E01, E02, E21 | non (lecture seule) |
| | Paramètres | E21 | purge des seules données BatiScan, sur confirmation |
| Calage | Caler le nuage | E02 | déplace **le nuage uniquement**, après affichage des résidus |
| | Nord de travail | E03 | donnée BatiScan (Nord géographique inchangé) |
| | Actualiser le scan | E22 | révisions ; compensation du nuage si repère rigide validé |
| Coupes & Snap | Zones de travail | E05 | données BatiScan ; zone de coupe de la vue 3D |
| | Accrochage au nuage | E23 | points de contrôle (données BatiScan) |
| | Coupes multiples | E05, E24 | lignes de modèle (traces / projections), plans de référence |
| Modélisation | Niveaux | E04 | niveaux et vues en plan validés |
| | Murs par zone | E07, E08, E13, E24 | murs, types dupliqués si nécessaire |
| | Poteaux poutres | E12, E13 | poteaux et poutres (familles déjà chargées) |
| | Écarts nuage | E14 | analyse enregistrée (données BatiScan) |

Principales garanties : coordonnées partagées jamais modifiées ; aucun type existant modifié (duplication contrôlée) ; relance sans doublon (provenance en stockage extensible) ; zones et analyses invalidées quand la source ou sa transformation change ; « non observé » jamais présenté comme « conforme » ; modèle collaboratif : avertissement et blocage sauf confirmation.

### Murs non plombés (E24)

Au moment de la validation, l'utilisateur choisit :
- **Mur incliné fidèle** : `WallCrossSection.SingleSlanted`, angle mesuré, sens vérifié sur la géométrie créée ;
- **Mur vertical strict + rapport de dévers** : axe en pied ou à mi-hauteur, écarts pied/tête dans le CSV.

Un dévers incertain (face partielle, traces irrégulières, épaisseur variable) produit toujours un mur vertical, signalé.

### Actualisation après ré-export ReCap (E22)

Empreinte SHA-256 du `.rcp` + fichiers `.rcs` du dossier « Support ». Cas distingués : chemin seul (calage conservé), contenu modifié (caches invalidés, analyses à recalculer, points de contrôle re-mesurés), transformation modifiée hors BatiScan, fichier absent / nouvel emplacement (nouveau lien avec le calage actuel, ancien lien conservé). Ajustement 6 DDL ancien → nouveau : même repère, repère rigide (compensation proposée sur le **nuage**), ou **non rigide** (aucune correction globale). La maquette n'est jamais déplacée.

## Installation

```powershell
# Moteur Python (Python 3.13)
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt

# Plugin
dotnet build BatiScan.slnx -c Release
$dest = "$env:APPDATA\Autodesk\Revit\Addins\2027"
New-Item -ItemType Directory -Force -Path $dest
Copy-Item src\BatiScan.Revit\bin\Release\net10.0-windows\BatiScan.dll, `
          src\BatiScan.Revit\bin\Release\net10.0-windows\BatiScan.Core.dll, `
          src\BatiScan.Revit\BatiScan.addin $dest
```

Le plugin trouve le moteur via le paramètre `RacineMoteur` (`%APPDATA%\BatiScan\parametres.json`), la variable `BATISCAN_RACINE`, ou les dossiers parents de la DLL. Désinstallation : supprimer les trois fichiers du dossier Addins (les données BatiScan restent dans les projets ; purge possible par « Paramètres »).

## Tests

```powershell
python -m pytest engine/tests                   # moteur : niveaux, murs/dévers J07, coupes, structure, écarts, directions, masque
dotnet test tests/BatiScan.Core.Tests           # Core : J01, J08, Snap, niveaux, murs ; intégration C# ↔ moteur réel
```

## Paramètres et tolérances

Valeurs par défaut dans `src/BatiScan.Core/Parametres/ParametresBatiScan.cs` (cibles proposées du cahier, à valider D07). Valeurs sans cible dans le cahier, marquées « hypothèse BatiScan » : résidu de calage 10 mm, même repère 5 mm, tolérance de plomb 0,2°, régularisation 1° / 20 mm, épaisseur supposée 200 mm. Paramètres des algorithmes : `DEFAUTS` de chaque module du moteur (renvoyés dans chaque réponse).

## Vérifications à faire dans Revit

Points non vérifiables hors Revit, contrôlés à l'exécution quand c'est possible :
1. Repère des points retournés par `GetPoints` (modèle ou local) : détecté automatiquement et journalisé ; extraction refusée s'il est indéterminé.
2. Relation interne ↔ partagé : construite par `ProjectLocation.GetProjectPosition` (méthode documentée ; `GetTotalTransform` n'est pas utilisé, son sens n'étant pas documenté), vérifiée par `BasePoint.SharedPosition` ; calage refusé si non confirmé.
3. Base d'élévation des niveaux (`Elevation` / `ProjectElevation`) : contrôlée après création, lot annulé si incohérent.
4. Signe de l'angle des murs inclinés : vérifié par le centroïde du solide.
5. Rotation 3D d'un nuage (compensation E22 inclinée) : peut être refusée par Revit ; le lot est alors annulé.
6. Affichage : pas de surbrillance graphique temporaire du candidat Snap (confirmation par dialogue).

## Hors périmètre (arbitrages actés)

Baies, dalles, toitures (E09, E10), contrôle de l'annexe (E15) : traités dans Revit. Bonus : le masque de nettoyage réversible (E17) existe côté moteur (`nettoyage`), sans commande Revit ; segmentation bâtiment/environnement (E18) et stations/panoramas (E19) non développés.

## Données

Aucun scan réel versionné (`.gitignore`). Jeux de test synthétiques générés par les tests (voir `tests/data/README.md`). Journal local uniquement (`%LOCALAPPDATA%\BatiScan\journal`), aucune télémétrie ; les extraits de points sont supprimés après chaque calcul (`BATISCAN_CONSERVER_POINTS=1` pour les garder).

## Licence

MIT – voir `LICENSE` (à créer).
