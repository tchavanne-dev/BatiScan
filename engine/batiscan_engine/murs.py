"""E07 / E24 – Propositions de murs droits par zone, avec mesure du dévers.

Principe :
1. points de surfaces quasi verticales (normales quasi horizontales) ;
2. RANSAC de plans successifs (graine fixée), découpés par DBSCAN ;
3. chaque plan découpé = une FACE observée ; son dévers est mesuré par
   régression du décalage horizontal des traces à plusieurs altitudes ;
4. appariement de faces parallèles proches = mur à épaisseur MESURÉE ;
   face seule = épaisseur INCONNUE (hypothèse demandée côté C#, E07).

Le moteur ne redresse rien : il retourne les mesures, le C# propose ensuite
« mur incliné fidèle » ou « mur vertical + rapport de dévers » (E24).
"""

from __future__ import annotations

import numpy as np

from . import geometrie as g

DEFAUTS = {
    "voxel_m": 0.02,
    "rayon_normales_m": 0.10,
    "max_voisins": 30,
    "inclinaison_max_deg": 15.0,
    "seuil_ransac_m": 0.015,
    "iterations_ransac": 800,
    "min_points": 300,
    "max_plans": 40,
    "dbscan_eps_m": 0.10,
    "dbscan_min_points": 10,
    "hauteur_min_m": 0.5,
    "longueur_min_m": 0.3,
    "epaisseur_min_m": 0.05,
    "epaisseur_max_m": 0.60,
    "angle_parallele_deg": 5.0,
    "recouvrement_min": 0.5,
    "pas_traces_m": 0.25,
    "min_points_trace": 20,
    "tolerance_plomb_deg": 0.2,
    "hauteur_min_devers_m": 1.0,
    "cellule_m": 0.10,
}


def _face(pts_face: np.ndarray, normale: np.ndarray, params: dict) -> dict | None:
    nh = normale[:2].copy()
    norme = np.linalg.norm(nh)
    if norme < 1e-9:
        return None
    nh /= norme
    u = np.array([-nh[1], nh[0]])
    s = pts_face[:, :2] @ u
    t = pts_face[:, :2] @ nh
    z = pts_face[:, 2]
    s0, s1 = np.percentile(s, [0.1, 99.9])
    z0, z1 = np.percentile(z, [0.1, 99.9])
    if s1 - s0 < params["longueur_min_m"] or z1 - z0 < params["hauteur_min_m"]:
        return None

    # Traces à plusieurs altitudes : décalage médian le long de la normale horizontale.
    pas = params["pas_traces_m"]
    traces = []
    for zc in np.arange(z0 + pas / 2, z1, pas):
        sel = np.abs(z - zc) <= pas / 2
        if sel.sum() >= params["min_points_trace"]:
            traces.append((float(zc), float(np.median(t[sel])), int(sel.sum())))

    devers = {"methode": "regression_traces", "traces": [], "incertain": True, "raison": "moins de 3 traces exploitables"}
    if len(traces) >= 3:
        zt = np.array([a for a, _, _ in traces])
        tt = np.array([b for _, b, _ in traces])
        w = np.array([c for _, _, c in traces], dtype=float)
        A = np.c_[np.ones_like(zt), zt]
        W = np.sqrt(w)[:, None]
        coef, *_ = np.linalg.lstsq(A * W, tt * W[:, 0], rcond=None)
        a, b = float(coef[0]), float(coef[1])
        residus = tt - (a + b * zt)
        rms_traces = float(np.sqrt(np.mean(residus**2)))
        hauteur = float(z1 - z0)
        incertain = hauteur < params["hauteur_min_devers_m"] or rms_traces > 2 * params["seuil_ransac_m"]
        raison = ""
        if hauteur < params["hauteur_min_devers_m"]:
            raison = f"hauteur observée {hauteur:.2f} m < {params['hauteur_min_devers_m']} m"
        elif rms_traces > 2 * params["seuil_ransac_m"]:
            raison = f"traces irrégulières (rms {rms_traces * 1000:.1f} mm) : inclinaison non uniforme"
        nz_plan = normale[2] / max(1e-12, np.linalg.norm(normale))
        devers = {
            "methode": "regression_traces",
            "pente_m_par_m": b,
            "angle_deg": float(np.degrees(np.arctan(b))),
            "angle_plan_deg": float(np.degrees(np.arcsin(np.clip(abs(nz_plan), 0, 1)))),
            "rms_traces_m": rms_traces,
            "decalage_base_m": a,
            "traces": [{"z_local": zc, "decalage_m": tc, "nombre_points": c} for zc, tc, c in traces],
            "incertain": bool(incertain),
            "raison": raison,
        }
    grille = g.couverture_grille(np.c_[s, z], np.array([s0, z0]), np.array([s1, z1]), params["cellule_m"])
    return {
        "nh": nh,
        "u": u,
        "s0": float(s0),
        "s1": float(s1),
        "z0": float(z0),
        "z1": float(z1),
        "t_med": float(np.median(t)),
        "n": int(len(pts_face)),
        "devers": devers,
        "couverture": grille,
    }


def _decalage_a(face: dict, z: float) -> float:
    d = face["devers"]
    if "pente_m_par_m" in d:
        return d["decalage_base_m"] + d["pente_m_par_m"] * z
    return face["t_med"]


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str]) -> tuple[list[dict], int]:
    echant = g.sous_echantillonner(pts.local, params["voxel_m"])
    if len(echant) < params["min_points"]:
        avertissements.append("Trop peu de points dans la zone pour proposer des murs.")
        return [], len(echant)
    n = g.normales(echant, params["rayon_normales_m"], params["max_voisins"])
    sin_max = np.sin(np.radians(params["inclinaison_max_deg"] + 5.0))
    restant = echant[np.abs(n[:, 2]) <= sin_max]
    sin_plan = np.sin(np.radians(params["inclinaison_max_deg"]))
    contrainte = lambda v: abs(v[2]) <= sin_plan  # noqa: E731

    faces = []
    for _ in range(params["max_plans"]):
        if len(restant) < params["min_points"]:
            break
        plan = g.ransac_plan(restant, params["seuil_ransac_m"], params["iterations_ransac"], rng, contrainte=contrainte)
        if plan is None or len(plan.inliers) < params["min_points"]:
            break
        inl = restant[plan.inliers]
        etiquettes = g.dbscan(inl, params["dbscan_eps_m"], params["dbscan_min_points"])
        for e in sorted(set(etiquettes) - {-1}):
            morceau = inl[etiquettes == e]
            if len(morceau) < params["min_points"]:
                continue
            f = _face(morceau, plan.normale, params)
            if f is not None:
                f["rms"] = plan.rms_m
                faces.append(f)
        restant = np.delete(restant, plan.inliers, axis=0)

    # ── Appariement des faces parallèles (épaisseur mesurée) ──────────────────
    cos_par = np.cos(np.radians(params["angle_parallele_deg"]))
    paires = []
    for i in range(len(faces)):
        for j in range(i + 1, len(faces)):
            fi, fj = faces[i], faces[j]
            if abs(fi["nh"] @ fj["nh"]) < cos_par:
                continue
            zm = (max(fi["z0"], fj["z0"]) + min(fi["z1"], fj["z1"])) / 2
            if min(fi["z1"], fj["z1"]) <= max(fi["z0"], fj["z0"]):
                continue
            signe = 1.0 if fi["nh"] @ fj["nh"] > 0 else -1.0
            ti = _decalage_a(fi, zm)
            tj = signe * _decalage_a(fj, zm)
            ep = abs(tj - ti)
            if not (params["epaisseur_min_m"] <= ep <= params["epaisseur_max_m"]):
                continue
            # recouvrement le long de l'axe de fi
            sj = np.array([fj["s0"], fj["s1"]]) * (fj["u"] @ fi["u"])
            a0, a1 = max(fi["s0"], sj.min()), min(fi["s1"], sj.max())
            lmin = min(fi["s1"] - fi["s0"], fj["s1"] - fj["s0"])
            if a1 - a0 < params["recouvrement_min"] * lmin:
                continue
            paires.append((ep, i, j, signe))
    paires.sort()
    utilisees: set[int] = set()
    murs = []

    def ajouter(fi: dict, fj: dict | None, signe: float, ep: float | None):
        nh, u = fi["nh"], fi["u"]
        z_bas = min(fi["z0"], fj["z0"]) if fj else fi["z0"]
        z_haut = max(fi["z1"], fj["z1"]) if fj else fi["z1"]
        if fj:
            sj = np.array([fj["s0"], fj["s1"]]) * (fj["u"] @ u)
            s0, s1 = min(fi["s0"], sj.min()), max(fi["s1"], sj.max())
            t_bas = (_decalage_a(fi, z_bas) + signe * _decalage_a(fj, z_bas)) / 2
            pentes = [f["devers"].get("pente_m_par_m") for f in (fi, fj)]
            if None not in pentes:
                pente = (pentes[0] + signe * pentes[1]) / 2
                incertain = fi["devers"]["incertain"] or fj["devers"]["incertain"]
                ecart_faces = abs(pentes[0] - signe * pentes[1])
            else:
                pente, incertain, ecart_faces = None, True, None
        else:
            s0, s1 = fi["s0"], fi["s1"]
            t_bas = _decalage_a(fi, z_bas)
            pente = fi["devers"].get("pente_m_par_m")
            incertain = fi["devers"]["incertain"]
            ecart_faces = None
        hauteur = z_haut - z_bas
        debut = u * s0 + nh * t_bas
        fin = u * s1 + nh * t_bas
        devers = {"incertain": True, "raison": "dévers non mesurable"}
        if pente is not None:
            angle = float(np.degrees(np.arctan(pente)))
            direction = nh * np.sign(pente) if pente != 0 else nh
            devers = {
                "angle_deg": angle,
                "pente_m_par_m": float(pente),
                "deplacement_sommet_m": float(pente * hauteur),
                "direction_inclinaison": [float(direction[0]), float(direction[1])],
                "incertain": bool(incertain),
                "raison": fi["devers"].get("raison", "") if incertain else "",
                "traces_face_1": [
                    {"z": tr["z_local"] + float(pts.origine[2]), "decalage_m": tr["decalage_m"] - fi["devers"]["decalage_base_m"]}
                    for tr in fi["devers"]["traces"]
                ],
            }
            if ecart_faces is not None:
                devers["ecart_pentes_faces_m_par_m"] = float(ecart_faces)
                if ecart_faces * hauteur > 2 * params["seuil_ransac_m"]:
                    devers["incertain"] = True
                    devers["raison"] = "faces non parallèles (épaisseur variable) : géométrie hors périmètre (E20)"
        if devers["incertain"]:
            plombage = "incertain"
        elif abs(devers["angle_deg"]) <= params["tolerance_plomb_deg"]:
            plombage = "plombe"
        else:
            plombage = "non_plombe"
        couv = fi["couverture"]["ratio"] if not fj else max(fi["couverture"]["ratio"], fj["couverture"]["ratio"])
        murs.append(
            {
                "id": f"M{len(murs) + 1:03d}",
                "type": "mur",
                "debut": pts.vers_repere(debut),
                "fin": pts.vers_repere(fin),
                "z_bas": float(z_bas + pts.origine[2]),
                "z_haut": float(z_haut + pts.origine[2]),
                "longueur_m": float(s1 - s0),
                "epaisseur_m": None if ep is None else float(ep),
                "epaisseur_mesuree": ep is not None,
                "normale": [float(nh[0]), float(nh[1])],
                "cote_matiere": "entre_faces" if fj else "inconnu",
                "nombre_faces": 2 if fj else 1,
                "nombre_points": int(fi["n"] + (fj["n"] if fj else 0)),
                "rms_m": float(max(fi["rms"], fj["rms"]) if fj else fi["rms"]),
                "devers": devers,
                "plombage": plombage,
                "couverture": {"methode": "grille_face", "taille_cellule_m": params["cellule_m"], "ratio": float(couv)},
                "justification": (
                    f"2 faces parallèles à {ep * 1000:.0f} mm" if fj else "1 seule face observée : épaisseur à fournir (hypothèse)"
                ),
            }
        )

    for ep, i, j, signe in paires:
        if i in utilisees or j in utilisees:
            continue
        utilisees.update((i, j))
        ajouter(faces[i], faces[j], signe, ep)
    for i, f in enumerate(faces):
        if i not in utilisees:
            ajouter(f, None, 1.0, None)
    return murs, len(echant)
