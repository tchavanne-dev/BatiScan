"""E14 – Écarts points ↔ surfaces de référence et couverture.

Pour chaque surface rectangulaire (face de mur créée, plan de référence…),
calcule la distance signée des points proches, des statistiques et la
couverture. Une surface sans points est « non_observe », jamais « conforme ».
Le statut est une estimation géométrique, pas une certification (E14).
"""

from __future__ import annotations

import numpy as np

from . import geometrie as g

DEFAUTS = {
    "surfaces": [],
    "tolerance_m": 0.01,
    "distance_max_m": 0.10,
    "cellule_m": 0.10,
    "couverture_min": 0.30,
}


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str]) -> tuple[list[dict], int]:
    resultats = []
    for k, surf in enumerate(params["surfaces"]):
        o = pts.vers_local(surf["origine"])
        u = np.asarray(surf["u"], dtype=float)
        v = np.asarray(surf["v"], dtype=float)
        u /= np.linalg.norm(u)
        v /= np.linalg.norm(v)
        nrm = np.cross(u, v)
        lu, lv = float(surf["longueur_u"]), float(surf["longueur_v"])
        q = pts.local - o
        a, b, d = q @ u, q @ v, q @ nrm
        sel = (a >= 0) & (a <= lu) & (b >= 0) & (b <= lv) & (np.abs(d) <= params["distance_max_m"])
        ds = d[sel]
        couv = g.couverture_grille(np.c_[a[sel], b[sel]], np.zeros(2), np.array([lu, lv]), params["cellule_m"])
        res = {
            "id": surf.get("id", f"E{k + 1:03d}"),
            "type": "ecart",
            "reference": surf.get("reference", ""),
            "sens": "positif du côté de la normale u×v",
            "nombre_points": int(sel.sum()),
            "couverture": couv,
            "tolerance_m": params["tolerance_m"],
        }
        if len(ds) == 0:
            res.update({"statut": "non_observe"})
        else:
            ab = np.abs(ds)
            hors = float((ab > params["tolerance_m"]).mean())
            res.update(
                {
                    "moyenne_signee_m": float(ds.mean()),
                    "moyenne_absolue_m": float(ab.mean()),
                    "p95_absolu_m": float(np.percentile(ab, 95)),
                    "max_absolu_m": float(ab.max()),
                    "part_hors_tolerance": hors,
                    "statut": (
                        "couverture_insuffisante"
                        if couv["ratio"] < params["couverture_min"]
                        else ("dans_tolerance" if np.percentile(ab, 95) <= params["tolerance_m"] else "hors_tolerance")
                    ),
                }
            )
        resultats.append(res)
    return resultats, len(pts.local)
