"""Lecture des points d'entrée et découpe par zone explicite (E05).

Le fichier de points n'est ouvert qu'en lecture (E17 : source jamais modifiée).
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np
import open3d as o3d

from .contrat import ErreurContrat

FORMATS_BINAIRES = {"xyz_f32_le": np.dtype("<f4"), "xyz_f64_le": np.dtype("<f8")}


@dataclass
class Points:
    """Points d'une requête, dans un repère local décalé de ``origine``.

    Coordonnée déclarée (repère de la requête) = ``local + origine``.
    Les calculs se font en local (précision), les sorties sont ré-exprimées
    dans le repère déclaré via :meth:`vers_repere`.
    """

    local: np.ndarray  # (N, 3) float64
    origine: np.ndarray  # (3,)
    recus: int
    indices_zone: np.ndarray  # indices des points retenus dans le tableau reçu

    def vers_repere(self, p_local) -> list[float]:
        p = np.asarray(p_local, dtype=float)
        return [float(v) for v in (p + self.origine[: p.shape[-1]])]

    def vers_local(self, p_repere) -> np.ndarray:
        p = np.asarray(p_repere, dtype=float)
        return p - self.origine[: p.shape[-1]]


def _resoudre(chemin: str, dossier_requete: Path) -> Path:
    p = Path(chemin)
    return p if p.is_absolute() else (dossier_requete / p)


def lire_brut(spec: dict, dossier_requete: Path) -> tuple[np.ndarray, np.ndarray]:
    """Lit les points (N, 3) tels que fournis et l'origine déclarée."""
    chemin = _resoudre(spec["chemin"], dossier_requete)
    if not chemin.exists():
        raise ErreurContrat("POINTS_ABSENTS", f"Fichier de points introuvable : {chemin}")
    origine = np.asarray(spec.get("origine_m", [0.0, 0.0, 0.0]), dtype=float)
    fmt = spec["format"]
    if fmt in FORMATS_BINAIRES:
        dt = FORMATS_BINAIRES[fmt]
        taille = chemin.stat().st_size
        if taille % (3 * dt.itemsize) != 0:
            raise ErreurContrat("POINTS_CORROMPUS", f"Taille {taille} o incompatible avec le format {fmt}.")
        donnees = np.fromfile(chemin, dtype=dt).reshape(-1, 3).astype(np.float64)
        attendu = spec.get("nombre")
        if attendu is not None and attendu != len(donnees):
            raise ErreurContrat("POINTS_CORROMPUS", f"{len(donnees)} points lus, {attendu} annoncés.")
        return donnees, origine
    if fmt == "fichier_open3d":
        nuage = o3d.io.read_point_cloud(str(chemin))
        donnees = np.asarray(nuage.points, dtype=np.float64)
        if len(donnees) == 0:
            raise ErreurContrat("FORMAT_NON_SUPPORTE", f"Aucun point lisible par Open3D dans {chemin.name}.")
        return donnees - origine, origine
    raise ErreurContrat("FORMAT_NON_SUPPORTE", f"Format de points « {fmt} » non supporté.")


def masque_zone(local: np.ndarray, origine: np.ndarray, zone: dict | None) -> np.ndarray:
    """Masque des points dans la boîte de zone (éventuellement tournée autour de Z)."""
    if not zone:
        return np.ones(len(local), dtype=bool)
    mini = np.asarray(zone["min"], dtype=float) - origine
    maxi = np.asarray(zone["max"], dtype=float) - origine
    p = local
    rot = float(zone.get("rotation_deg", 0.0) or 0.0)
    if rot:
        centre = (mini + maxi) / 2
        a = np.radians(-rot)
        c, s = np.cos(a), np.sin(a)
        q = p - centre
        p = np.c_[c * q[:, 0] - s * q[:, 1], s * q[:, 0] + c * q[:, 1], q[:, 2]] + centre
    return np.all((p >= mini) & (p <= maxi), axis=1)


def charger(requete: dict, dossier_requete: Path) -> Points:
    brut, origine = lire_brut(requete["points"], dossier_requete)
    masque = masque_zone(brut, origine, requete.get("zone"))
    indices = np.nonzero(masque)[0]
    return Points(local=brut[indices], origine=origine, recus=len(brut), indices_zone=indices)


def ecrire_xyz_f32(chemin: Path, points_repere: np.ndarray, origine) -> None:
    """Utilitaire de test : écrit un fichier ``xyz_f32_le`` relatif à ``origine``."""
    (np.asarray(points_repere, dtype=np.float64) - np.asarray(origine, dtype=float)).astype("<f4").tofile(chemin)
