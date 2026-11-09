"""Lecture/validation des requêtes et construction des réponses (contrat 1.0.0).

Voir ``contracts/requete.schema.json`` et ``contracts/reponse.schema.json``.
"""

from __future__ import annotations

import json
import platform
from pathlib import Path
from typing import Any

import open3d as o3d

from . import __version__

VERSION_CONTRAT = "1.0.0"
DOSSIER_CONTRATS = Path(__file__).resolve().parents[2] / "contracts"


class ErreurContrat(Exception):
    """Requête non conforme : le moteur répond avec statut « erreur »."""

    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code
        self.message = message


def _schema(nom: str) -> dict | None:
    chemin = DOSSIER_CONTRATS / nom
    if not chemin.exists():
        return None
    return json.loads(chemin.read_text(encoding="utf-8"))


def valider_requete(requete: dict) -> None:
    """Valide la requête contre le schéma JSON versionné."""
    if requete.get("contract_version") != VERSION_CONTRAT:
        raise ErreurContrat(
            "VERSION_CONTRAT",
            f"Version de contrat « {requete.get('contract_version')} » non prise en charge (attendu {VERSION_CONTRAT}).",
        )
    schema = _schema("requete.schema.json")
    if schema is None:
        return
    try:
        import jsonschema
    except ImportError:  # validation minimale ci-dessus déjà faite
        return
    erreurs = sorted(jsonschema.Draft202012Validator(schema).iter_errors(requete), key=lambda e: list(e.path))
    if erreurs:
        e = erreurs[0]
        chemin = "/".join(str(p) for p in e.path) or "(racine)"
        raise ErreurContrat("REQUETE_INVALIDE", f"Requête non conforme au contrat en « {chemin} » : {e.message}")


def parametres_effectifs(defauts: dict, fournis: dict | None) -> dict:
    """Fusionne paramètres par défaut et fournis ; refuse les clés inconnues."""
    fournis = fournis or {}
    inconnus = sorted(set(fournis) - set(defauts))
    if inconnus:
        raise ErreurContrat("PARAMETRE_INCONNU", f"Paramètre(s) inconnu(s) : {', '.join(inconnus)}")
    effectifs = dict(defauts)
    effectifs.update(fournis)
    return effectifs


def info_moteur() -> dict:
    return {"version": __version__, "open3d": o3d.__version__, "python": platform.python_version()}


def reponse_vide(requete: dict | None) -> dict[str, Any]:
    """Squelette de réponse reprenant le contexte déclaré par la requête."""
    requete = requete or {}
    return {
        "contract_version": VERSION_CONTRAT,
        "id_requete": str(requete.get("id_requete", "")),
        "operation": str(requete.get("operation", "")),
        "statut": "erreur",
        "moteur": info_moteur(),
        "unites": requete.get("unites", {"longueur": "m", "angle": "deg"}),
        "repere": requete.get("repere", {}),
        "source": requete.get("source", {}),
        "zone": requete.get("zone"),
        "parametres": {},
        "graine": int(requete.get("graine", 0) or 0),
        "nombre_points": {"recus": 0, "dans_zone": 0, "traites": 0},
        "couverture": {"methode": "aucune", "ratio": 0.0},
        "propositions": [],
        "avertissements": [],
        "erreurs": [],
        "duree_s": 0.0,
    }
