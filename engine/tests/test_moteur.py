"""Tests du moteur BatiScan sur jeux synthétiques (J01, J07, J08, J09…)."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import synthetique as syn  # noqa: E402
from batiscan_engine.__main__ import main  # noqa: E402

RACINE = Path(__file__).resolve().parents[2]


def executer(chemin_req: Path) -> dict:
    rep = chemin_req.parent / "reponse.json"
    assert main([str(chemin_req), str(rep)]) == 0
    reponse = json.loads(rep.read_text(encoding="utf-8"))
    try:
        import jsonschema

        schema = json.loads((RACINE / "contracts" / "reponse.schema.json").read_text(encoding="utf-8"))
        jsonschema.validate(reponse, schema)
    except ImportError:
        pass
    return reponse


# ── Contrat ───────────────────────────────────────────────────────────────────


def test_version_contrat_refusee(tmp_path):
    req = syn.ecrire_requete(tmp_path, "niveaux", np.zeros((10, 3)))
    d = json.loads(req.read_text())
    d["contract_version"] = "9.0.0"
    req.write_text(json.dumps(d))
    r = executer(req)
    assert r["statut"] == "erreur" and r["erreurs"][0]["code"] == "VERSION_CONTRAT"


def test_parametre_inconnu_refuse(tmp_path):
    req = syn.ecrire_requete(tmp_path, "niveaux", np.zeros((10, 3)), parametres={"inconnu": 1})
    r = executer(req)
    assert r["erreurs"][0]["code"] == "PARAMETRE_INCONNU"


def test_zone_vide_signalee(tmp_path):
    pts = np.random.default_rng(0).uniform(0, 1, (100, 3))
    req = syn.ecrire_requete(tmp_path, "niveaux", pts, zone={"nom": "vide", "min": [5, 5, 5], "max": [6, 6, 6]})
    r = executer(req)
    assert r["statut"] == "erreur" and r["erreurs"][0]["code"] == "ZONE_VIDE"


def test_reponse_declare_unites_repere_parametres(tmp_path):
    rng = np.random.default_rng(1)
    req = syn.ecrire_requete(tmp_path, "niveaux", syn.dalle(rng, 0, 0, 4, 4, 0.0))
    r = executer(req)
    assert r["unites"] == {"longueur": "m", "angle": "deg"}
    assert r["repere"]["nom"] == "revit_interne_m"
    assert r["parametres"]["voxel_m"] == 0.02
    assert r["nombre_points"]["recus"] > 0 and 0 < r["couverture"]["ratio"] <= 1


# ── E04 Niveaux ───────────────────────────────────────────────────────────────


def maison(rng):
    return np.vstack(
        [
            syn.dalle(rng, 0, 0, 6, 5, 0.00),  # sol RDC
            syn.dalle(rng, 0, 0, 6, 5, 2.60),  # plafond RDC
            syn.dalle(rng, 0, 0, 6, 5, 2.85),  # sol étage
            syn.dalle(rng, 0, 0, 6, 5, 5.30),  # plafond étage
            syn.dalle(rng, 2, 2, 1.0, 0.8, 0.75),  # table
            syn.mur_incline(rng, [0, 0], [6, 0], 0.0, 5.3, 0.2, 0.0, densite=0.03),
        ]
    )


def test_niveaux_altitudes_et_types(tmp_path):
    rng = np.random.default_rng(2)
    r = executer(syn.ecrire_requete(tmp_path, "niveaux", maison(rng)))
    props = r["propositions"]
    alt = sorted(p["altitude_m"] for p in props)
    for attendu in (0.0, 0.75, 2.60, 2.85, 5.30):
        assert min(abs(a - attendu) for a in alt) <= 0.010, (attendu, alt)  # cible J02 : ≤ 10 mm
    table = min(props, key=lambda p: abs(p["altitude_m"] - 0.75))
    assert table["type_suppose"] == "mobilier_ou_plan_de_travail"
    sol = min(props, key=lambda p: abs(p["altitude_m"]))
    assert sol["type_suppose"] == "sol"


def test_niveaux_reproductibles(tmp_path):
    rng = np.random.default_rng(3)
    pts = maison(rng)
    r1 = executer(syn.ecrire_requete(tmp_path / "a", "niveaux", pts))
    r2 = executer(syn.ecrire_requete(tmp_path / "b", "niveaux", pts))
    assert r1["propositions"] == r2["propositions"]


def test_precision_loin_origine(tmp_path):
    """Coordonnées géoréférencées (~150 km) : l'origine déclarée préserve le mm."""
    rng = np.random.default_rng(4)
    decalage = np.array([152_345.0, 167_890.0, 120.0])
    pts = syn.dalle(rng, 0, 0, 4, 4, 0.0) + decalage
    r = executer(syn.ecrire_requete(tmp_path, "niveaux", pts, origine=decalage))
    assert abs(r["propositions"][0]["altitude_m"] - 120.0) <= 0.002


# ── E07 / E24 Murs et dévers (J07) ────────────────────────────────────────────


def test_mur_incline_j07(tmp_path):
    rng = np.random.default_rng(5)
    pts = np.vstack(
        [
            syn.mur_incline(rng, [0, 0], [5, 0], 0.0, 2.6, 0.20, 1.0),  # non plombé 1°
            syn.mur_incline(rng, [0, 3], [5, 3], 0.0, 2.6, 0.30, 0.0),  # plombé
        ]
    )
    r = executer(syn.ecrire_requete(tmp_path, "murs", pts))
    murs = [m for m in r["propositions"] if m["nombre_faces"] == 2]
    assert len(murs) == 2, r["propositions"]
    incline = min(murs, key=lambda m: abs(m["debut"][1]))
    droit = max(murs, key=lambda m: abs(m["debut"][1]))
    assert incline["epaisseur_mesuree"] and abs(incline["epaisseur_m"] - 0.20) <= 0.005
    assert abs(droit["epaisseur_m"] - 0.30) <= 0.005
    assert abs(abs(incline["devers"]["angle_deg"]) - 1.0) <= 0.05
    assert incline["plombage"] == "non_plombe"
    assert droit["plombage"] == "plombe"
    # déplacement au sommet ≈ tan(1°)·2,6 m = 45,4 mm, sens = normale gauche (+Y)
    assert abs(abs(incline["devers"]["deplacement_sommet_m"]) - np.tan(np.radians(1)) * 2.6) <= 0.003
    assert incline["devers"]["direction_inclinaison"][1] > 0.99
    assert abs(incline["longueur_m"] - 5.0) <= 0.05


def test_mur_face_unique_epaisseur_inconnue(tmp_path):
    rng = np.random.default_rng(6)
    pts = syn.mur_incline(rng, [0, 0], [4, 0], 0.0, 2.5, 0.2, 0.0, deux_faces=False)
    r = executer(syn.ecrire_requete(tmp_path, "murs", pts))
    m = r["propositions"][0]
    assert m["epaisseur_m"] is None and not m["epaisseur_mesuree"] and m["cote_matiere"] == "inconnu"


def test_mur_trop_bas_devers_incertain(tmp_path):
    rng = np.random.default_rng(7)
    pts = syn.mur_incline(rng, [0, 0], [4, 0], 0.0, 0.7, 0.2, 2.0)
    r = executer(syn.ecrire_requete(tmp_path, "murs", pts))
    assert all(m["plombage"] == "incertain" for m in r["propositions"])


# ── E05 / E24 Coupes multiples (J07 : ≤ 1 mm) ─────────────────────────────────


def test_coupes_deux_altitudes_devers(tmp_path):
    rng = np.random.default_rng(8)
    angle = 1.5
    pts = syn.mur_incline(rng, [0, 0], [5, 0], 0.0, 2.6, 0.2, angle, densite=0.006, deux_faces=False)
    r = executer(syn.ecrire_requete(tmp_path, "coupes", pts, parametres={"altitudes_m": [0.5, 2.0]}))
    t1, t2 = r["propositions"]
    assert t1["z"] == 0.5 and t2["z"] == 2.0 and t1["segments"] and t2["segments"]
    y1 = np.mean([t1["segments"][0]["debut"][1], t1["segments"][0]["fin"][1]])
    y2 = np.mean([t2["segments"][0]["debut"][1], t2["segments"][0]["fin"][1]])
    attendu = np.tan(np.radians(angle)) * 1.5
    assert abs((y2 - y1) - attendu) <= 0.001


def test_coupe_altitude_non_observee(tmp_path):
    rng = np.random.default_rng(9)
    pts = syn.mur_incline(rng, [0, 0], [3, 0], 0.0, 2.5, 0.2, 0.0)
    r = executer(syn.ecrire_requete(tmp_path, "coupes", pts, parametres={"altitudes_m": [1.0, 4.0]}))
    assert r["statut"] == "partiel" and r["propositions"][1]["segments"] == []


# ── E12 Structure ─────────────────────────────────────────────────────────────


def test_poteau_et_poutre(tmp_path):
    rng = np.random.default_rng(10)
    pts = np.vstack(
        [
            syn.dalle(rng, 0, 0, 8, 6, 0.0),
            syn.dalle(rng, 0, 0, 8, 6, 3.0),
            syn.poteau(rng, 2.0, 2.0, 0.30, 0.30, 0.0, 3.0),
            syn.poutre(rng, 1.0, 4.0, 7.0, 0.25, 2.6, 0.40),
        ]
    )
    r = executer(syn.ecrire_requete(tmp_path, "structure", pts))
    poteaux = [p for p in r["propositions"] if p["type"] == "poteau"]
    poutres = [p for p in r["propositions"] if p["type"] == "poutre"]
    assert len(poteaux) == 1 and len(poutres) == 1, r["propositions"]
    pt = poteaux[0]
    assert np.allclose(pt["centre"], [2.0, 2.0], atol=0.03)
    assert abs(pt["encombrement_observe"]["a_m"] - 0.30) <= 0.03
    pr = poutres[0]
    assert abs(pr["z_sous_face"] - 2.6) <= 0.01 and abs(pr["retombee_observee_m"] - 0.40) <= 0.02
    assert abs(pr["largeur_observee_m"] - 0.25) <= 0.03 and abs(pr["longueur_observee_m"] - 6.0) <= 0.1


# ── E14 Écarts ────────────────────────────────────────────────────────────────


def test_ecarts_et_non_observe(tmp_path):
    rng = np.random.default_rng(11)
    pts = syn.rectangle(rng, [0, 0.005, 0], [1, 0, 0], [0, 0, 1], 4, 2.5)  # face réelle à +5 mm
    surfaces = [
        {"id": "F1", "origine": [0, 0, 0], "u": [1, 0, 0], "v": [0, 0, 1], "longueur_u": 4, "longueur_v": 2.5},
        {"id": "F2", "origine": [10, 0, 0], "u": [1, 0, 0], "v": [0, 0, 1], "longueur_u": 4, "longueur_v": 2.5},
    ]
    r = executer(syn.ecrire_requete(tmp_path, "ecarts", pts, parametres={"surfaces": surfaces}))
    f1, f2 = r["propositions"]
    # normale u×v = (1,0,0)×(0,0,1) = (0,−1,0) ⇒ point à y=+5 mm a une distance signée −5 mm
    assert abs(f1["moyenne_signee_m"] + 0.005) <= 0.0005
    assert f1["statut"] == "dans_tolerance"
    assert f2["statut"] == "non_observe" and f2["nombre_points"] == 0


# ── E03 Directions ────────────────────────────────────────────────────────────


def test_directions_batiment_tourne(tmp_path):
    rng = np.random.default_rng(12)
    a = np.radians(23.0)
    u = np.array([np.cos(a), np.sin(a)])
    v = np.array([-np.sin(a), np.cos(a)])
    pts = np.vstack(
        [
            syn.mur_incline(rng, [0, 0], 6 * u, 0, 2.5, 0.2, 0, densite=0.02),
            syn.mur_incline(rng, [0, 0], 4 * v, 0, 2.5, 0.2, 0, densite=0.02),
        ]
    )
    r = executer(syn.ecrire_requete(tmp_path, "directions", pts))
    assert abs(r["propositions"][0]["angle_deg"] - 23.0) <= 0.3
    assert len(r["propositions"]) >= 1


# ── E17 Nettoyage réversible ──────────────────────────────────────────────────


def test_nettoyage_masque_reversible(tmp_path):
    rng = np.random.default_rng(13)
    base = syn.dalle(rng, 0, 0, 3, 3, 0.0)
    parasites = rng.uniform([0, 0, 0.5], [3, 3, 2.0], (50, 3))
    protege = rng.uniform([10, 10, 1.0], [10.5, 10.5, 1.5], (5, 3))  # isolé mais protégé
    pts = np.vstack([base, parasites, protege])
    req = syn.ecrire_requete(
        tmp_path,
        "nettoyage",
        pts,
        parametres={"chemin_masque": "masque.bin", "zones_protegees": [{"min": [9, 9, 0], "max": [11, 11, 2]}]},
    )
    empreinte_avant = hashlib.sha256((tmp_path / "points.bin").read_bytes()).hexdigest()
    r = executer(req)
    masque = np.fromfile(tmp_path / "masque.bin", dtype=np.uint8)
    assert len(masque) == len(pts)
    assert masque[-5:].all(), "zone protégée conservée"
    assert (masque[len(base) : len(base) + 50] == 0).mean() > 0.9
    assert hashlib.sha256((tmp_path / "points.bin").read_bytes()).hexdigest() == empreinte_avant
    assert r["propositions"][0]["reversible"]


def test_diagnostic(capsys):
    assert main(["--diagnostic"]) == 0
    d = json.loads(capsys.readouterr().out)
    assert d["moteur"]["open3d"] == "0.20.0"
