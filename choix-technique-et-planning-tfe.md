# TFE Scan-to-BIM : choix technique et planning de réalisation

Proposition 0.1 du 30 septembre 2026. Ce document répond à la demande de commencer le plugin et de convenir des étapes du TFE ; le langage, les dates et les engagements restent soumis à l’accord de Thomas.

## Décision technique recommandée

**Commencer le plugin en C#, avec possibilité d’ajouter un moteur CPython externe lorsque le besoin est démontré.** Ne pas développer simultanément deux applications complètes dès le départ, et ne pas introduire C++ dans le périmètre initial.

Autodesk présente C# comme un choix naturel pour son guide de premier plugin utilisant l’API .NET de Revit ([Autodesk](https://www.autodesk.com/support/technical/article/caas/tsarticles/ts/7I2bC1zUr4VjJ3U31uM66K.html)). Pour les calculs de nuages, Open3D propose des interfaces Python et C++ sur un moteur optimisé ([Open3D](https://www.open3d.org/)).

| Partie du projet | Choix proposé | Justification pour ce TFE |
|---|---|---|
| Boutons, commandes et validations dans Revit | C# | Regrouper l’intégration, les modifications du document et leur contrôle |
| Coordonnées, transformations et calculs géométriques simples | Bibliothèque C# séparée de Revit | Permettre les tests sans ouvrir Revit et éviter une dépendance Python immédiate |
| Détection de surfaces/niveaux et filtrage nécessitant des bibliothèques spécialisées | CPython externe si utile | Expérimenter séparément, notamment avec l’accompagnement du professeur |
| Communication avec un moteur externe | Contrat de fichiers/résultats versionné | Rendre explicites unités, repères, source, paramètres et erreurs |
| Optimisation C++ spécialisée | Hors périmètre initial | N’ajouter une complexité native qu’après un blocage mesuré, pas par principe |

Cette recommandation est adaptée aux priorités actuelles, fortement liées à Revit : coupes, projections, Snap, coordonnées et mise à jour des liens. Si Thomas ou son professeur maîtrise déjà nettement mieux Python et souhaite assurer la maintenance en Python, un prototype pyRevit reste une alternative à arbitrer avant de commencer, pas un second chantier concurrent.

La version de .NET ne sera pas choisie avant d’avoir relevé la version exacte de Revit, mise à jour comprise. Le projet, les dépendances et les outils de compilation seront ensuite figés et documentés.

## Périmètre de travail

Les choix déjà actés sont architecture et structure, production centrée sur calage/niveaux/murs et préparation de l’existant pour concevoir l’annexe dans Revit. La création de portes, fenêtres, dalles et toitures reste dans Revit ; les réseaux MEP détaillés ne sont pas inclus.

Les trois différences recherchées par rapport au workflow actuel de Thomas sont :

- **Coupes et murs non plombés :** manipuler des plans explicites, rappeler des coupes et distinguer mesures, projections et objets modélisés.
- **Snap :** rendre l’accrochage plus réactif sans dégrader silencieusement le point choisi.
- **Actualisation des scans :** réparer ou actualiser les liens sans perdre le calage ni déplacer la maquette sans validation.

La détection automatique des niveaux reste un objectif du cahier des charges, avec validation utilisateur ; elle ne sera pas remplacée discrètement par un simple placement manuel. Le nettoyage et la reconnaissance du bâtiment restent des bonus.

## Démarrer sans attendre la fin de l’enquête

La nouvelle demande de démarrage permet de préparer le socle technique et de faire des essais réversibles pendant le sondage. Les résultats de l’enquête doivent néanmoins être intégrés avant de figer le périmètre de réalisation détaillé ; ils ne justifient pas de recommencer le projet à chaque réponse.

Le premier point de décision réunit les résultats disponibles du sondage, les essais techniques et l’avis du professeur. Un changement majeur après ce point devra indiquer ce qu’il remplace, son effet sur les tests et son impact sur le calendrier.

## Planning par étapes à dater ensemble

L’ordre ci-dessous est une proposition d’exécution, pas une promesse de délai. Les dates, durées et charges seront ajoutées après confirmation de la remise, de la défense, de la disponibilité hebdomadaire, du niveau de départ et des données.

| Étape | Travail principal | Livrable visible | Critère de passage | Trace pour le mémoire |
|---|---|---|---|---|
| A : préparation et essais de faisabilité | Vérifier Revit, outils, formats, matériel ; constituer les petits jeux de test ; préparer le sondage ; tester les accès indispensables | Fiche d’environnement et démonstration d’une commande en lecture seule | Le plugin se charge et lit les informations attendues ; les blocages de Snap/calage sont identifiés | Contexte, analyse FARO, méthode du sondage, justification des choix |
| B : socle et diagnostic | Identifier le nuage, ses unités, sa transformation, les niveaux et les références ; journal et tests hors Revit | Bouton « Diagnostiquer le projet » avec résultat lisible | Aucune modification du projet ; résultats contrôlés sur un jeu connu | Architecture, repères et protocole de test |
| C : calage, orientation et actualisation | Transformations réversibles, contrôle des coordonnées, axe assisté, réparation des chemins et détection des changements de scan | Parcours contrôlé « positionner/orienter/actualiser » | Points de contrôle cohérents avant/après ; pas de double transformation ; conflit détecté | Méthode de calage et essais ancien/nouveau scan |
| D : coupes, plans et Snap | Zones nommées, tranches, plans de référence, traces à plusieurs hauteurs, mesure de latence et optimisation ciblée | Démonstration sur mur non plombé et parcours de points | Source inchangée, projection explicite, Snap évalué en vitesse et précision | Comparaison ergonomique et mesures à froid/à chaud |
| E : niveaux et murs | D’abord une correspondance assistée testable, puis détection de niveaux ; création d’un mur connu, puis ajustement au nuage | Petite zone réellement modélisée et éditable | Zéro architectural correct, pas de doublons, hypothèses visibles, validation des propositions | Algorithmes, limites, erreurs et corrections |
| F : recette intégrée et comparaison | Enchaîner le workflow sur un cas non utilisé pour régler les paramètres ; essais d’erreur ; comparaison FARO et installation sur un autre poste si disponible | Version candidate et résultats bruts | Scénarios essentiels réussis ; défauts critiques corrigés ; résultats reproductibles | Résultats, discussion et limites |
| G : finalisation du TFE | Stabiliser, terminer le mémoire, préparer la démonstration et répéter la défense | Version figée, mémoire, support et démonstration de secours | Aucun ajout fonctionnel tardif ; procédure de reprise et fichiers disponibles | Conclusion, perspectives et annexes |

Les fonctions structurelles exactes devront être arrêtées lors de A : murs porteurs et références seulement, ou aussi poutres/poteaux. Dans le second cas, les exigences et la charge de E doivent être révisées avant engagement.

## Jalons intermédiaires de sécurité

### Premier jalon : un bouton utile, pas une interface complète

La première démonstration doit être une commande de diagnostic en lecture seule. Elle affiche au minimum la version de l’environnement, les nuages liés, les niveaux et les transformations accessibles, avec un message clair lorsqu’une donnée manque.

Le travail peut commencer sur un petit jeu synthétique ou un échantillon autorisé si le scan de la maison n’est pas prêt. Les conclusions sur les performances et la qualité devront ensuite être vérifiées sur les données réelles.

### Calage : ne pas reporter les cas difficiles

Les essais doivent inclure un fichier déplacé, un nouveau fichier dans le même repère et un nouveau recalage qui change le repère. Si la nouvelle géométrie ne peut pas être expliquée par une transformation globale unique, le logiciel doit le signaler plutôt que forcer une correction.

Les caches et analyses doivent être invalidés lorsque leur source change. Les niveaux et murs existants ne doivent pas suivre automatiquement une modification de nuage sans lot de changements approuvé.

### Snap et murs non plombés : valider tôt la faisabilité

Un essai réduit doit être entrepris dès A pour vérifier qu’un point fiable peut être récupéré dans le contexte de la version Revit retenue. La réalisation complète intervient en D, mais un obstacle d’API ne doit pas être découvert à la fin du TFE.

Avant E, Thomas choisira le résultat attendu pour les murs non plombés : représentation inclinée fidèle, mur vertical de référence avec écarts documentés, ou deux modes. Le simple fait de projeter une trace ne vaut pas modélisation fidèle de toute la géométrie.

### Détection : ajouter Python sur preuve d’utilité

Si les essais justifient Open3D ou une autre bibliothèque Python, la première intégration sera un aller-retour sur données connues, sans écriture automatique dans Revit. Le moteur retourne une proposition et des paramètres ; le plugin C# reste responsable de la validation et de la création dans le document.

La détection globale d’un étage ne précède pas le succès sur une zone simple et un cas ambigu. Chaque extension du périmètre doit conserver les tests précédemment réussis.

## Organisation hebdomadaire proposée

La semaine doit comporter du développement, des tests et une trace écrite ; le mémoire ne commence pas après le code. Une répartition de départ possible est de consacrer environ la moitié du temps à la réalisation, un quart aux tests/mesures et un quart à la rédaction, au sondage et à la revue, puis de l’ajuster aux premières semaines.

- **Début de semaine :** choisir un résultat démontrable et ses critères de réussite ; éviter plusieurs grosses fonctions ouvertes en même temps.
- **Pendant les séances :** faire de petites modifications, vérifier le résultat et noter les échecs utiles.
- **Fin de semaine :** enregistrer une courte démonstration, les tests et le temps réellement passé ; mettre à jour le chapitre concerné.
- **Avec le professeur :** proposer un point régulier, par exemple toutes les deux semaines, sous réserve de ses disponibilités.
- **Historique :** conserver code, paramètres et décisions dans un dépôt versionné ; les scans privés ne sont pas publiés avec le code.

L’IA peut aider à développer, expliquer et relire, mais une sortie de l’IA ne valide pas une fonction. Le jalon est réussi lorsque Thomas sait l’expliquer et que le comportement a été vérifié dans Revit.

## Construction du calendrier réel

Le calendrier sera calculé à rebours à partir de la remise écrite et de la défense. Les examens, cours, congés indisponibles et autres projets devront être retirés de la capacité, et le temps de scan/préparation des données ne sera pas considéré comme gratuit.

Le plan daté devra réserver une marge explicite pour les blocages et protéger la fin du projet pour les corrections, la rédaction et les répétitions. Proposition à discuter : réserver environ 20 % de la capacité restante aux aléas et arrêter les nouvelles fonctionnalités au moins deux semaines avant la remise écrite, si l’horizon disponible le permet.

Les premières séances de A/B serviront à mesurer le rythme réel et à réestimer la suite. En cas de dépassement, réduire d’abord l’étendue des automatismes ou le nombre de catégories, avec accord de Thomas ; ne pas supprimer les contrôles de coordonnées, les tests ou le mémoire.

## Bonus et règle d’arrêt

Le nettoyage et la segmentation bâtiment ne commencent que si le parcours essentiel passe la recette et que la marge restante couvre encore la finalisation. Ils peuvent être présentés comme perspectives si cette condition n’est pas remplie.

Si le délai se resserre, proposer un périmètre de repli explicite, par exemple une zone ou un niveau plutôt que la maison entière. Toute réduction d’un objectif validé, notamment la détection des niveaux, doit être discutée et documentée.

## Informations et accords nécessaires

| Point à confirmer | Pourquoi |
|---|---|
| Date de remise écrite et date de défense | Fixer la fin de développement et la période de finalisation |
| Heures réellement disponibles par semaine et périodes indisponibles | Transformer les étapes en calendrier tenable |
| Version exacte de Revit, mise à jour comprise | Choisir environnement .NET, références API et tests |
| Niveau de programmation et accord sur C# | Ajuster l’apprentissage et confirmer le choix plutôt que le présumer |
| Disponibilité du scan, formats et référence spatiale | Planifier acquisition/préparation et recette réelle |
| Détail structure et représentation des murs non plombés | Éviter d’engager des fonctions non encore définies |

Statut de cette proposition : langage recommandé mais non validé, étapes proposées mais non datées, aucun plugin compilé ou installé au cours de cette préparation. Après les quatre premières réponses, le prochain livrable sera un planning daté avec tâches hebdomadaires, livrables, points de contrôle et marge.
