# Jeux de test synthétiques

Aucun scan réel n'est versionné. Les jeux sont **générés par le code des tests** (graines fixées) :

| Jeu | Générateur | Tests |
|---|---|---|
| J01 – transformations, unités, orientation | `TestsGeometrie.cs` | aller-retour ≤ 0,1 mm, 4/6 DDL, orientation ≤ 0,1° |
| J02 (synthétique partiel) – niveaux | `engine/tests/synthetique.py::maison` | altitudes ≤ 10 mm, table classée mobilier |
| J05 (partiel) – niveaux existants | `TestsMetier.cs::TestsNiveaux` | association sans doublon |
| J07 – mur non plombé, coupes | `synthetique.py::mur_incline`, `TestsIntegrationMoteur.cs` | dévers 1° ± 0,05°, coupes ≤ 1 mm |
| J08 – ré-export rigide / non rigide | `TestsMetier.cs::TestsRevisionScan` | compensation ≤ 1 mm, refus non rigide |
| J09 (partiel) – Snap | `TestsMetier.cs::TestsSnap` | hors zone, « aucun point valide », p95 |

J02–J06 et J09 réels (maison scannée) restent à constituer avec les données privées, hors dépôt.
