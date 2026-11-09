"""E05 / E24 – Traces de coupe à plusieurs altitudes.

Chaque altitude produit une trace indépendante (segments 2D) : les traces
précédentes ne sont jamais écrasées, ce qui permet de comparer un mur non
plombé entre deux tranches. La projection sur un plan cible est faite côté C#
(géométrie simple, testable sans Revit) ; le nuage source n'est pas modifié.
"""

from __future__ import annotations

import numpy as np

from . import geometrie as g

DEFAUTS = {
    "altitudes_m": [],
    "epaisseur_m": 0.02,
    "voxel_m": 0.01,
    "seuil_ligne_m": 0.01,
    "iterations_ransac": 400,
    "min_points_segment": 25,
    "max_segments": 60,
    "ecart_max_m": 0.15,
    "longueur_min_m": 0.20,
}


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str]) -> tuple[list[dict], int]:
    altitudes = list(params["altitudes_m"])
    if not altitudes:
        avertissements.append("Aucune altitude de coupe fournie (parametres.altitudes_m).")
        return [], 0
    traces = []
    total = 0
    for k, z_abs in enumerate(altitudes):
        z = z_abs - pts.origine[2]
        bande = pts.local[np.abs(pts.local[:, 2] - z) <= params["epaisseur_m"] / 2]
        bande = g.sous_echantillonner(bande, params["voxel_m"])
        total += len(bande)
        p2 = bande[:, :2]
        segments = []
        restant = p2
        while len(restant) >= params["min_points_segment"] and len(segments) < params["max_segments"]:
            res = g.ransac_droite_2d(restant, params["seuil_ligne_m"], params["iterations_ransac"], rng)
            if res is None:
                break
            u, centre, inl = res
            if len(inl) < params["min_points_segment"]:
                break
            q = restant[inl]
            s = (q - centre) @ u
            nrm = np.array([-u[1], u[0]])
            for morceau in g.decouper_par_trous(s, params["ecart_max_m"]):
                if len(morceau) < params["min_points_segment"]:
                    continue
                sm = s[morceau]
                s0, s1 = float(sm.min()), float(sm.max())
                if s1 - s0 < params["longueur_min_m"]:
                    continue
                d = (q[morceau] - centre) @ nrm
                segments.append(
                    {
                        "debut": pts.vers_repere(centre + u * s0),
                        "fin": pts.vers_repere(centre + u * s1),
                        "rms_m": float(np.sqrt(np.mean(d**2))),
                        "nombre_points": int(len(morceau)),
                    }
                )
            restant = np.delete(restant, inl, axis=0)
        if not segments:
            avertissements.append(f"Altitude {z_abs:.3f} m : aucune trace (zone non observée à cette hauteur).")
        traces.append(
            {
                "id": f"T{k + 1:03d}",
                "type": "trace",
                "z": float(z_abs),
                "epaisseur_m": params["epaisseur_m"],
                "nombre_points": int(len(bande)),
                "segments": segments,
            }
        )
    return traces, total
