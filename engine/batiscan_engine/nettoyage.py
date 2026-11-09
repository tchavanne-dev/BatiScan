"""E17 (bonus) – Masque réversible de points suspects.

Le fichier source n'est jamais modifié : le résultat est un fichier masque
séparé (uint8, 1 = conservé, 0 = rejeté), aligné sur l'ordre des points
REÇUS. Les points hors zone et les zones protégées (E11, terrain à conserver)
sont toujours conservés. Désactiver le masque restaure les points initiaux.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np

from . import geometrie as g
from .contrat import ErreurContrat

DEFAUTS = {
    "chemin_masque": "",
    "voisins": 20,
    "ecart_type": 2.0,
    "zones_protegees": [],
}


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str], dossier: Path) -> tuple[list[dict], int]:
    if not params["chemin_masque"]:
        raise ErreurContrat("PARAMETRE_MANQUANT", "parametres.chemin_masque est requis pour le nettoyage.")
    chemin = Path(params["chemin_masque"])
    if not chemin.is_absolute():
        chemin = dossier / chemin
    masque_recus = np.ones(pts.recus, dtype=np.uint8)
    if len(pts.local) >= params["voisins"] + 1:
        _, gardes = g.vers_open3d(pts.local).remove_statistical_outlier(params["voisins"], params["ecart_type"])
        rejet_zone = np.ones(len(pts.local), dtype=bool)
        rejet_zone[np.asarray(gardes, dtype=int)] = False
        for z in params["zones_protegees"]:
            mini = np.asarray(z["min"], dtype=float) - pts.origine
            maxi = np.asarray(z["max"], dtype=float) - pts.origine
            rejet_zone &= ~np.all((pts.local >= mini) & (pts.local <= maxi), axis=1)
        masque_recus[pts.indices_zone[rejet_zone]] = 0
    else:
        avertissements.append("Trop peu de points : aucun rejet proposé.")
    masque_recus.tofile(chemin)
    rejetes = int((masque_recus == 0).sum())
    return [
        {
            "id": "K001",
            "type": "masque",
            "chemin_masque": str(chemin),
            "format": "uint8 (1 = conservé), ordre des points reçus",
            "rejetes": rejetes,
            "conserves": int(pts.recus - rejetes),
            "methode": "remove_statistical_outlier",
            "reversible": True,
        }
    ], len(pts.local)
