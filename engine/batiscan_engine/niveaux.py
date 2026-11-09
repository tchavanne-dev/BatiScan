"""E04 – Détection des surfaces horizontales et proposition de niveaux.

Le moteur PROPOSE des altitudes de surfaces horizontales observées avec leur
type supposé et une justification. Il ne choisit ni le zéro architectural ni
le rez-de-chaussée : le point le plus bas n'est pas présumé être le RDC.
"""

from __future__ import annotations

import numpy as np

from . import geometrie as g

DEFAUTS = {
    "voxel_m": 0.02,
    "rayon_normales_m": 0.10,
    "max_voisins": 30,
    "tolerance_horizontale_deg": 10.0,
    "pas_histogramme_m": 0.01,
    "demi_fenetre_m": 0.04,
    "min_points": 400,
    "seuil_ransac_m": 0.01,
    "iterations_ransac": 500,
    "ecart_min_niveaux_m": 0.10,
    "surface_min_m2": 1.5,
    "inclinaison_pente_deg": 2.0,
    "hauteur_etage_min_m": 2.0,
    "hauteur_etage_max_m": 4.5,
    "cellule_m": 0.10,
}


def _pics(z: np.ndarray, pas: float, min_points: int, ecart_min: float, demi_fenetre: float) -> list[float]:
    """Altitudes des pics d'histogramme (comptage sur fenêtre glissante)."""
    if len(z) == 0:
        return []
    bords = np.arange(z.min() - pas, z.max() + 2 * pas, pas)
    hist, _ = np.histogram(z, bords)
    k = max(1, int(round(demi_fenetre / pas)))
    cumul = np.convolve(np.pad(hist, k), np.ones(2 * k + 1, dtype=int), mode="valid")
    centres = (bords[:-1] + bords[1:]) / 2
    ordre = np.argsort(-cumul, kind="stable")
    retenus: list[float] = []
    for i in ordre:
        if cumul[i] < min_points:
            break
        if all(abs(centres[i] - r) >= ecart_min for r in retenus):
            retenus.append(float(centres[i]))
    return sorted(retenus)


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str]) -> tuple[list[dict], int]:
    local = pts.local
    echant = g.sous_echantillonner(local, params["voxel_m"])
    if len(echant) < params["min_points"]:
        avertissements.append("Trop peu de points dans la zone pour proposer des niveaux.")
        return [], len(echant)
    n = g.normales(echant, params["rayon_normales_m"], params["max_voisins"])
    cos_tol = np.cos(np.radians(params["tolerance_horizontale_deg"]))
    horiz = echant[np.abs(n[:, 2]) >= cos_tol]

    altitudes = _pics(
        horiz[:, 2], params["pas_histogramme_m"], params["min_points"], params["ecart_min_niveaux_m"], params["demi_fenetre_m"]
    )

    candidats = []
    for z0 in altitudes:
        bande = horiz[np.abs(horiz[:, 2] - z0) <= params["demi_fenetre_m"]]
        if len(bande) < params["min_points"]:
            continue
        plan = g.ransac_plan(
            bande,
            params["seuil_ransac_m"],
            params["iterations_ransac"],
            rng,
            contrainte=lambda v: abs(v[2]) >= cos_tol,
        )
        if plan is None or len(plan.inliers) < params["min_points"]:
            continue
        inl = bande[plan.inliers]
        nz = plan.normale if plan.normale[2] >= 0 else -plan.normale
        inclinaison = float(np.degrees(np.arccos(np.clip(nz[2], -1, 1))))
        z_med = float(np.median(inl[:, 2]))
        emprise = g.cellules_occupees(inl[:, :2], params["cellule_m"])
        surface = len(emprise) * params["cellule_m"] ** 2
        candidats.append(
            {
                "z": z_med,
                "inclinaison": inclinaison,
                "rms": plan.rms_m,
                "n": len(inl),
                "surface": surface,
                "emprise": emprise,
                "xy_min": inl[:, :2].min(axis=0),
                "xy_max": inl[:, :2].max(axis=0),
            }
        )

    propositions = []
    for i, c in enumerate(candidats):
        dessus, dessous = [], []
        for j, o in enumerate(candidats):
            if i == j:
                continue
            dz = o["z"] - c["z"]
            if params["hauteur_etage_min_m"] <= abs(dz) <= params["hauteur_etage_max_m"]:
                commun = len(c["emprise"] & o["emprise"]) / max(1, min(len(c["emprise"]), len(o["emprise"])))
                if commun >= 0.3:
                    (dessus if dz > 0 else dessous).append((o["z"], commun))
        dessus.sort(key=lambda t: t[0])
        dessous.sort(key=lambda t: -t[0])
        raisons = []
        if c["inclinaison"] > params["inclinaison_pente_deg"]:
            type_suppose = "pente"
            raisons.append(f"inclinaison {c['inclinaison']:.1f}° > {params['inclinaison_pente_deg']}°")
        elif c["surface"] < params["surface_min_m2"]:
            type_suppose = "mobilier_ou_plan_de_travail"
            raisons.append(f"surface observée {c['surface']:.2f} m² < {params['surface_min_m2']} m²")
        elif dessus and not dessous:
            type_suppose = "sol"
            raisons.append(f"surface recouverte à +{dessus[0][0] - c['z']:.2f} m (hauteur d'étage plausible)")
        elif dessous and not dessus:
            type_suppose = "plafond"
            raisons.append(f"surface recouvrant un sol à −{c['z'] - dessous[0][0]:.2f} m")
        elif dessus and dessous:
            type_suppose = "indetermine"
            raisons.append("surfaces superposées au-dessus et au-dessous : sol d'étage ou sous-face, à confirmer")
        else:
            type_suppose = "indetermine"
            raisons.append("aucune surface superposée à une hauteur d'étage plausible")
        propositions.append(
            {
                "id": f"N{i + 1:03d}",
                "type": "niveau",
                "altitude_m": float(c["z"] + pts.origine[2]),
                "inclinaison_deg": c["inclinaison"],
                "rms_m": c["rms"],
                "nombre_points": int(c["n"]),
                "surface_observee_m2": float(c["surface"]),
                "emprise_min": pts.vers_repere(c["xy_min"]),
                "emprise_max": pts.vers_repere(c["xy_max"]),
                "type_suppose": type_suppose,
                "justification": "; ".join(raisons),
            }
        )
    return propositions, len(echant)
