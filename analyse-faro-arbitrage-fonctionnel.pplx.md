# FARO As-Built for Revit : audit fonctionnel et matrice d’arbitrage

Version de travail 0.11, 30 septembre 2026, après arbitrages de Thomas à 16 h 20 et ajout du sondage BIM à 16 h 23. Document destiné au TFE : exploitation du scan d’une maison pour produire une maquette Revit utilisable dans un projet d’annexe.

## Conclusion de l’audit

Le positionnement proposé n’est pas de remplacer toute la suite FARO, mais d’améliorer le parcours allant du nuage enregistré à une maquette architecturale contrôlable. Les coordonnées, l’orientation, les niveaux, la modélisation et la vérification constituent le périmètre recommandé ; les automatismes doivent rester corrigeables.

**Périmètre acté : architecture et structure ; production centrée sur calage, niveaux et murs ; rôle limité à préparer l’existant pour l’annexe.** Les arbitrages détaillés restent des propositions sauf mention explicite du choix de Thomas ; le nettoyage et la reconnaissance du bâtiment restent des bonus souhaités.

« Exclure » signifie ne pas développer dans ce plugin pour le TFE, et non déclarer la fonction inutile pour tous les utilisateurs. « Déléguer » signifie conserver le besoin dans le workflow, mais utiliser un outil existant au lieu de le réécrire.

## Retour d’usage de Thomas et conséquences

Thomas identifie les coupes/plans de référence et la projection des murs non plombés, la latence du Snap sur gros nuages et les mises à jour des liaisons/coordonnées après « ré-immatriculation » comme ses trois difficultés prioritaires. Il s’agit de son expérience rapportée, pas d’une mesure indépendante des performances de FARO.

| Priorité utilisateur | Fonctions à examiner particulièrement | Réponse proposée dans le cahier des charges |
|---|---|---|
| Coupes et murs non plombés | F06/F07, F12–F14, F21, F33–F37 | E05/E06/E24 : coupes rappelables, plans explicites, traces à plusieurs altitudes, mesure du dévers et projection contrôlée |
| Latence du Snap | F19/F21/F32/F38 | E23 : recherche bornée, point accroché vérifiable, latences à froid/à chaud et contrôle de précision |
| Mise à jour du scan | F01/F26–F28 | E22 : distinguer chemin, contenu et repère ; contrôler avant d’appliquer une nouvelle transformation |

La structure n’est plus écartée globalement, mais le choix « calage, niveaux et murs » ne suffit pas à approuver une reconnaissance automatique de poutres ou de charpente. De même, les murs non plombés doivent être traités dans le workflow ; le choix entre objet incliné fidèle et mur vertical de référence reste à valider.

## Confrontation prévue avec les élèves BIM

Thomas demande un sondage des élèves de l’option BIM de l’année en cours, afin de prendre en compte les problèmes qu’ils ont rencontrés avec FARO As-Built dans Revit. Le questionnaire proposé recueille d’abord les expériences spontanées et les outils utiles, puis la fréquence, les conséquences et les améliorations souhaitées.

La matrice sera confrontée aux résultats avant sa validation finale : un besoin absent de l’analyse pourra être ajouté et une fonction utile à la promotion pourra être réexaminée. Les fonctions hors périmètre ne seront toutefois pas réintroduites automatiquement, et les mesures de sécurité ne dépendront pas d’un vote ; aucune réponse n’a encore été collectée.

## Périmètre documentaire et degré de certitude

Le catalogue détaillé repose sur le manuel FARO de juin 2025 accessible dans une reproduction consultable, complété par les notes officielles de septembre 2026 ; il ne constitue pas un test sur une installation FARO 2026 ([manuel FARO 2025](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6), [notes FARO](https://knowledge.faro.com/Software/As-Built/As-Built_for_Autodesk_Revit/Release_Notes_for_As-Built_for_Autodesk_Revit)).

Le portail officiel renvoie vers des téléchargements et une aide en ligne ; l’aide ouverte lors du contrôle est identifiée comme 2022.1, et le téléchargement du manuel récent n’a pas livré de contenu exploitable pour cet audit ([portail des manuels](https://knowledge.faro.com/Software/As-Built/As-Built_for_Autodesk_Revit/User_Manual_for_As-Built_for_Autodesk_Revit), [aide FARO 2022.1](https://farotechnologies.mcoutput.com/faro_as-built_for_revit_2022.1/en-us/Default.htm)). L’exhaustivité revendiquée est donc celle des fonctions et commandes documentées ci-dessous, pas celle de chaque bouton d’une version installée non inspectée.

La numérotation F01–F78 est propre à cet audit : elle compte des fonctions, commandes et groupes explicitement identifiés, pas 78 commandes officiellement dénombrées par FARO. Les répétitions d’un même outil dans plusieurs rubans ne sont pas comptées deux fois.

## Corrections de l’analyse antérieure

| Sujet | Conclusion corrigée | Conséquence pour le TFE |
|---|---|---|
| « Tout est manuel » | FARO ajuste des géométries, propose des types, aligne des murs et reconnaît des profils ; ce sont des automatismes documentés ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Comparer le nombre d’actions et la qualité finale, pas opposer artificiellement « manuel » et « automatique ». |
| Coordonnées prétendument nouvelles | Acquire PC Coordinates et Linking PC Coordinates existent déjà ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | L’amélioration recherchée est le diagnostic, la sécurité et la simplicité du parcours. |
| Niveaux automatiques | Level Auto-Mode associe les composants aux niveaux proches des points choisis ; cela ne démontre pas une détection globale des étages ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Distinguer détection de surfaces, interprétation des niveaux et association aux niveaux existants. |
| Paramètres de bruit | Auto Measure Parameters calcule déjà bruit et distance moyenne entre points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer l’assistance et l’explication plutôt qu’annoncer une invention. |
| Cleanup | Il supprime des configurations, données auxiliaires, images, résultats et liens du projet ; ce n’est pas un outil de débruitage du scan ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Séparer maintenance du projet et nettoyage géométrique du nuage. |
| Résultats d’analyse « perdus » | Les résultats sont sauvegardés ; leur visualisation doit être restaurée dans Revit ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Viser une restauration plus simple, pas prétendre créer la première persistance. |
| « LOA = simple légende » | FARO propose aussi des styles d’affichage d’écarts liés aux plages USIBD LOA ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Un score de confiance algorithmique ne sera pas présenté comme une certification LOA. |
| Limites anciennes | Les limites des murs inclinés/variables figurent dans les notes 2021 ; un bug historique n’est pas automatiquement un bug encore présent en 2026 ([notes FARO](https://knowledge.faro.com/Software/As-Built/As-Built_for_Autodesk_Revit/Release_Notes_for_As-Built_for_Autodesk_Revit)). | Vérifier sur la version de comparaison avant toute affirmation devant le jury. |
| Calendrier | Les notes consultées donnent septembre 2026 pour la version 2026 ; le jour exact n’est pas établi par cette page ([notes FARO](https://knowledge.faro.com/Software/As-Built/As-Built_for_Autodesk_Revit/Release_Notes_for_As-Built_for_Autodesk_Revit)). | Ne pas reprendre le 15 septembre comme date vérifiée ici. |
| Suppressions trop rapides | Les fonctions d’aide, d’orthophoto, de maintenance et de réglage répondent à des usages documentés ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Simplifier ou déléguer selon le besoin ; leur existence ne prouve pas une mauvaise architecture. |

Cette note remplace les jugements fonctionnels trop catégoriques de l’analyse du 28 septembre. Elle ne réévalue pas le comparatif des abonnements ou modèles d’IA présent dans cet ancien document.

## Règles d’arbitrage

- **Conserver :** garder la capacité, sans chercher à copier le code ou l’interface FARO.
- **Améliorer :** garder le besoin avec une différence explicite et testable.
- **Fusionner :** regrouper plusieurs commandes derrière une même opération utilisateur.
- **Déléguer :** s’appuyer sur Revit ou un outil de préparation existant.
- **Exclure :** ne pas inclure dans le périmètre recommandé du TFE.
- **À vérifier :** preuve documentaire insuffisante pour conclure sur la version cible.
- **À arbitrer :** fonction documentée, mais choix de périmètre restant à décider par Thomas.

L’utilité et la difficulté sont des appréciations pour ce cas d’usage, pas des performances mesurées. Les références E renvoient au cahier des charges associé ; elles permettent de retrouver le traitement de chaque besoin sans imposer un ordre de développement.

## Matrice des fonctions documentées

### Entrées, niveaux et architecture

| ID | Fonction FARO et fait vérifié | Arbitrage proposé | Justification et cible |
|---|---|---|---|
| F01 | Nuages Autodesk ReCap RCP/RCS pris en charge ([notes FARO](https://knowledge.faro.com/Software/As-Built/As-Built_for_Autodesk_Revit/Release_Notes_for_As-Built_for_Autodesk_Revit)). | Améliorer | E01 : contrôler source, unités, références et chemins ; ne pas promettre tous les formats. |
| F02 | Level : placer un niveau à partir de points et créer la vue en plan associée ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E04 : niveaux proposés, choix du zéro, validation ; vue en plan facultative. |
| F03 | Level Auto-Mode : contraintes aux niveaux proches des points choisis ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E04/E07 : règle explicite de correspondance, sans confusion avec la détection. |
| F04 | Wall : placement de murs guidé par des points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E07 : mode assisté de secours dans le même outil que la détection. |
| F05 | Wall Thickness : adaptation d’un type de mur à l’épaisseur mesurée ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E07 : éviter les types dupliqués et distinguer épaisseur observée/hypothèse. |
| F06 | Fit Wall : ajustement de murs droits verticaux avec aperçu et choix de type ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E07 : propositions par zone ou niveau, acceptation individuelle ou par lot. |
| F07 | Align Walls : perpendicularité, parallélisme, axes et jonctions avec tolérances ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E08 : choix relevé fidèle/orthogonalisation ; aucune correction silencieuse. |
| F08 | Window : placement guidé de fenêtres ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer, choix de périmètre acté | E09 : fenêtres modélisées dans Revit, pas générées par le plugin actuel. |
| F09 | Door : placement guidé de portes ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer, choix de périmètre acté | E09 : portes modélisées dans Revit. |
| F10 | Opening : création d’ouvertures dans les murs ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer, choix de périmètre acté | E09 : pas de génération de baies ; leurs effets sur la détection des murs restent à gérer. |
| F11 | Column architecturale : placement par points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E12 : commande native ou assistance limitée si le bâtiment en a besoin. |

### Plans, géométrie et surfaces

| ID | Fonction FARO et fait vérifié | Arbitrage proposé | Justification et cible |
|---|---|---|---|
| F12 | Fit Plane (1 Point) : plan ajusté autour d’un point ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E06 : « Ajuster un plan », sélection rapide. |
| F13 | Fit Plane (n Points) : plan moyen sur plusieurs points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E06 : sélection guidée dans le même outil. |
| F14 | Fit Plane with Contour : plan et contour ; résultats de régression affichés ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E06/E10 : option contour, qualité et couverture conservées. |
| F15 | Fit Polygon : ajustement de chaînes de lignes coplanaires ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E06 : aide aux profils et aux limites, pas outil de reconstruction générale. |
| F16 | Save as Family : lignes enregistrées comme aide de construction en famille ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E13 : ne pas transformer cet outil en promesse de famille paramétrique automatique. |
| F17 | Intersect 2 Planes : ligne d’intersection ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E06 : opération contextuelle de géométrie. |
| F18 | Intersect 3 Planes : point ou figure d’intersection ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E06 : même module ; rejeter les configurations dégénérées. |
| F19 | Model Line : lignes avec accrochage au nuage ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E06 : utile pour les cas que l’automatisme ne traite pas. |
| F20 | CPoint : points de construction nommés ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E02/E06 : repères contrôlés, sans créer une famille pour chaque point du scan. |
| F21 | Point modes : projection et intersection avec le plan de travail ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E06 : mode affiché au moment du clic, pour éviter les erreurs de profondeur. |
| F22 | Toposurface : terrain à partir de lignes et points de construction ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E11 : conserver les points utiles ; génération de terrain hors production calage/niveaux/murs retenue. |
| F23 | Deform Shape : points hauts/bas d’une dalle ou toiture plate ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | E20 : différer la déformation automatique ; conserver l’analyse des écarts. |

### Coordonnées, préparation et navigation

| ID | Fonction FARO et fait vérifié | Arbitrage proposé | Justification et cible |
|---|---|---|---|
| F24 | Import Scan Positions from RCP : symboles aux stations disponibles ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E01/E19 : stations facultatives, sans supposer qu’elles existent dans chaque export. |
| F25 | Import Scan Positions : stations via TXT exporté de Modeler ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E01 : import de secours uniquement si les données réelles l’exigent. |
| F26 | Linking PC Coordinates : relation avec Modeler/SendToRevit malgré les transformations ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E02 : conserver la chaîne de transformations ; pas de dépendance FARO obligatoire. |
| F27 | Acquire PC Coordinates : correspondance entre repère du nuage et coordonnées partagées ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E02 : diagnostic, conflits, points de contrôle et validation explicite. |
| F28 | Align Point Cloud : points homologues pour aligner sur un modèle existant, ajouté en 2022.1 ([notes FARO](https://knowledge.faro.com/Software/As-Built/As-Built_for_Autodesk_Revit/Release_Notes_for_As-Built_for_Autodesk_Revit)). | Améliorer | E02/E03 : distinguer calage 3D et orientation de travail ; afficher les résidus. |
| F29 | Export PC Region : export PTS d’une région ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E16 : export de zone avec repère/transformations, si format retenu. |
| F30 | Ortho Image : image couleur ou rayons X à l’échelle ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E13/E16 : utile pour façades et détails ; ne pas en faire une priorité sans besoin terrain. |
| F31 | Prepare Point Cloud : images précalculées facilitant la lecture en plan ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | E05/E21 : ne pas copier cette implémentation par défaut ; mesurer d’abord la lisibilité. |
| F32 | Auto Measure Parameters : estimation du bruit et de la distance entre points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E06/E17 : proposition locale expliquée, modifiable et journalisée. |
| F33 | Define Rectangle : région rectangulaire ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E05 : même outil « Zone de travail ». |
| F34 | Define Polygon : région polygonale ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E05 : même outil, mode avancé. |
| F35 | Define Box : boîte de sélection ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E05 : sélection volumique. |
| F36 | Define Slice : tranche de nuage ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E05 : hauteur et épaisseur visibles. |
| F37 | Section Manager : sections nommées et hiérarchiques ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E05 : zones nommées par étage/objet et état réutilisable. |
| F38 | Hide/Show PC : visibilité du nuage ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E05 : indispensable pour comparer nuage et modèle. |
| F39 | Hide/Show PP : visibilité des points de protocole ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E02/E21 : visibilité des repères et annotations de contrôle. |

### Structure et réseaux

| ID | Fonction FARO et fait vérifié | Arbitrage proposé | Justification et cible |
|---|---|---|---|
| F40 | Beam : placement de familles linéaires par points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | À arbitrer | E12/D11 : structure retenue ; création de poutres à confirmer, non présumée obligatoire. |
| F41 | Column structurelle : placement guidé ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | À arbitrer | E12/D11 : même décision pour poteaux. |
| F42 | Fit Structural Element : ajustement et reconnaissance de profils ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | À arbitrer | E12/D11 : distinguer ajustement d’un objet choisi et reconnaissance de toute la charpente. |
| F43 | Align Structural Elements : alignement des éléments structurels ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | À arbitrer | E12/D11 : besoin exact non encore précisé. |
| F44 | Load Structural Families : chargement en lot de familles ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E13 : bibliothèque Revit existante, pas de doublon gratuit. |
| F45 | Fit Pipe : tuyau ajusté à partir de points, diamètre proposé ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Réseaux détaillés hors objectif architectural recommandé. |
| F46 | Place Pipe Fitting : raccords entre extrémités ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Dépend d’un module tuyauterie complet. |
| F47 | Align Pipes : contraintes géométriques de réseaux ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Ne pas engager un solveur réseau pour un usage non confirmé. |
| F48 | Set Link : connecteurs As-Built ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Pas de connecteurs propriétaires à reproduire. |
| F49 | Set Link (Auto) : connecteurs aux composants voisins ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Même justification. |
| F50 | Remove Link : suppression des connecteurs As-Built ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Sans objet si ces connecteurs ne sont pas créés. |
| F51 | Load Families : familles de raccords/accessoires ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | Chargement natif si un élément est nécessaire à la maquette. |
| F52 | Fit Round Duct : ajustement de gaines rondes ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | MEP détaillé hors périmètre recommandé. |
| F53 | Fit Rectangular Duct : ajustement de gaines rectangulaires ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Même justification. |
| F54 | Place Duct Fitting : raccords de gaines ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Dépend du module gaines. |
| F55 | Align Ducts : alignement gaines/raccords ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Même justification. |

### Analyse, contrôle et éditeur de familles

| ID | Fonction FARO et fait vérifié | Arbitrage proposé | Justification et cible |
|---|---|---|---|
| F56 | Clash Detection : conflits éléments/nuage visible, dégagement configurable ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure du périmètre choisi | E15 : Thomas a retenu la préparation de l’existant, pas le contrôle dédié de l’annexe. |
| F57 | Calculate : écarts entre surfaces Revit et nuage ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E14 : indicateurs, couverture et zones non observées. |
| F58 | Switch Values : moyenne, minimum, maximum, plus proche, nombre de points ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E14 : sélecteur explicite avec unités et définition. |
| F59 | Export Results : texte et aides géométriques ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E16 : résultats traçables et export tabulaire lisible. |
| F60 | Restore : réafficher des résultats sauvegardés sans recalcul ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E14 : restauration au besoin avec vérification de validité. |
| F61 | Hide : masquer l’analyse sans effacer les valeurs ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E14 : visibilité dans le panneau de contrôle. |
| F62 | Transfer : afficher l’analyse dans une autre vue ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E14 : conserver la relation résultat/vue sans recalcul inutile. |
| F63 | Place USIBD Legend et styles LOA associés ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E14/E16 : légende de tolérances explicite ; aucune conformité normative automatique. |
| F64 | Insert PC Region : région de points dans l’éditeur de familles ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E13 : workflow spécialisé si nécessaire ; faisabilité à vérifier avant promesse. |
| F65 | Insert Ortho Image : image mise à l’échelle avec fichier associé ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E13 : garder la possibilité, pas un nouveau moteur d’images par défaut. |

### Visualisation externe et administration

| ID | Fonction FARO et fait vérifié | Arbitrage proposé | Justification et cible |
|---|---|---|---|
| F66 | SendToRevit : vue scan externe et transfert de points vers Revit ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E19 : ne pas développer un visualiseur panoramique complet sans besoin validé. |
| F67 | Project Coordinates : coordonnées de projet pour SendToRevit ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E02 : une seule source de vérité pour les transformations. |
| F68 | Detail Line : lignes de détail depuis la vue scan ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | Revit pour les détails 2D ; E06 pour les repères issus du scan. |
| F69 | Getting Started : tutoriels et ressources ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E21 : guide court et jeu d’exemple, utiles à la démonstration. |
| F70 | License Info : gestionnaire de licence FARO ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Aucun système commercial de licence requis pour le prototype. |
| F71 | Cleanup : purge de données auxiliaires du projet ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E21 : cache séparé, purge contrôlée et effets expliqués. |
| F72 | Settings : paramètres de reconnaissance, création et analyse ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Améliorer | E21 : préréglages simples et mode avancé, pas de réglages rendus invisibles. |
| F73 | Customer Experience Improvement Program : journalisation d’usage configurable ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | Pas de télémétrie distante par défaut ; journal local suffisant. |
| F74 | Help et FARO Knowledge Base : documentation et aide ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Fusionner | E21 : aide contextuelle commune, vocabulaire BIM expliqué. |
| F75 | Worksharing Settings : préférences utilisateur dans le modèle central ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Exclure | E21 : recommander le mono-utilisateur et avertir sur les modèles collaboratifs. |
| F76 | Info : version et support ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Conserver | E21 : indispensable à la reproductibilité des essais. |
| F77 | Software Update : ouverture de la page de mise à jour ([manuel](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)). | Déléguer | E21 : installation/versionnement manuel contrôlé, pas de mise à jour silencieuse. |
| F78 | Plant Exchange figure dans l’aide officielle 2022.1 ; maintien et détail en 2025/2026 non confirmés ici ([aide FARO](https://farotechnologies.mcoutput.com/faro_as-built_for_revit_2022.1/en-us/Default.htm)). | À vérifier, puis exclusion recommandée | Échanges industriels hors cas d’usage ; ne pas le compter comme fonction 2026 vérifiée. |

## Besoins complémentaires et innovations candidates

Les lignes suivantes sont des exigences proposées pour le plugin, pas des affirmations selon lesquelles aucun autre logiciel ne les possède. L’absence d’une fonction dans les documents consultés n’établit ni son inexistence chez FARO ni une nouveauté commerciale mondiale.

| ID | Capacité envisagée | Recommandation | Pourquoi / limite | Exigence |
|---|---|---|---|---|
| N01 | Diagnostic guidé des repères et transformations réversibles | Retenir | Sécurise la priorité exprimée par Thomas. | E02 |
| N02 | Orientation proposée automatiquement, avec alternative à deux points | Retenir | Deux points donnent une direction, pas un géoréférencement complet. | E03 |
| N03 | Détection de niveaux et association au ±0,00 | Retenir | Distinguer sols, plafonds, sous-faces et demi-niveaux. | E04 |
| N04 | File de propositions accepter/rejeter/corriger | Retenir | Valider sans masquer l’incertitude. | E21 |
| N05 | Détection de murs droits par zone | Retenir comme cible recommandée | Tester une zone, puis un niveau ; pas une promesse de maison complète instantanée. | E07 |
| N06 | Détection automatique des baies | Hors périmètre choisi | Modélisation dans Revit ; les occultations restent un cas à gérer pour les murs. | E09 |
| N07 | Dalles, plafonds et pans de toiture simples | Hors périmètre choisi | Modélisation dans Revit. | E10 |
| N08 | Sélection du terrain utile à l’annexe | Conserver les données | Pas de génération de terrain par le plugin engagée. | E11 |
| N09 | Débruitage réversible avec aperçu | Bonus demandé | Mesurer les points utiles supprimés à tort. | E17 |
| N10 | Séparation bâtiment/environnement | Bonus demandé | Corrigeable ; pas de suppression définitive automatique. | E18 |
| N11 | Carte de couverture et zones non observées | Retenir pour le contrôle | Un manque de points ne vaut pas un écart nul. | E14 |
| N12 | Rapport de contrôle et traçabilité par élément | Retenir | Soutient le mémoire et l’explication des choix. | E14/E16 |
| N13 | Raccordement de l’annexe à l’existant | Délégation actée à Revit | Le plugin prépare l’existant, sans module dédié de conception ou contrôle d’annexe. | E15 |
| N14 | Comparaison de deux scans dans le temps | Exclure du TFE recommandé | Nouveau protocole de recalage et de validation. | Hors périmètre |
| N15 | Matériaux/couches déduits automatiquement | Exclure comme donnée certaine | Un aspect visible ne justifie pas une composition constructive. | Hors périmètre |
| N16 | Équipements automatiquement reconnus | Exclure du TFE recommandé | Classes supplémentaires et jeux d’évaluation spécifiques. | Hors périmètre |
| N17 | Copilote en langage naturel | Exclure du socle | Aucun bénéfice prioritaire démontré pour le calage ou la précision. | Hors périmètre |
| N18 | Apprentissage à partir des corrections | Exclure du socle | Enregistrer une correction n’entraîne pas automatiquement un modèle. | Hors périmètre |
| N19 | Nuages de milliards de points, moteur dédié | Exclure comme promesse | Fixer d’abord une enveloppe de données et de matériel testée. | E21 |
| N20 | Collaboration cloud/ACC/BCF | Exclure du socle | Pas nécessaire à la démonstration mono-utilisateur recommandée. | Hors périmètre |
| N21 | Plans, coupes et façades prêts à imprimer | Déléguer à Revit | Création de vues liée aux niveaux possible ; cotation complète non engagée. | E04/E16 |
| N22 | Murs non plombés et autres géométries complexes | Scinder les besoins | Projection/dévers prioritaires ; représentation inclinée à arbitrer ; patrimoine et formes complexes restent séparés. | E20/E24 |
| N23 | Actualisation des scans et des liaisons | Retenir comme priorité proposée | Contrôler chemin, contenu, repère et invalidation des résultats ; pas de déplacement silencieux du modèle. | E22 |
| N24 | Snap réactif avec précision contrôlée | Retenir comme priorité proposée | Mesurer la latence réelle et préserver la qualité du point choisi. | E23 |
| N25 | Gestion coordonnée des coupes et plans de projection | Retenir comme priorité proposée | Rappeler et comparer les traces à plusieurs altitudes sans déformer la source. | E05/E06/E24 |

## Décisions à ne pas prendre sur une simple lecture de manuel

- **Ergonomie :** mesurer les actions et les corrections sur une tâche identique avant de qualifier FARO de lent ou compliqué.
- **Qualité :** comparer des géométries et des niveaux sur une référence indépendante ; pas seulement des captures visuelles.
- **Robustesse :** les corrections de bugs documentées ne prouvent pas que la version installée présente encore ces bugs.
- **Formats :** distinguer lecture pour le calcul, affichage dans Revit et export ; ce sont trois engagements différents.
- **Automatisation :** annoncer les catégories, préconditions et limites exactes plutôt que « reconnaître toute la maison ».
- **Comparaison commerciale :** aucune promesse de supériorité globale à ce stade ; le résultat devra être démontré sur le périmètre retenu.

## Conditions pour fermer l’audit

L’analyse documentaire permet de préparer les arbitrages maintenant. La clôture complète nécessite encore la version FARO utilisée pour la comparaison, une inspection de son ruban ou de son manuel installé, un essai ciblé des fonctions critiques et la confrontation aux résultats du sondage de la promotion.

Les contrôles prioritaires incluent F02/F03 pour les niveaux, F06 pour les murs, F27/F28 pour les coordonnées, F12–F14/F33–F37 pour les coupes et plans, F19/F21 pour le Snap et F57–F63 pour l’analyse. D01–D03 sont désormais actés ; la matrice détaillée, les fonctions structurelles, la représentation des murs non plombés et les seuils restent à finaliser avant approbation de la version 1.0.
