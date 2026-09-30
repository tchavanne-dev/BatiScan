# BatiScan

Plugin Revit 2027 – Inventaire et analyse des nuages de points liés.

## Description

**BatiScan** est un plugin Revit 2027 en lecture seule qui :

- Parcourt le projet actif et liste tous les **nuages de points liés** (`PointCloudInstance`).
- Affiche pour chaque nuage : nom, chemin du fichier source, unités du projet, transformation complète (origine, axes, échelle) et **statut de calage** (référence non confirmée vs repositionné).
- **N'écrit jamais** dans le projet et ne modifie pas les coordonnées partagées existantes.

## Prérequis

| Composant | Version |
|-----------|---------|
| Revit | 2027 |
| .NET | 10 (net10.0-windows) |
| SDK | Autodesk Revit API 2027 |

## Structure du projet

```
BatiScan/
├── BatiScan.csproj          ← Projet .NET 10
├── Application.cs           ← IExternalApplication (ruban)
├── CommandeNuageDePoints.cs ← IExternalCommand (inventaire nuages)
├── BatiScan.addin           ← Manifeste Revit
├── .gitignore
└── README.md
```

## Compilation

```powershell
# Restaurer les packages NuGet et compiler en Release
dotnet build BatiScan.csproj -c Release
```

> **Note** : Si les packages NuGet Autodesk ne sont pas encore disponibles pour Revit 2027,
> décommentez les références locales dans `BatiScan.csproj` et ajustez le chemin
> vers `C:\Program Files\Autodesk\Revit 2027\`.

## Déploiement

```powershell
# Copier la DLL et le manifeste dans le dossier Addins utilisateur
$dest = "$env:APPDATA\Autodesk\Revit\Addins\2027"
New-Item -ItemType Directory -Force -Path $dest
Copy-Item "bin\Release\net10.0-windows\BatiScan.dll" $dest
Copy-Item "BatiScan.addin" $dest
```

Relancez Revit 2027. L'onglet **BatiScan** apparaît dans le ruban.

## Utilisation

1. Ouvrez un projet Revit contenant des nuages de points (RCP, PCG, E57…).
2. Cliquez sur **BatiScan → Inventaire des nuages**.
3. Un `TaskDialog` affiche le rapport complet.
4. Le bouton **Copier dans le presse-papiers** permet d'exporter le rapport.

## Règle de non-modification des coordonnées partagées

> ⚠️ Cette commande est déclarée `[Transaction(TransactionMode.ReadOnly)]`.
> Aucune transaction d'écriture ne peut être ouverte. Les coordonnées partagées
> du projet ne sont jamais altérées.

## Licence

MIT – voir `LICENSE` (à créer).
