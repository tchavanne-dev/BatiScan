"""E03 – Directions dominantes en plan (orientation automatique).

Retourne plusieurs alternatives pondérées ; le logiciel ne choisit pas
silencieusement le sens (E03). Angles en degrés, dans [0, 90[, mesurés depuis
l'axe X du repère déclaré, sens trigonométrique.
"""

from __future__ import annotations

import numpy as np

from . import geometrie as g

DEFAUTS = {
    "voxel_m": 0.03,
    "rayon_normales_m": 0.12,
    "max_voisins": 30,
    "tolerance_verticale_deg": 10.0,
    "pas_deg": 0.5,
    "lissage_deg": 1.5,
    "nombre_alternatives": 3,
    "ecart_min_deg": 5.0,
}


def detecter(pts, params: dict, rng: np.random.Generator, avertissements: list[str]) -> tuple[list[dict], int]:
    echant = g.sous_echantillonner(pts.local, params["voxel_m"])
    if len(echant) < 50:
        avertissements.append("Trop peu de points pour estimer une orientation.")
        return [], len(echant)
    n = g.normales(echant, params["rayon_normales_m"], params["max_voisins"])
    vert = np.abs(n[:, 2]) <= np.sin(np.radians(params["tolerance_verticale_deg"]))
    nh = n[vert, :2]
    if len(nh) < 50:
        avertissements.append("Pas assez de surfaces verticales pour estimer une orientation.")
        return [], len(echant)
    angles = np.degrees(np.arctan2(nh[:, 1], nh[:, 0])) % 90.0
    pas = params["pas_deg"]
    nb = int(round(90 / pas))
    hist, _ = np.histogram(angles, bins=nb, range=(0, 90))
    k = max(1, int(round(params["lissage_deg"] / pas)))
    # lissage circulaire (période 90°)
    etendu = np.r_[hist[-k:], hist, hist[:k]]
    lisse = np.convolve(etendu, np.ones(2 * k + 1), mode="same")[k:-k]
    total = float(lisse.sum()) or 1.0
    alternatives = []
    for i in np.argsort(-lisse, kind="stable"):
        a = (i + 0.5) * pas
        if all(min(abs(a - b), 90 - abs(a - b)) >= params["ecart_min_deg"] for b, _ in alternatives):
            alternatives.append((a, float(lisse[i] / total)))
        if len(alternatives) >= params["nombre_alternatives"]:
            break
    props = []
    for j, (a, poids) in enumerate(alternatives):
        # Affinage : moyenne circulaire des angles proches du pic
        proches = angles[np.minimum(np.abs(angles - a), 90 - np.abs(angles - a)) <= params["lissage_deg"]]
        if len(proches):
            ang4 = np.radians(proches * 4)
            a = float(np.degrees(np.arctan2(np.sin(ang4).mean(), np.cos(ang4).mean())) / 4) % 90.0
        props.append({"id": f"D{j + 1:03d}", "type": "direction", "angle_deg": float(a), "poids_relatif": poids})
    return props, len(echant)
