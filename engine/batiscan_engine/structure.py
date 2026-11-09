"""E12 – Propositions de poteaux et de poutres.

Poteaux : amas verticaux compacts présents sur plusieurs tranches d'altitude.
Poutres : sous-faces horizontales allongées situées sous un plafond.

Seule la géométrie OBSERVÉE est retournée (encombrement, axe, retombée).
Aucune section, aucun matériau ni aucune qualité ne sont affirmés (E12) ;
le type Revit est choisi par l'utilisateur. Aucun calcul de structure.
"""

from __future__ import annotations

import numpy as np

from . import geometrie as g

DEFAUTS = {
    "voxel_m": 0.02,
    "rayon_normales_m": 0.10,
    "max_voisins": 30,
    "tolerance_horizontale_deg": 10.0,
    # Poteaux
    "nombre_tranches": 5,
    "epaisseur_tranche_m": 0.10,
    "marge_verticale_m": 0.30,
    "dbscan_eps_m": 0.06,
    "dbscan_min_points": 8,
    "section_min_m": 0.08,
    "section_max_m": 0.80,
    "presence_min_tranches": 0.8,
    "tolerance_alignement_m": 0.05,
    # Poutres
    "retombee_min_m": 0.08,
    "retombee_max_m": 1.20,
    "largeur_min_m": 0.08,
    "largeur_max_m": 0.80,
    "longueur_min_m": 1.0,
    "elancement_min": 3.0,
    "dbscan_poutre_eps_m": 0.08,
    "min_points_poutre": 60,
}


def _poteaux(local: np.ndarray, params: dict, origine: np.ndarray, avert: list[str]) -> list[dict]:
    z0, z1 = local[:, 2].min(), local[:, 2].max()
    marge = params["marge_verticale_m"]
    if z1 - z0 < 2 * marge + params["epaisseur_tranche_m"]:
        avert.append("Zone trop basse pour détecter des poteaux.")
        return []
    altitudes = np.linspace(z0 + marge, z1 - marge, params["nombre_tranches"])
    par_tranche = []
    for zc in altitudes:
        bande = local[np.abs(local[:, 2] - zc) <= params["epaisseur_tranche_m"] / 2][:, :2]
        lab = g.dbscan(np.c_[bande, np.zeros(len(bande))], params["dbscan_eps_m"], params["dbscan_min_points"])
        amas = []
        for e in sorted(set(lab) - {-1}):
            q = bande[lab == e]
            c, u, lg, la = g.boite_orientee_2d(q)
            if params["section_min_m"] <= la and lg <= params["section_max_m"]:
                amas.append((c, u, lg, la, len(q)))
        par_tranche.append(amas)
    # Chaînage vertical : un poteau est présent dans la plupart des tranches.
    poteaux = []
    if not par_tranche:
        return poteaux
    for c, u, lg, la, n in par_tranche[len(par_tranche) // 2]:
        presences, sections = 0, []
        for amas in par_tranche:
            proches = [a for a in amas if np.linalg.norm(a[0] - c) <= max(params["tolerance_alignement_m"], la / 2)]
            if proches:
                presences += 1
                sections.append(proches[0])
        if presences / len(par_tranche) < params["presence_min_tranches"]:
            continue
        lgs = np.array([s[2] for s in sections])
        las = np.array([s[3] for s in sections])
        centres = np.array([s[0] for s in sections])
        rotation = float(np.degrees(np.arctan2(u[1], u[0])) % 180.0)
        # Seules 1 ou 2 faces visibles ⇒ l'encombrement sous-estime la section.
        partielle = bool(np.std(lgs) > 0.02 or np.std(las) > 0.02)
        poteaux.append(
            {
                "type": "poteau",
                "centre": [float(v) for v in (np.median(centres, axis=0) + origine[:2])],
                "z_bas": float(z0 + origine[2]),
                "z_haut": float(z1 + origine[2]),
                "encombrement_observe": {"a_m": float(np.median(lgs)), "b_m": float(np.median(las))},
                "rotation_deg": rotation,
                "presence_tranches": f"{presences}/{len(par_tranche)}",
                "section_observee": "partielle" if partielle else "contour_complet_probable",
                "justification": "amas vertical compact continu sur la hauteur de la zone ; section non affirmée",
            }
        )
    return poteaux


def _poutres(local: np.ndarray, params: dict, origine: np.ndarray, avert: list[str]) -> list[dict]:
    echant = g.sous_echantillonner(local, params["voxel_m"])
    if len(echant) < 100:
        return []
    n = g.normales(echant, params["rayon_normales_m"], params["max_voisins"])
    horiz = echant[np.abs(n[:, 2]) >= np.cos(np.radians(params["tolerance_horizontale_deg"]))]
    if len(horiz) < 100:
        return []
    # Plafond de référence : surface horizontale haute la plus peuplée.
    z = horiz[:, 2]
    hist, bords = np.histogram(z, bins=max(10, int((z.max() - z.min()) / 0.02) + 1))
    haut = np.nonzero(bords[:-1] >= z.min() + 0.6 * (z.max() - z.min()))[0]
    if len(haut) == 0:
        return []
    k = haut[np.argmax(hist[haut])]
    z_plafond = float((bords[k] + bords[k + 1]) / 2)
    sous = horiz[(z_plafond - horiz[:, 2] >= params["retombee_min_m"]) & (z_plafond - horiz[:, 2] <= params["retombee_max_m"])]
    if len(sous) < params["min_points_poutre"]:
        return []
    lab = g.dbscan(sous, params["dbscan_poutre_eps_m"], params["dbscan_min_points"])
    poutres = []
    for e in sorted(set(lab) - {-1}):
        q = sous[lab == e]
        if len(q) < params["min_points_poutre"]:
            continue
        c, u, lg, la = g.boite_orientee_2d(q[:, :2])
        if lg < params["longueur_min_m"] or not (params["largeur_min_m"] <= la <= params["largeur_max_m"]):
            continue
        if lg / max(la, 1e-6) < params["elancement_min"]:
            continue
        z_sf = float(np.median(q[:, 2]))
        # Largeur : les arêtes de sous-face ont des normales mixtes (exclues ci-dessus) ;
        # on reprend tous les points (joues comprises) entre sous-face et plafond.
        v = np.array([-u[1], u[0]])
        marge = 0.06
        r = echant[(echant[:, 2] > z_sf - 0.01) & (echant[:, 2] < z_plafond - 0.02)]
        rs, rt = (r[:, :2] - c) @ u, (r[:, :2] - c) @ v
        proches = (np.abs(rs) <= lg / 2) & (np.abs(rt) <= la / 2 + marge)
        if proches.sum() > 20:
            t0, t1 = np.percentile(rt[proches], [0.5, 99.5])
            c = c + v * (t0 + t1) / 2
            la = float(t1 - t0)
        a, b = c - u * lg / 2, c + u * lg / 2
        poutres.append(
            {
                "type": "poutre",
                "debut": [float(a[0] + origine[0]), float(a[1] + origine[1]), float(z_plafond + origine[2])],
                "fin": [float(b[0] + origine[0]), float(b[1] + origine[1]), float(z_plafond + origine[2])],
                "z_sous_face": float(z_sf + origine[2]),
                "z_plafond_reference": float(z_plafond + origine[2]),
                "retombee_observee_m": float(z_plafond - z_sf),
                "largeur_observee_m": float(la),
                "longueur_observee_m": float(lg),
                "hypothese": "dessus de poutre supposé au nu du plafond de référence",
                "justification": "sous-face horizontale allongée sous le plafond ; section non affirmée",
            }
        )
    return poutres


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str]) -> tuple[list[dict], int]:
    if len(pts.local) < 100:
        avertissements.append("Trop peu de points pour proposer des éléments de structure.")
        return [], len(pts.local)
    elements = _poteaux(pts.local, params, pts.origine, avertissements) + _poutres(pts.local, params, pts.origine, avertissements)
    for i, el in enumerate(elements):
        el["id"] = f"S{i + 1:03d}"
    return elements, len(pts.local)
