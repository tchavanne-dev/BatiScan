"""Outils géométriques communs du moteur (numpy + Open3D).

Toutes les longueurs sont en mètres. Les fonctions aléatoires reçoivent un
``numpy.random.Generator`` explicite : les résultats sont reproductibles.

Note : ``PointCloud.segment_plane`` d'Open3D 0.20 est parallélisé et ne donne
pas un résultat stable même avec ``o3d.utility.random.seed`` (vérifié le
2026-11-09). Le RANSAC de plan est donc réimplémenté ici (règle CLAUDE.md :
graines fixées, résultats reproductibles).
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import open3d as o3d


# ── Nuages Open3D ─────────────────────────────────────────────────────────────


def vers_open3d(points: np.ndarray) -> o3d.geometry.PointCloud:
    """Convertit un tableau (N, 3) en nuage Open3D."""
    nuage = o3d.geometry.PointCloud()
    nuage.points = o3d.utility.Vector3dVector(np.ascontiguousarray(points, dtype=np.float64))
    return nuage


def sous_echantillonner(points: np.ndarray, voxel_m: float) -> np.ndarray:
    """Sous-échantillonnage par voxel (``voxel_down_sample``)."""
    if voxel_m <= 0 or len(points) == 0:
        return points
    return np.asarray(vers_open3d(points).voxel_down_sample(voxel_m).points)


def normales(points: np.ndarray, rayon_m: float, max_nn: int) -> np.ndarray:
    """Normales non orientées (unitaires) estimées par voisinage hybride."""
    if len(points) == 0:
        return np.zeros((0, 3))
    nuage = vers_open3d(points)
    nuage.estimate_normals(o3d.geometry.KDTreeSearchParamHybrid(radius=rayon_m, max_nn=max_nn))
    return np.asarray(nuage.normals)


def dbscan(points: np.ndarray, eps_m: float, min_points: int) -> np.ndarray:
    """Étiquettes DBSCAN (−1 = bruit). Déterministe."""
    if len(points) == 0:
        return np.zeros(0, dtype=int)
    return np.asarray(vers_open3d(points).cluster_dbscan(eps=eps_m, min_points=min_points, print_progress=False))


# ── RANSAC de plan reproductible ──────────────────────────────────────────────


@dataclass
class Plan:
    """Plan n·p + d = 0, n unitaire."""

    normale: np.ndarray
    d: float
    inliers: np.ndarray  # indices
    rms_m: float

    def distances(self, points: np.ndarray) -> np.ndarray:
        return points @ self.normale + self.d


def ajuster_plan_moindres_carres(points: np.ndarray) -> tuple[np.ndarray, float, float]:
    """Plan total least squares (SVD). Retourne (normale, d, rms)."""
    centre = points.mean(axis=0)
    _, _, vt = np.linalg.svd(points - centre, full_matrices=False)
    n = vt[-1]
    n = n / np.linalg.norm(n)
    d = -float(n @ centre)
    rms = float(np.sqrt(np.mean((points @ n + d) ** 2)))
    return n, d, rms


def ransac_plan(
    points: np.ndarray,
    seuil_m: float,
    iterations: int,
    rng: np.random.Generator,
    contrainte=None,
    taille_lot: int = 64,
) -> Plan | None:
    """RANSAC de plan avec graine explicite.

    ``contrainte(normale) -> bool`` permet d'écarter les hypothèses
    (ex. plans verticaux uniquement). Le meilleur consensus est raffiné par
    moindres carrés sur ses inliers.
    """
    n_pts = len(points)
    if n_pts < 3:
        return None
    meilleur_score = 0
    meilleur = None
    restant = iterations
    while restant > 0:
        lot = min(taille_lot, restant)
        restant -= lot
        idx = rng.integers(0, n_pts, size=(lot, 3))
        a, b, c = points[idx[:, 0]], points[idx[:, 1]], points[idx[:, 2]]
        n = np.cross(b - a, c - a)
        norme = np.linalg.norm(n, axis=1)
        valide = norme > 1e-12
        if not valide.any():
            continue
        n = n[valide] / norme[valide, None]
        a = a[valide]
        if contrainte is not None:
            ok = np.array([contrainte(v) for v in n], dtype=bool)
            if not ok.any():
                continue
            n, a = n[ok], a[ok]
        d = -np.einsum("ij,ij->i", n, a)
        dist = np.abs(points @ n.T + d)  # (N, lot)
        scores = (dist < seuil_m).sum(axis=0)
        k = int(np.argmax(scores))
        if scores[k] > meilleur_score:
            meilleur_score = int(scores[k])
            meilleur = (n[k], float(d[k]))
    if meilleur is None or meilleur_score < 3:
        return None
    n, d = meilleur
    inliers = np.nonzero(np.abs(points @ n + d) < seuil_m)[0]
    n2, d2, rms = ajuster_plan_moindres_carres(points[inliers])
    if contrainte is not None and not contrainte(n2):
        n2, d2 = n, d
    # Réévaluation des inliers sur le plan raffiné
    inliers = np.nonzero(np.abs(points @ n2 + d2) < seuil_m)[0]
    if len(inliers) >= 3:
        rms = float(np.sqrt(np.mean((points[inliers] @ n2 + d2) ** 2)))
    return Plan(normale=n2, d=d2, inliers=inliers, rms_m=rms)


# ── Droites 2D ────────────────────────────────────────────────────────────────


@dataclass
class Segment2D:
    debut: np.ndarray
    fin: np.ndarray
    rms_m: float
    nombre_points: int


def ransac_droite_2d(points: np.ndarray, seuil_m: float, iterations: int, rng: np.random.Generator):
    """RANSAC de droite 2D. Retourne (direction unitaire, point, inliers) ou None."""
    n_pts = len(points)
    if n_pts < 2:
        return None
    idx = rng.integers(0, n_pts, size=(iterations, 2))
    a, b = points[idx[:, 0]], points[idx[:, 1]]
    u = b - a
    norme = np.linalg.norm(u, axis=1)
    ok = norme > 1e-9
    if not ok.any():
        return None
    u = u[ok] / norme[ok, None]
    a = a[ok]
    nrm = np.c_[-u[:, 1], u[:, 0]]
    meilleur, meilleur_score = None, 0
    for debut in range(0, len(u), 64):
        sl = slice(debut, debut + 64)
        dist = np.abs(
            (points[:, 0, None] - a[sl, 0][None, :]) * nrm[sl, 0][None, :]
            + (points[:, 1, None] - a[sl, 1][None, :]) * nrm[sl, 1][None, :]
        )
        scores = (dist < seuil_m).sum(axis=0)
        k = int(np.argmax(scores))
        if scores[k] > meilleur_score:
            meilleur_score = int(scores[k])
            meilleur = (u[sl][k], a[sl][k])
    if meilleur is None:
        return None
    u0, a0 = meilleur
    n0 = np.array([-u0[1], u0[0]])
    inl = np.nonzero(np.abs((points - a0) @ n0) < seuil_m)[0]
    if len(inl) < 2:
        return None
    centre = points[inl].mean(axis=0)
    _, _, vt = np.linalg.svd(points[inl] - centre, full_matrices=False)
    u1 = vt[0] / np.linalg.norm(vt[0])
    n1 = np.array([-u1[1], u1[0]])
    inl = np.nonzero(np.abs((points - centre) @ n1) < seuil_m)[0]
    return u1, centre, inl


def decouper_par_trous(abscisses: np.ndarray, ecart_max_m: float) -> list[np.ndarray]:
    """Découpe des indices triés selon l'abscisse là où un trou dépasse ``ecart_max_m``."""
    ordre = np.argsort(abscisses)
    s = abscisses[ordre]
    coupures = np.nonzero(np.diff(s) > ecart_max_m)[0]
    return np.split(ordre, coupures + 1)


# ── Couverture ────────────────────────────────────────────────────────────────


def couverture_grille(coords_2d: np.ndarray, mini: np.ndarray, maxi: np.ndarray, cellule_m: float) -> dict:
    """Ratio de cellules occupées d'une grille régulière sur le rectangle [mini, maxi].

    Une cellule sans point est « non observée » ; elle n'est jamais comptée
    comme conforme (E06, E14).
    """
    etendue = np.maximum(np.asarray(maxi) - np.asarray(mini), cellule_m)
    nx, ny = (np.ceil(etendue / cellule_m)).astype(int)
    total = int(nx * ny)
    if len(coords_2d) == 0 or total == 0:
        return {"methode": "grille", "taille_cellule_m": cellule_m, "cellules_occupees": 0, "cellules_totales": total, "ratio": 0.0}
    ij = np.floor((coords_2d - mini) / cellule_m).astype(int)
    dedans = (ij[:, 0] >= 0) & (ij[:, 0] < nx) & (ij[:, 1] >= 0) & (ij[:, 1] < ny)
    occupees = len(np.unique(ij[dedans, 0] * ny + ij[dedans, 1]))
    return {
        "methode": "grille",
        "taille_cellule_m": cellule_m,
        "cellules_occupees": int(occupees),
        "cellules_totales": total,
        "ratio": float(occupees / total),
    }


def cellules_occupees(coords_2d: np.ndarray, cellule_m: float) -> set[tuple[int, int]]:
    """Ensemble des cellules (i, j) occupées, pour les comparaisons d'emprise."""
    if len(coords_2d) == 0:
        return set()
    ij = np.floor(coords_2d / cellule_m).astype(np.int64)
    return set(map(tuple, np.unique(ij, axis=0)))


def boite_orientee_2d(points: np.ndarray, pas_deg: float = 0.5):
    """Rectangle d'aire minimale (centre, axe long, longueur, largeur), bornes en percentiles 0,5/99,5.

    L'ACP seule est instable sur une section carrée (axe principal arbitraire) :
    on balaie les orientations sur [0°, 90°[ et on retient l'aire minimale.
    """
    centre = points.mean(axis=0)
    if len(points) < 3:
        return centre, np.array([1.0, 0.0]), 0.0, 0.0
    q = points - centre
    meilleur = None
    for a in np.radians(np.arange(0.0, 90.0, pas_deg)):
        u = np.array([np.cos(a), np.sin(a)])
        v = np.array([-u[1], u[0]])
        s0, s1 = np.percentile(q @ u, [0.5, 99.5])
        t0, t1 = np.percentile(q @ v, [0.5, 99.5])
        aire = (s1 - s0) * (t1 - t0)
        if meilleur is None or aire < meilleur[0]:
            meilleur = (aire, u, v, s0, s1, t0, t1)
    _, u, v, s0, s1, t0, t1 = meilleur
    if t1 - t0 > s1 - s0:  # axe u = plus grande dimension
        u, v = v, -u
        s0, s1, t0, t1 = t0, t1, -t1, -t0
    c = centre + u * (s0 + s1) / 2 + v * (t0 + t1) / 2
    return c, u, float(s1 - s0), float(t1 - t0)
