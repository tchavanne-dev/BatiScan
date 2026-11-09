# Cahier des charges du plugin Revit Scan-to-BIM

Version 0.12 du 30 septembre 2026, intégrant les arbitrages fonctionnels, le sondage BIM et la demande de démarrage/planning à 16 h 54. Porteur : Thomas Chavanne ; contexte : TFE, relevé d’une maison par scanner FARO, production de l’existant dans Revit et utilisation de cette maquette pour étudier une annexe.

## Statut et règles de validation

Ce document définit les besoins, les résultats attendus, les limites et les tests, sans arrêter le langage ni les phases de construction. Il complète la matrice « FARO As-Built for Revit : audit fonctionnel et matrice d’arbitrage », dont les identifiants F et N sont repris ci-dessous.

**Statut actuel : périmètre partiellement validé, exigences détaillées à finaliser.** Les décisions de la section suivante sont actées par Thomas ; les méthodes, seuils de test et fonctionnalités supplémentaires restent des propositions. Les mots « doit » ne signifient pas que tous les détails de cette version ont été approuvés.

Les niveaux de priorité sont des propositions de périmètre, pas des phases : P0 = indispensable au résultat recommandé ; P1 = utile, à accepter explicitement avec sa charge ; P2 = bonus facultatif ; HP = hors périmètre recommandé. Une fonction P1 ou P2 n’entre pas dans l’engagement de réalisation tant qu’elle n’a pas été acceptée.

La version 1.0 sera créée après arbitrage des questions bloquantes. À la demande de Thomas du 30 septembre à 16 h 54, une proposition de choix technique et de planning est maintenant préparée en parallèle : le socle et les essais réversibles peuvent démarrer après accord, sans attendre la clôture de l’enquête ; les engagements détaillés seront confirmés après les résultats et essais.

## Décisions actées et retours d’usage

| Décision de Thomas | Portée actée | Ce qui n’est pas implicitement approuvé |
|---|---|---|
| Architecture et structure | Les besoins structurels font partie de l’analyse et du périmètre métier ; réseaux MEP détaillés exclus | Une reconnaissance complète de charpente ou tous les outils poutres/poteaux |
| Calage, niveaux et murs | Le plugin concentre sa production de maquette sur ces fonctions | Création automatique des baies, dalles et toitures ; ces objets restent traités dans Revit |
| Préparer l’existant | La maquette sert ensuite à concevoir l’annexe dans Revit | Module de conception d’annexe ou de détection dédiée de ses interférences |
| Nettoyage et reconnaissance du bâtiment | Bonus souhaités, conformément à la demande initiale | Obligations nécessaires à la livraison du cœur du plugin |

Thomas signale trois difficultés dans son usage de FARO : la gestion des coupes et plans de référence avec projection des murs non plombés ; la latence lors du Snap dans les gros nuages ; la mise à jour des liaisons et coordonnées après une « ré-immatriculation du scan ». Ces observations sont un retour utilisateur, pas des défauts universels démontrés par cet audit.

La traduction proposée en exigences prioritaires est E05/E06/E24 pour les coupes et projections, E23 pour le Snap et E22 pour la mise à jour des scans. Le terme « ré-immatriculation » reste à préciser ; E22 distingue par sécurité renommage/déplacement de fichier, nouvel export et nouveau recalage des stations.

## Étude des besoins auprès de la promotion BIM

**Décision actée : intégrer au TFE un sondage des élèves de l’option BIM de l’année en cours.** Il vise à identifier leurs difficultés d’utilisation de FARO As-Built dans Revit et à faire évoluer les arbitrages du plugin, sans se limiter aux problèmes personnels de Thomas.

Le document « Sondage des élèves BIM : utilisation de FARO As-Built dans Revit » contient le questionnaire prêt à transposer et le protocole proposé. Aucun formulaire n’est encore diffusé et aucun résultat n’est disponible.

- **S01, recueil :** interroger les élèves sur leur pratique réelle, les fonctions utiles, les difficultés spontanées, leur fréquence, leurs conséquences et leurs priorités ; séparer les réponses des non-utilisateurs.
- **S02, protection :** ne pas demander de nom ni d’e-mail, vérifier les paramètres de la plateforme, faire valider la diffusion et les modalités de conservation par l’encadrant ; ne pas annoncer un anonymat non vérifié.
- **S03, analyse :** publier effectifs et distributions, exclure les non-utilisateurs des dénominateurs de fréquence, coder les réponses ouvertes et conserver les retours positifs ou contradictoires.
- **S04, arbitrage :** relier chaque résultat utile à une exigence E et documenter la décision ; ni les votes ni le nombre de plaintes ne remplacent les obligations de sécurité ou les limites du TFE.
- **S05, validation :** présenter la synthèse à Thomas et au professeur avant de figer le cahier des charges et les phases ; les décisions déjà prises ne changent pas automatiquement.

Ces exigences concernent la démarche du TFE, pas une fonction « sondage » à intégrer dans le plugin. Les trois difficultés de Thomas sont des hypothèses de recherche à confronter aux réponses, pas des résultats attribués à toute la classe.

## Problème à résoudre

Le projet doit permettre de transformer un relevé existant en maquette Revit exploitable, sans perdre les coordonnées du relevé ni dissimuler les approximations de modélisation. La priorité exprimée porte sur le calage, l’orientation et les niveaux ; la reconnaissance du bâtiment et le nettoyage sont des bonus.

Le coût actuel en temps et en erreurs n’a pas encore été mesuré. Le TFE devra établir cette référence sur les tâches signalées par Thomas, en séparant délai du Snap, temps de gestion des coupes et temps de remise en cohérence après actualisation du scan, plutôt que partir d’un pourcentage de gain supposé.

## Objectifs et résultats attendus

- **Fiabilité spatiale :** préserver les relations de coordonnées, l’altitude de référence et l’orientation ; aucune transformation non journalisée.
- **Préparation du projet :** proposer un alignement et des niveaux, puis permettre de les valider ou de les corriger.
- **Modélisation contrôlée :** produire des objets Revit éditables pour les catégories retenues, sans prétendre connaître les parties invisibles.
- **Qualité démontrable :** fournir des mesures d’écart, de couverture et de temps total, incluant les corrections humaines.
- **Reproductibilité :** permettre à un autre utilisateur de reproduire le workflow avec la version, les paramètres et les données de test fournis.

Les seuils provisoires de la section « Recette » sont des cibles de conception à faire valider. Ils ne constituent ni une précision annoncée du scanner ni des performances déjà obtenues.

## Non-objectifs recommandés

- **Remplacement intégral de FARO :** les fonctions MEP industrielles, licences commerciales et échanges spécialisés ne sont pas reprises.
- **Recalage des stations brutes :** le nuage de départ est supposé assemblé ; l’assemblage multi-stations et le géoréférencement topographique initial restent en amont.
- **Conception de l’annexe :** le rôle validé du plugin est de préparer l’existant ; conception et contrôles propres à l’annexe restent dans Revit ou d’autres outils.
- **Baies, dalles et toitures :** leur génération par le plugin est exclue du périmètre choisi ; leur présence dans le scan doit néanmoins être prise en compte pour ne pas fausser les murs ou les niveaux.
- **Reconstruction universelle :** aucune garantie sur tous les murs irréguliers, matériaux, réseaux ou bâtiments patrimoniaux.
- **Certification :** aucun score algorithmique ni carte d’écarts ne vaut, à lui seul, certification normative ou validation structurelle.

## Utilisateurs et scénarios

| ID | Utilisateur | Besoin et bénéfice |
|---|---|---|
| U01 | Étudiant modeleur | Identifier le repère d’un nuage avant de l’utiliser pour éviter un décalage invisible. |
| U02 | Modeleur de l’existant | Orienter le travail sans perdre la localisation réelle. |
| U03 | Modeleur de l’existant | Associer les niveaux détectés au zéro architectural et aux niveaux déjà présents. |
| U04 | Modeleur de l’existant | Valider des objets proposés, les corriger et poursuivre avec les outils natifs. |
| U05 | Concepteur de l’annexe | Conserver les façades de raccordement, seuils et terrain nécessaires. |
| U06 | Professeur ou évaluateur | Reproduire les essais, consulter les hypothèses et contrôler les écarts. |
| U07 | Élève de la promotion BIM actuelle | Exprimer son expérience FARO, positive ou problématique, afin de contribuer aux choix du TFE sans être évalué. |

Les variantes à prendre en charge dans les tests sont : projet vide, projet avec coordonnées établies, niveaux existants, nuage local non géoréférencé, nuage partiel, plusieurs fichiers et ouverture ultérieure sans le moteur d’analyse. Les cas impossibles à traiter doivent être signalés sans modification du document.

## Exigences fonctionnelles

### E01 : Identifier et charger les données

Priorité proposée : P0. Traçabilité : F01, F24, F25 ; dépendances : version Revit cible et formats réels du relevé.

Le plugin doit lister les nuages liés, leur fichier source, leur état d’accès et les informations disponibles sur unités et repères. Il doit distinguer un nuage déjà lié d’un fichier à préparer ; une lecture externe E57 éventuelle ne signifie pas automatiquement un affichage direct E57 dans Revit.

- **Cas nominal :** étant donné un lien valide, lorsque le diagnostic est lancé, alors le bon fichier et sa transformation sont identifiés.
- **Donnée incomplète :** étant donné un repère inconnu, lorsque le fichier est reconnu, alors le statut reste « repère non confirmé », jamais « géoréférencement vérifié ».
- **Erreur :** étant donné un fichier absent ou un format non supporté, lorsque l’opération commence, alors un message précis apparaît et aucun objet n’est créé.

### E02 : Préserver les coordonnées et établir le calage

Priorité proposée : P0. Traçabilité : F20, F26–F28, F39, F67, N01 ; dépendance : E01.

Le plugin doit distinguer repère source, repère de calcul, transformation de l’instance, repère interne Revit et coordonnées partagées. Il doit conserver unités, origine, orientation, altitude de référence, transformations et inverses, avec provenance et statut de vérification.

La préservation n’est pas une simple opération d’affichage : Autodesk précise que Revit n’utilise pas automatiquement le système SIG renseigné dans le RCP ReCap ([Autodesk](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Point-clouds-don-t-align-in-Revit.html)). Les conversions d’unités doivent également être explicites, les longueurs internes de l’API étant exprimées en pieds ([Autodesk, unités](https://help.autodesk.com/cloudhelp/2025/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Application_and_Document/Revit_API_Revit_API_Developers_Guide_Introduction_Application_and_Document_Units_html.html)).

- **Calage connu :** étant donné des repères compatibles et des points de contrôle, lorsque la transformation est proposée, alors les résidus et le sens de transformation sont affichés avant application.
- **Projet protégé :** étant donné des coordonnées partagées déjà établies, lorsque le nuage est incompatible, alors le conflit est signalé et aucun écrasement automatique n’a lieu.
- **Réversibilité :** étant donné une transformation validée, lorsque les coordonnées locales sont reconverties, alors les coordonnées source sont retrouvées dans la tolérance numérique fixée.
- **Limite :** étant donné un changement de système géographique ou altimétrique nécessaire mais non pris en charge, alors le plugin demande une préparation externe ; il ne simule pas une reprojection par simple déplacement.

### E03 : Orienter le projet de travail

Priorité proposée : P0. Traçabilité : F28, N02 ; dépendance : E02.

Le plugin doit proposer une orientation issue d’un axe choisi ou de directions détectées, en distinguant le Nord de travail et la référence géographique connue. Le parcours assisté doit permettre de définir l’axe par deux points une fois l’outil lancé ; la validation n’est pas comptée dans ces deux clics.

- **Mode assisté :** étant donné une verticale et un repère valides, lorsque deux points distincts définissent un axe en plan, alors un aperçu indique la rotation proposée.
- **Mode automatique :** étant donné plusieurs directions plausibles, lorsque l’analyse est lancée, alors les alternatives restent visibles et le logiciel ne décide pas silencieusement du sens.
- **Sécurité :** après validation puis annulation ou réouverture, les contrôles de E02 restent conformes ; un modèle existant n’est jamais déplacé sans liste des éléments affectés.

### E04 : Détecter et associer les niveaux

Priorité proposée : P0. Traçabilité : F02/F03, N03/N21 ; dépendances : E02 et verticale confirmée.

Le plugin doit analyser les altitudes et surfaces observées pour proposer des niveaux, présenter les niveaux Revit existants et demander le niveau de référence ±0,00. Il doit distinguer altitude absolue, hauteur relative, nom du niveau et convention sol fini/structure/sous-face.

- **Association :** étant donné un sol de référence connu, lorsque l’utilisateur le définit comme zéro, alors les niveaux relatifs et l’altitude extérieure restent simultanément disponibles.
- **Non-duplication :** étant donné des niveaux existants, lorsque les candidats sont validés ou la commande relancée, alors aucune duplication n’est créée sans décision explicite.
- **Ambiguïté :** étant donné une table, un plafond, une pente ou un demi-niveau, alors le candidat peut être rejeté ou reclassé ; le point le plus bas n’est pas automatiquement le rez-de-chaussée.
- **Création :** les niveaux et vues en plan demandés sont récapitulés avant application ; les vues sont optionnelles.

### E05 : Définir la zone de travail

Priorité proposée : P0. Traçabilité : F31, F33–F38 ; dépendance : E01.

Le plugin doit proposer boîte, tranche et sélection de zone, avec nom, limites, visibilité et nuages concernés. Il doit distinguer la zone affichée, la zone calculée et les points exclus ; les coupes doivent pouvoir être nommées, rappelées et associées à un niveau ou à un plan de référence, sans modifier les coordonnées du nuage.

- **Sélection :** lorsque la zone change, alors son volume et son épaisseur sont visibles dans l’aperçu.
- **Calcul :** lorsque l’analyse commence, alors le périmètre réellement traité est annoncé, même s’il diffère de la vue courante.
- **Persistance :** après réouverture, les zones sauvegardées retrouvent leur repère ; elles sont invalidées si la source ou sa transformation a changé.
- **Comparaison en hauteur :** étant donné un mur non plombé, lorsque l’utilisateur passe entre deux tranches d’altitudes connues, alors les traces et leurs altitudes restent identifiables sans écrasement de la précédente.

### E06 : Mesurer et ajuster des géométries de référence

Priorité proposée : P0 pour plans de référence et projection, P1 pour contours et intersections avancées. Traçabilité : F12–F21, F32 ; dépendances : E02/E05 ; usage pour les murs non plombés détaillé en E24.

Le plugin peut proposer un outil commun de plan, contour, ligne et point de contrôle, avec estimation des paramètres et affichage de la qualité. Il doit permettre un réglage manuel documenté et conserver les points ou zones utilisés.

- **Plan :** étant donné une zone plane, lorsque l’ajustement est demandé, alors plan, résidus, nombre de points et couverture sont fournis.
- **Intersection :** étant donné des plans parallèles ou presque dégénérés, alors l’opération refuse un résultat instable.
- **Reproductibilité :** paramètres, sélection et méthode utilisés sont retrouvables dans le journal.

### E07 : Produire des murs Revit contrôlables

Fonction actée : murs dans le cœur du plugin. Priorité proposée : P0 ; niveau d’automatisation et représentation des murs non plombés à préciser. Traçabilité : F04–F06, N05 ; dépendances : E02/E04/E05/E21/E24.

Le périmètre proposé couvre la création de murs à partir de propositions par zone ou d’une sélection assistée, puis validées avant création. Le plugin doit distinguer faces observées, épaisseur supposée, type choisi, limites de hauteur et zones masquées ; la gestion des murs non plombés est une exigence à part entière de E24, dont le choix de représentation finale n’est pas encore tranché.

- **Objet :** après acceptation, le résultat est un mur Revit éditable avec type, niveau de base, hauteur ou contrainte haute et provenance.
- **Face unique :** si une seule face est visible, l’épaisseur est demandée ou reprise d’une hypothèse explicitement affichée ; elle n’est pas présentée comme mesurée.
- **Types :** aucun type partagé existant n’est modifié silencieusement et les types équivalents sont réutilisés.
- **Reprise :** une relance ne double pas les murs déjà créés ; une géométrie courbe ou inclinée non supportée est signalée, pas discrètement redressée.

### E08 : Régulariser les murs sans falsifier le relevé

Priorité proposée : P1. Traçabilité : F07 ; dépendance : E07.

Le plugin doit séparer « suivre l’existant » et « régulariser sous tolérance ». Il doit montrer les déplacements, rotations, jonctions et conséquences sur les éléments hébergés.

- **Comparaison :** lorsque l’orthogonalisation est proposée, alors l’utilisateur voit l’état mesuré et l’état régularisé.
- **Tolérance :** une modification hors tolérance est refusée ou explicitement soumise à décision.
- **Hébergement :** aucune porte/fenêtre n’est laissée silencieusement incohérente ; les éléments verrouillés ou contraints provoquent un avertissement.

### E09 : Proposer des baies, portes et fenêtres

Statut après arbitrage : HP, modélisation dans Revit. Traçabilité : F08–F10, N06 ; dépendance éventuelle : E07. Les critères ci-dessous sont conservés comme référence pour une extension future, pas comme engagement du TFE.

Le plugin doit distinguer ouverture géométrique, catégorie métier et famille Revit. Une zone sans points peut rester « non observée » ; elle n’est pas automatiquement assimilée à une fenêtre.

- **Proposition :** une baie candidate présente largeur, hauteur, allège, hôte et éléments effectivement observés.
- **Validation :** catégorie, type et sens d’ouverture sont confirmés avant création.
- **Erreur :** en cas de famille absente ou de dimensions non compatibles, le logiciel conserve le candidat et explique le blocage.

### E10 : Dalles, plafonds et toitures simples

Statut après arbitrage : HP, modélisation dans Revit. Traçabilité : F14, N07 ; dépendances éventuelles : E04/E06. Les critères ci-dessous concernent uniquement une éventuelle extension future.

Le périmètre proposé se limite aux surfaces et contours simples sélectionnés ou proposés, sans reconstruire automatiquement toutes les couches du bâtiment. Les épaisseurs et compositions non observées restent des choix utilisateur.

- **Création :** un contour fermé valide produit la catégorie Revit retenue et un objet éditable.
- **Trou de données :** les limites insuffisamment observées restent à compléter et à valider.
- **Limite :** toiture complexe ou contour incohérent entraîne un refus explicite, pas une fermeture arbitraire.

### E11 : Conserver et exploiter le terrain de l’annexe

Priorité proposée : P0 pour ne pas altérer les données de terrain ; HP pour génération d’un terrain par le plugin dans le périmètre calage/niveaux/murs retenu. Traçabilité : F22, N08 ; dépendances : E02/E05.

Le plugin doit permettre de protéger la zone de terrain utile au projet, avec altitude et limites conservées. Une création de terrain peut être ajoutée sur une sélection contrôlée, dans la catégorie compatible avec la version Revit retenue.

- **Protection :** les filtres bâtiment/environnement ne suppriment pas une zone de terrain marquée « à conserver ».
- **Modélisation :** la surface proposée reste liée aux points sélectionnés et affiche les zones interpolées.
- **Borne :** une surface scan ne constitue pas une validation de limites de propriété.

### E12 : Structure limitée

Statut : structure incluse dans le périmètre métier validé ; fonctions exactes à arbitrer. Traçabilité : F11, F40–F44. Aucune exclusion globale du besoin structurel ne peut être déduite du choix de concentrer la maquette sur le calage, les niveaux et les murs.

Le point ouvert est de savoir si « structure » désigne ici les murs porteurs et les références nécessaires à leur modélisation, ou aussi l’ajustement et la création de poutres/poteaux. Dans le second cas, un sous-périmètre devra être approuvé avant de rendre ces fonctions obligatoires ; la reconnaissance d’une charpente entière n’est pas présumée acceptée.

- **Validation :** aucune section ou qualité de matériau non observée n’est affirmée.
- **Compatibilité :** catégorie, axe, rotation et niveaux sont inspectables après création.
- **Limite :** aucun calcul de stabilité ou dimensionnement structurel n’est inclus.

### E13 : Familles et références de détail

Priorité proposée : P0 pour choix/réutilisation des types de murs ; HP pour workflows spécialisés de familles dans le périmètre actuel. Traçabilité : F16, F30, F44, F51, F64/F65.

Le plugin doit privilégier les familles déjà présentes et distinguer objet paramétrique, géométrie de référence et image. Le nuage dans l’éditeur de familles et les orthophotos sont des options à vérifier séparément.

- **Famille existante :** son utilisation ne modifie pas les autres instances par changement silencieux de type.
- **Famille absente :** l’utilisateur est informé du fichier requis et peut abandonner sans objet partiel.
- **Référence :** une image ou un contour n’est jamais déclaré équivalent à une famille paramétrique complète.

### E14 : Mesurer les écarts et la couverture

Priorité proposée : P0 pour mesures et traçabilité ; P1 pour visualisation colorée avancée. Traçabilité : F57/F58, F60–F63, N11/N12 ; dépendances : E02/E05 et éléments de référence.

Le module doit préciser la distance mesurée, son sens, l’échantillonnage, la zone, les unités, les seuils et les surfaces éligibles. Il doit séparer les zones hors tolérance des zones non observées et distinguer une estimation de qualité d’une certification.

- **Mesures :** sur des surfaces synthétiques d’écart connu, les résultats correspondent au protocole choisi dans la tolérance numérique fixée.
- **Couverture :** une surface sans points n’obtient pas un écart nul ou un statut conforme.
- **Persistance :** paramètres et résultats peuvent être relus après fermeture ; un changement de source, transformation ou élément rend le résultat « à recalculer ».
- **Interprétation :** score de confiance, écart géométrique et tolérance projet sont affichés comme notions distinctes.

### E15 : Contrôle local de raccordement de l’annexe

Statut après arbitrage : HP ; le rôle validé est « préparer l’existant ». Traçabilité : F56, N13. Les critères ci-dessous ne s’appliqueraient qu’après une nouvelle décision d’ajouter ce module.

Le plugin peut rechercher des contacts ou interférences dans une zone choisie autour de l’annexe, sans concevoir celle-ci. L’utilisateur doit connaître le périmètre observé et les dégagements retenus.

- **Alerte :** une interférence contient un élément, une position, un critère et une vue ou sélection de contrôle.
- **Absence de preuve :** l’absence de points ne prouve pas l’absence d’obstacle.
- **Usage :** le résultat reste une aide géométrique, pas une validation réglementaire ou structurelle.

### E16 : Exporter les résultats et transmettre le projet

Priorité proposée : P0 pour rapport/CSV ; P2 pour exports de nuages supplémentaires. Traçabilité : F29/F30, F59/F63, N12/N21 ; dépendances : E02/E14/E21.

Le plugin doit produire un rapport lisible et un export tabulaire des résultats, avec versions, repères, unités, sources, paramètres et éléments concernés. Le paquet de transmission doit identifier les fichiers externes nécessaires, sans les publier ni les envoyer automatiquement.

- **Rapport :** chaque valeur exportée peut être reliée à un résultat et à son contexte de calcul.
- **Réouverture :** le projet transmis signale les liens manquants et ne présente pas de résultats anciens comme actuels.
- **Export de points optionnel :** une zone exportée conserve la définition du repère et les transformations ; le retour dans Revit fait l’objet d’un test distinct.

### E17 : Nettoyer les points parasites de manière réversible

Priorité proposée : P2, bonus demandé par Thomas. Traçabilité : F32, N09 ; dépendances : E01/E02/E05.

Le module doit proposer une sélection de points suspects avec paramètres, aperçu avant/après et possibilité de restauration. Il ne doit jamais écraser le fichier original ni confondre filtre d’affichage, masque d’analyse et fichier effectivement nettoyé.

- **Aperçu :** points conservés et rejetés peuvent être visualisés séparément.
- **Restauration :** la désactivation du masque retrouve les points initiaux.
- **Contrôle :** détails fins et zones peu denses sont inclus dans le jeu d’essai ; les faux rejets sont mesurés.
- **Limite :** aucune promesse de supprimer tous les points parasites sans perte.

### E18 : Séparer bâtiment et environnement

Priorité proposée : P2, bonus demandé par Thomas. Traçabilité : N10 ; dépendances : E05/E17 si filtrage préalable.

Le module doit proposer une segmentation ou sélection bâtiment/environnement avec correction manuelle et zones protégées. La version de base peut rester une sélection assistée ; la classification sémantique complète est une ambition distincte.

- **Correction :** une zone mal classée peut être réaffectée sans recommencer l’ensemble.
- **Terrain :** les zones protégées de E11 restent conservées.
- **Ambiguïté :** végétation contre façade, mobilier et voisinage sont documentés comme cas difficiles, pas supprimés sans contrôle.

### E19 : Stations et panoramas

Priorité proposée : P2 pour positions disponibles, HP pour visualiseur complet propre au TFE. Traçabilité : F24/F66.

Le plugin peut afficher les stations contenues dans les données et ouvrir un outil existant si cette intégration est disponible. L’absence d’images ou de stations ne doit bloquer ni le calage ni la modélisation.

- **Données présentes :** chaque station utilise le même calage que le nuage.
- **Données absentes :** le module est désactivé avec explication.
- **Limite :** aucune image panoramique n’est reconstruite ou promise à partir des seuls XYZ.

### E20 : Géométries complexes

Priorité proposée : HP pour reconstruction patrimoniale et géométries complexes ; ne concerne pas l’exigence prioritaire de coupes et projection des murs non plombés E24. Traçabilité : F23, N22.

Les murs courbes, à épaisseur variable, voûtes et déformations automatiques de dalles restent hors périmètre recommandé. Le traitement des murs non plombés ne peut plus être simplement exclu : l’analyse et la projection sont prioritaires dans E24, tandis que la création de leur géométrie inclinée exacte attend une décision de Thomas.

- **Sécurité :** un cas non supporté n’est pas forcé dans une catégorie incorrecte.
- **Traçabilité :** le rapport liste les éléments exclus ou simplifiés.
- **Évolution :** chaque catégorie ajoutée nécessite ses propres données et critères de recette.

### E21 : Validation, sécurité et exploitation

Priorité proposée : P0. Traçabilité : F39, F69–F77, N04/N19 ; dépendance transversale à toute modification.

Le plugin doit afficher les propositions, modifications et suppressions avant application, permettre accepter/rejeter/corriger et annuler un lot. Il doit fournir progression, annulation du calcul, diagnostic, version, aide courte, paramètres et journal local.

- **Validation :** aucun objet définitif n’est créé par une simple détection.
- **Échec :** une erreur laisse le document dans un état cohérent, sans lot partiellement appliqué non signalé.
- **Réexécution :** les objets déjà traités sont identifiés ; la relance propose actualisation, conservation ou nouvelle instance explicite.
- **Maintenance :** une purge ne touche qu’aux données appartenant au plugin et détaille ce qui sera effacé.
- **Déconnexion :** les objets créés restent consultables et éditables sans moteur externe ; les fonctions avancées peuvent devenir indisponibles mais doivent l’indiquer.

### E22 : Actualiser un scan sans casser les liaisons et les coordonnées

Priorité proposée : P0, issue du retour d’usage de Thomas. Traçabilité : F01/F26–F28, N23 ; dépendances : E01/E02/E21.

Le plugin doit distinguer la modification du chemin d’un fichier de la modification de son contenu ou de son repère. Un nouvel export ou recalage ne doit pas entraîner une réapplication aveugle de l’ancienne transformation, ni un déplacement automatique de la maquette existante.

- **Chemin seul :** étant donné un fichier identique déplacé ou renommé, lorsque le lien est réparé, alors le calage est conservé et contrôlé.
- **Contenu mis à jour :** étant donné un nouveau nuage annoncé dans le même repère, alors les points de contrôle sont revérifiés, les caches invalidés et les analyses marquées à recalculer.
- **Repère modifié :** étant donné un nouveau recalage, alors ancien/nouveau repères et correspondances sont présentés avec résidus avant validation ; les déplacements du lien et du modèle ne sont pas confondus.
- **Recalage non rigide entre stations :** si les résidus ne sont pas compatibles avec une transformation globale unique, alors le plugin ne prétend pas tout corriger par une seule rotation/translation et demande une vérification des zones affectées.
- **Comparaison :** l’utilisateur peut consulter l’état précédent et annuler le changement de lien/calage ; le journal conserve la filiation entre sources.
- **Protection :** aucune mise à jour des niveaux ou des murs existants n’est déclenchée simplement parce que le nuage a changé ; les modifications éventuelles constituent un lot séparé à approuver.

### E23 : Accrochage au nuage réactif et vérifiable

Priorité proposée : P0, issue du retour d’usage de Thomas. Traçabilité : F19/F21/F32/F38, N24 ; dépendances : E01/E05/E21.

Le plugin doit limiter la recherche de points à une zone explicitement choisie et donner un retour visuel sur le candidat avant confirmation. Si un échantillon allégé sert à l’affichage, la position retenue doit être affinée sur les données de référence disponibles, ou signalée comme approximative.

- **Périmètre :** un nuage ou une zone exclu de la recherche ne peut pas fournir silencieusement le point accroché.
- **Précision :** le point effectivement enregistré et sa projection éventuelle sont distingués ; le gain de vitesse ne justifie pas une position non annoncée.
- **Échec :** en absence de candidat satisfaisant, l’outil indique « aucun point valide » au lieu d’accrocher un objet voisin ou une ancienne valeur.
- **Performance :** l’essai mesure séparément premier accès, préparation du cache et accrochages suivants ; la latence médiane et le percentile 95 sont publiés sur un parcours fixe.
- **Actualisation :** un cache lié à une ancienne source ou transformation est invalidé conformément à E22.

### E24 : Gérer les coupes et la projection des murs non plombés

Priorité proposée : P0 pour lecture, mesure et projection contrôlée ; représentation finale du mur à arbitrer. Traçabilité : F06/F07/F12–F14/F21/F33–F37, N22/N25 ; dépendances : E02/E04/E05/E06.

Le plugin doit permettre de comparer des traces à plusieurs altitudes, de mesurer le dévers d’une zone observée et de projeter une trace ou un point sur un plan explicitement choisi. Le plan de coupe, le plan de projection, l’altitude et la direction de projection doivent être visibles ; ni la projection ni le redressement du modèle ne doivent modifier le nuage source.

- **Représentations séparées :** points mesurés, traces de coupe et géométrie projetée sont identifiables comme trois données différentes.
- **Dévers :** sur un mur synthétique incliné d’une valeur connue, l’écart entre traces à deux altitudes est retrouvé dans la tolérance fixée pour l’essai.
- **Projection :** une modification du plan cible actualise l’aperçu sans déplacer les points source ni changer leurs coordonnées partagées.
- **Incertitude :** une face trop partielle ou irrégulière ne produit pas une inclinaison présentée comme certaine pour tout le mur.
- **Décision en attente :** soit créer une représentation inclinée fidèle lorsque la géométrie et la version cible le permettent, soit créer un mur vertical de référence avec écarts documentés, soit offrir les deux modes. Aucun de ces choix n’est encore acté.

## Contrat des données et des interfaces

Le contrat reste indépendant du choix Python/C#/C++. La mise en œuvre devra respecter le schéma logique suivant, qu’il soit stocké dans le projet, un fichier associé ou les deux.

| Objet | Informations minimales |
|---|---|
| Source de nuage | Identifiant, chemin, empreinte/version, format, unités, date du relevé si connue, stations disponibles |
| Référence spatiale | Repère source, système horizontal si connu, référence verticale si connue, origine locale, unités, statut confirmé/inconnu |
| Transformation | Repère départ/arrivée, rotation, translation, conversions d’unités, inverse, points de contrôle, résidus, date de validation |
| Niveau candidat | Identifiant, altitude source, hauteur relative, type de surface supposé, couverture, justification, association Revit |
| Objet proposé | Catégorie, géométrie, paramètres observés, paramètres supposés, provenance, état accepter/rejeter/corriger |
| Résultat de contrôle | Source et géométrie utilisées, méthode, échantillonnage, seuils, unités, couverture, statistiques, validité |
| Masque de nettoyage | Identifiant source, zone, méthode, paramètres, points exclus ou règle reproductible, zones protégées |
| Journal | Version du plugin, paramètres, actions, avertissements, durée, décisions, créations/modifications et annulations |
| Révision de scan | Source précédente/nouvelle, type de changement, contrôles, transformation proposée, caches/résultats invalidés, décision de mise à jour |
| Coupe et projection | Plan de coupe, altitude/épaisseur, plan cible, direction de projection, points source, traces et géométrie dérivée |
| Session d’accrochage | Source et révision, zone de recherche, candidat mesuré, point projeté, précision/approximation annoncée, temps d’accès |

Les fichiers volumineux ne doivent pas être copiés sans nécessité et les métadonnées ne doivent pas perdre leur lien avec les sources. La lecture, l’affichage et l’export d’un format sont validés séparément.

## Exigences non fonctionnelles

- **Compatibilité :** une version exacte de Revit, incluant sa mise à jour, sera gelée ; aucun engagement multi-version sans tests.
- **Données privées :** traitement local recommandé ; aucune transmission de scans, plans ou adresses à un service externe sans décision explicite.
- **Intégrité :** fichiers source inchangés, absence de suppression silencieuse, possibilité d’annuler les modifications de modèle.
- **Performance :** échantillonnage ou traitement par zones autorisé si le rapport le précise ; aucune promesse « illimitée ».
- **Ergonomie :** interface en français, unités visibles, vocabulaire BIM, mode simple et paramètres avancés.
- **Robustesse :** liens manquants, source modifiée, mémoire insuffisante, géométrie dégénérée et calcul annulé doivent être gérés.
- **Traçabilité :** version, paramètres, hypothèses et décisions accessibles ; résultats périmés identifiés.
- **Maintenance :** documentation d’installation et de désinstallation, dépendances et licences inventoriées, versions figées pour les essais.
- **Worksharing :** recommandé hors périmètre ; si le modèle est collaboratif, le plugin doit avertir et bloquer les opérations non validées.

## Protocole de recette et indicateurs

### Jeux de données à constituer

Les données doivent être figées avant le test final et séparées des zones utilisées pour régler les paramètres. Une seule maison permet une étude de cas, pas une preuve de généralisation à tous les bâtiments.

| Jeu | Contenu | Ce qu’il vérifie |
|---|---|---|
| J01 | Géométrie synthétique, rotation/translation et unités connues | Transformations, conversions, orientation et erreurs numériques |
| J02 | Partie de maison avec points de contrôle et altitudes indépendantes | Calage et niveaux réels |
| J03 | Zone avec murs, mobilier, portes, fenêtres et occultations | Détection, hypothèses, correction humaine |
| J04 | Extérieur comprenant façade, terrain, végétation et détail fin | Nettoyage, segmentation et préservation des points utiles |
| J05 | Projet contenant déjà niveaux, coordonnées, types et contraintes | Non-écrasement, absence de doublons, annulation |
| J06 | Fichier manquant, transformation modifiée, source remplacée, manque de mémoire simulé | Erreurs et invalidation des résultats |
| J07 | Mur synthétique non plombé, plusieurs hauteurs de coupe et plans de projection | Dévers, projection et absence de modification de la source |
| J08 | Même nuage renommé, réexporté puis recalé avec transformation connue ; variante à résidus non uniformes | Actualisation des liens et refus d’un calage global inadéquat |
| J09 | Même zone en nuage léger et lourd, parcours de clics identique, zones masquées ou exclues | Latence à froid/à chaud, qualité du Snap et respect du périmètre |

### Cibles proposées, à approuver

| Indicateur | Cible proposée | Méthode et limite |
|---|---|---|
| Intégrité de la source | 0 modification de l’original | Comparaison d’empreinte avant/après chaque scénario |
| Conversion numérique | Erreur aller-retour ≤ 0,1 mm sur J01 | Mesure numérique, sans rapport avec l’incertitude du scanner |
| Conservation du calage | Variation des contrôles ≤ 1 mm après orientation/enregistrement | Compare un même calage avant/après, pas sa justesse topographique initiale |
| Altitude des niveaux validés | Écart ≤ 10 mm par rapport à la référence J02 | Convention de niveau identique ; seuil à adapter avec le professeur |
| Orientation assistée | Écart ≤ 0,1° sur l’axe de référence J01 | Deux points suffisamment espacés ; distance minimale à fixer dans le test |
| Doublons | 0 doublon lors d’une relance inchangée | Niveaux, objets et types gérés par le plugin |
| Validation humaine | 100 % des lots de modification approuvés | Journal et essais de refus/annulation |
| Couverture | 100 % des zones non mesurables identifiées comme telles dans le test | Aucun « conforme » par absence de points |
| Gain de temps | Cible de travail : ≥ 25 % sur la tâche retenue | Même données et livrable, calcul et corrections inclus ; comparaison non encore réalisée |
| Détection niveaux/murs | Précision et rappel publiés, seuil à fixer après référence manuelle | Définir unité d’évaluation et règle d’appariement avant le test |
| Nettoyage bonus | Taux de faux rejets et de parasites restants publiés | Zones annotées, détails fins séparés ; pas de seuil arbitraire engagé avant qualification |
| Réactivité | Annulation demandable pendant le calcul ; retour cible ≤ 5 s hors écriture atomique | Sur poste et jeu de taille convenus |
| Latence du Snap à chaud | Cible exploratoire : percentile 95 ≤ 250 ms | Sur poste et taille à définir ; premier accès et coût du cache publiés séparément, pas de garantie avant mesure |
| Qualité du Snap | Aucune dégradation au-delà de la tolérance de sélection approuvée | Même parcours avant/après optimisation ; taux d’échec et approximation déclarés |
| Actualisation des scans | 0 déplacement de modèle ou écrasement de coordonnées sans validation | Scénarios J08, contrôles et journal avant/après |
| Coupe/projection | Erreur ≤ 1 mm sur le cas synthétique J07 | Cible numérique proposée, non garantie de précision du relevé réel |
| Taille et durée maximale | Non contractualisées avant inventaire matériel/données | Mesurer nombre de points, mémoire maximale et temps par opération |

Une erreur moyenne seule ne suffit pas à valider la maquette. Le rapport doit fournir le nombre d’observations, les zones non couvertes et, selon la mesure, moyenne absolue, percentile 95 et maximum, avec les règles d’exclusion.

### Comparaison équitable avec FARO

Le test doit préciser la version exacte de FARO, la version Revit, le poste, les données, les fonctions activées et l’expérience de l’opérateur. Les possibilités de FARO doivent être utilisées correctement, notamment ses ajustements, son calcul de paramètres et ses fonctions d’analyse documentées ([manuel FARO](https://www.scribd.com/document/902610357/FARO-as-Built-For-Revit-User-Manual-en-2025-6)).

Le chronométrage doit distinguer préparation, détection/calcul, décisions, corrections et production du livrable. Il doit inclure trois tâches issues du retour utilisateur : rappeler/comparer des coupes d’un mur non plombé, effectuer le parcours de Snap et actualiser un scan avec vérification des coordonnées ; si une tâche n’est pas réalisable ou comparable dans l’un des outils, le résultat est « non comparable », pas un gain artificiellement chiffré.

## Dépendances, risques et limites

| Risque | Effet possible | Réponse exigée |
|---|---|---|
| Repère ou unité inconnus | Décalage massif ou fausse altitude | Diagnostic bloquant et saisie documentée |
| Sol/plafond confondus | Mauvais niveaux | Validation métier et cas ambigus conservés |
| Épaisseur cachée | Mur plausible mais faux | Hypothèse visible et choix utilisateur |
| Orthogonalisation excessive | Effacement des irrégularités réelles | Modes mesuré/régularisé distincts |
| Nuage incomplet | Baies ou surfaces inventées | Cartographie des zones non observées |
| Trop de fonctionnalités | Aucun parcours terminé | Arbitrage explicite, pas d’ajout automatique des bonus |
| Limite d’API ou format | Fonction impossible à intégrer comme prévu | Essai de faisabilité avant engagement technique |
| Modifications d’un type partagé | D’autres éléments modifiés | Réutilisation/duplication contrôlées |
| Nettoyage trop agressif | Détails et terrain supprimés | Masques réversibles et zones protégées |
| Données de comparaison insuffisantes | Conclusions non démontrées | Référence indépendante et limites publiées |
| Réutilisation d’une ancienne transformation sur un nouveau scan | Faux calage ou double transformation | Contrôles E22, pas de conservation aveugle |
| Accrochage rapide sur aperçu dégradé | Point choisi faux ou trop approximatif | Affinage, précision déclarée et mesure E23 |
| Projection assimilée à une mesure réelle | Mur redressé sans traçabilité | Séparer source, projection et objet de modélisation dans E24 |

Les dépendances importantes sont E01 → E02, E02 → E03/E04, E04/E05 → E07, E07 → E08 et E02/E05 → E14. E22 dépend de l’identification de source et du calage ; E23 dépend de la zone de recherche et de l’invalidation des caches ; E24 utilise coupes et plans de référence. E21 est transversal ; ces dépendances ne constituent pas encore un découpage en phases.

## Livrables attendus si le périmètre est approuvé

- **Plugin :** paquet installable et désinstallable sur la version Revit retenue.
- **Code :** dépôt versionné, instructions de compilation et dépendances documentées.
- **Données :** jeu d’exemple partageable, références et cas d’erreur ; données privées exclues de toute diffusion non autorisée.
- **Documentation :** guide d’installation, mode d’emploi, limites, dépannage et description des repères.
- **Tests :** protocoles, résultats bruts, rapports d’écart et comparaison chronométrée.
- **Mémoire :** problématique, état de l’art corrigé, arbitrages, méthodes, résultats et limites.
- **Enquête utilisateurs :** questionnaire, modalités de diffusion, synthèse non identifiante, limites de l’échantillon et tableau reliant les résultats aux exigences.
- **Registre de décisions :** version approuvée du présent document et justification des ajouts/exclusions.

## Questions bloquantes et arbitrages

| ID | Décision | Recommandation actuelle | Responsable | Effet |
|---|---|---|---|---|
| D01 | Périmètre métier | ACTÉ : architecture et structure ; MEP détaillé exclu | Thomas | Détail structure à préciser en D11 |
| D02 | Profondeur de modélisation | ACTÉ : calage, niveaux et murs ; baies/dalles/toitures traitées dans Revit | Thomas | Représentation des murs non plombés à préciser en D12 |
| D03 | Place de l’annexe | ACTÉ : préparer l’existant uniquement | Thomas | Pas de module de conception ou contrôle dédié de l’annexe |
| D04 | Place des bonus | Bonus souhaités : nettoyage et reconnaissance du bâtiment, conformément à la demande initiale | Thomas | Pas de promotion implicite en P0 |
| D05 | Versions exactes et accès FARO | Relever les versions installées et le manuel correspondant | Thomas | Bloque la recette comparative et la configuration |
| D06 | Référence des scans | Confirmer formats, unités, géoréférencement et référence verticale | Thomas / fournisseur du relevé | Bloque les promesses de calage automatique |
| D07 | Tolérances et convention du zéro | Valider les cibles proposées et sol fini/structure | Thomas + professeur | Bloque l’acceptation quantitative |
| D08 | Poste et volume | Décrire RAM, CPU/GPU, taille et densité des scans | Thomas | Bloque les engagements de performance |
| D09 | Date de remise et capacité | Fixer l’échéance et le temps réellement disponible | Thomas | Nécessaire au futur phasage |
| D10 | Langage et architecture | Proposition : plugin C# ; CPython externe ajouté seulement si utile ; pas de C++ au départ | Thomas + avis technique | Accord à recueillir ; .NET dépend de la version exacte de Revit |
| D11 | Fonctions de structure | Murs porteurs/références seulement ou aussi ajustement/création de poutres et poteaux ? | Thomas | Bloque la définition précise de E12 |
| D12 | Représentation des murs non plombés | Mur incliné fidèle, mur vertical de référence avec écarts, ou choix des deux ? | Thomas + professeur | Bloque le résultat attendu de E07/E24 |
| D13 | Sens de « ré-immatriculation » | Nouveau recalage des stations, réexport/indexation, ou simple changement de nom/chemin ? | Thomas | Bloque le scénario principal de E22 |
| D14 | Sondage de la promotion | ACTÉ : étude des difficultés des élèves ; plateforme, diffusion, questionnaire final et conservation à valider | Thomas + professeur | En parallèle du socle ; résultats avant engagement détaillé du périmètre |
| D15 | Démarrage et planning | ACTÉ : Thomas souhaite commencer et convenir du langage et des étapes ; proposition séparée préparée | Thomas | Planning daté après échéances, capacité et environnement ; pas d’accord implicite sur la proposition |

## Registre d’approbation

| Version | Statut | Décisions enregistrées |
|---|---|---|
| 0.9, 30/09/2026 | Proposition non approuvée | Besoins exprimés intégrés ; arbitrages recommandés, sans validation officielle |
| 0.10, 30/09/2026 à 16 h 20 | Arbitrages partiels actés | D01–D03 validés ; trois difficultés utilisateur traduites en E22–E24 ; détail structure et murs non plombés ouverts |
| 0.11, 30/09/2026 à 16 h 23 | Sondage ajouté à la démarche | D14 acté ; exigences méthodologiques S01–S05 et questionnaire préparés ; pas de collecte effectuée |
| 0.12, 30/09/2026 à 16 h 54 | Préparation du démarrage | D15 acté ; recommandation C# avec Python optionnel et étapes proposées, non encore approuvées ou datées |
| 1.0 | Non émise | Sera produite après décisions explicites et fermeture des points bloquants nécessaires |

Toute modification après approbation devra nommer l’exigence concernée, le bénéfice, les dépendances, les tests supplémentaires et ce qu’elle remplace ou reporte. La proposition d’étapes figure dans « TFE Scan-to-BIM : choix technique et planning de réalisation » ; aucune date ni phase n’est encore approuvée.
