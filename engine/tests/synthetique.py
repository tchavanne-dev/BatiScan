"""Générateurs de nuages synthétiques (jeux J01, J07, J08…) – aucune donnée privée."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np

BRUIT = 0.002  # 2 mm


def rectangle(rng, origine, u, v, lu, lv, densite=0.01, bruit=BRUIT):
    """Échantillonne un rectangle origine + a·u + b·v, a∈[0,lu], b∈[0,lv]."""
    u = np.asarray(u, float) / np.linalg.norm(u)
    v = np.asarray(v, float) / np.linalg.norm(v)
    n = max(10, int(lu * lv / densite**2))
    a = rng.uniform(0, lu, n)
    b = rng.uniform(0, lv, n)
    nrm = np.cross(u, v)
    return np.asarray(origine, float) + np.outer(a, u) + np.outer(b, v) + np.outer(rng.normal(0, bruit, n), nrm)


def dalle(rng, x0, y0, lx, ly, z, densite=0.02):
    return rectangle(rng, [x0, y0, z], [1, 0, 0], [0, 1, 0], lx, ly, densite)


def mur_incline(rng, p0, p1, z0, hauteur, epaisseur, angle_deg, densite=0.01, deux_faces=True):
    """Mur droit dont le sommet est déplacé de tan(angle)·h vers la normale gauche de p0→p1."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    lg = np.linalg.norm(d)
    u = np.r_[d / lg, 0.0]
    nh = np.array([-u[1], u[0], 0.0])
    k = np.tan(np.radians(angle_deg))
    v = np.array([0.0, 0.0, 1.0]) + k * nh  # direction montante inclinée
    lv = hauteur * np.linalg.norm(v)
    faces = [rectangle(rng, np.r_[p0, z0], u, v, lg, lv, densite)]
    if deux_faces:
        faces.append(rectangle(rng, np.r_[p0, z0] + epaisseur * nh, u, v, lg, lv, densite))
    return np.vstack(faces)


def poteau(rng, cx, cy, a, b, z0, z1, densite=0.01):
    x0, y0 = cx - a / 2, cy - b / 2
    h = z1 - z0
    return np.vstack(
        [
            rectangle(rng, [x0, y0, z0], [1, 0, 0], [0, 0, 1], a, h, densite),
            rectangle(rng, [x0, y0 + b, z0], [1, 0, 0], [0, 0, 1], a, h, densite),
            rectangle(rng, [x0, y0, z0], [0, 1, 0], [0, 0, 1], b, h, densite),
            rectangle(rng, [x0 + a, y0, z0], [0, 1, 0], [0, 0, 1], b, h, densite),
        ]
    )


def poutre(rng, x0, y, x1, largeur, z_sous_face, retombee, densite=0.01):
    lg = x1 - x0
    return np.vstack(
        [
            rectangle(rng, [x0, y - largeur / 2, z_sous_face], [1, 0, 0], [0, 1, 0], lg, largeur, densite),
            rectangle(rng, [x0, y - largeur / 2, z_sous_face], [1, 0, 0], [0, 0, 1], lg, retombee, densite),
            rectangle(rng, [x0, y + largeur / 2, z_sous_face], [1, 0, 0], [0, 0, 1], lg, retombee, densite),
        ]
    )


def ecrire_requete(dossier: Path, operation: str, points: np.ndarray, parametres=None, zone=None, origine=None, graine=42):
    """Écrit points.bin + requete.json conformes au contrat 1.0.0 ; retourne le chemin de la requête."""
    dossier.mkdir(parents=True, exist_ok=True)
    origine = np.zeros(3) if origine is None else np.asarray(origine, float)
    (points - origine).astype("<f4").tofile(dossier / "points.bin")
    req = {
        "contract_version": "1.0.0",
        "id_requete": f"test-{operation}",
        "operation": operation,
        "unites": {"longueur": "m", "angle": "deg"},
        "repere": {"nom": "revit_interne_m", "description": "test synthétique"},
        "source": {"id": "synthetique", "chemin": "memoire", "empreinte": "sha256:test", "revision": 1},
        "points": {"chemin": "points.bin", "format": "xyz_f32_le", "nombre": int(len(points)), "origine_m": origine.tolist()},
        "parametres": parametres or {},
        "graine": graine,
    }
    if zone is not None:
        req["zone"] = zone
    chemin = dossier / "requete.json"
    chemin.write_text(json.dumps(req), encoding="utf-8")
    return chemin
