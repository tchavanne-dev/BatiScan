# Contrats d'échange C# ↔ Python

Schémas versionnés des fichiers échangés entre le plugin Revit (C#) et le moteur de calcul (Python).

| Fichier | Sens | Version |
|---|---|---|
| `requete.schema.json` | C# → Python | 1.0.0 |
| `reponse.schema.json` | Python → C# | 1.0.0 |

Chaque fichier d'échange déclare au minimum :

- `contract_version` : version du schéma utilisé ;
- les **unités** de toutes les longueurs (jamais implicites) : mètres et degrés ;
- le **repère** des coordonnées (`revit_interne_m` = coordonnées internes Revit converties en mètres) et, pour traçabilité, la transformation vers les coordonnées partagées ;
- la **source** (identifiant, chemin, empreinte SHA-256, révision, transformation de l'instance) ;
- les **paramètres** de calcul (la réponse renvoie les paramètres *effectifs*, valeurs par défaut incluses) et la graine aléatoire ;
- le **nombre de points** et la **couverture** (E06, E14) ;
- les **erreurs et avertissements**.

## Protocole

1. Le C# extrait les points de la zone (`PointCloudInstance.GetPoints`, filtre multi-plans) dans un fichier binaire temporaire `points.bin` (float32 little-endian, `x y z` consécutifs, coordonnées **relatives à `points.origine_m`** pour conserver la précision loin de l'origine).
2. Le C# écrit `requete.json` dans un dossier de travail `%LOCALAPPDATA%\BatiScan\travail\<id>` puis lance :
   `venv\Scripts\python.exe -m batiscan_engine requete.json reponse.json`
3. Le moteur écrit sur la sortie standard des lignes `PROGRESSION <0..1> <message>` (facultatif), puis `reponse.json`. Code de sortie 0 dès que la réponse est écrite, y compris avec `statut = "erreur"`.
4. Annulation : le C# termine le processus ; aucune réponse partielle n'est exploitée.
5. Le C# présente les propositions (accepter / rejeter / corriger) ; seul le C# modifie le document Revit, dans une transaction nommée.

Le fichier source du nuage n'est jamais lu en écriture : le nettoyage (E17) produit un **masque** séparé (`uint8`, 1 = point conservé).

## Opérations

| Opération | Exigence | Propositions |
|---|---|---|
| `niveaux` | E04 | `niveau` : altitude, inclinaison, surface, type supposé, justification |
| `murs` | E07, E24 | `mur` : axe, faces, épaisseur mesurée ou inconnue, dévers par traces |
| `coupes` | E05, E24 | `trace` : segments à une altitude |
| `structure` | E12 | `poteau`, `poutre` : géométrie observée, section non affirmée |
| `ecarts` | E14 | `ecart` : statistiques et couverture par surface, « non_observe » sans points |
| `directions` | E03 | `direction` : directions dominantes alternatives |
| `nettoyage` | E17 | `masque` : masque réversible, zones protégées respectées |

Toute évolution incompatible incrémente la version majeure ; le moteur refuse une version qu'il ne connaît pas.

Voir le cahier des charges, section « Contrat des données et des interfaces ».
