"""Point d'entrée : ``python -m batiscan_engine <requete.json> <reponse.json>``.

Autres usages :
  ``python -m batiscan_engine --diagnostic``  → versions (JSON) sur stdout.

Le code de sortie vaut 0 dès qu'une réponse est écrite, même en erreur
(le statut est dans la réponse). Code 2 : arguments invalides ou réponse
impossible à écrire.
"""

from __future__ import annotations

import json
import sys
import time
import traceback
from pathlib import Path

import numpy as np

from . import coupes, directions, ecarts, geometrie, murs, nettoyage, niveaux, structure
from .contrat import ErreurContrat, info_moteur, parametres_effectifs, reponse_vide, valider_requete
from .points import charger

OPERATIONS = {
    "niveaux": niveaux,
    "murs": murs,
    "coupes": coupes,
    "structure": structure,
    "ecarts": ecarts,
    "directions": directions,
    "nettoyage": nettoyage,
}


def progression(valeur: float, message: str) -> None:
    print(f"PROGRESSION {valeur:.2f} {message}", flush=True)


def traiter(requete: dict, dossier: Path) -> dict:
    """Exécute une requête validée et retourne la réponse (dict)."""
    t0 = time.perf_counter()
    reponse = reponse_vide(requete)
    try:
        valider_requete(requete)
        module = OPERATIONS[requete["operation"]]
        params = parametres_effectifs(module.DEFAUTS, requete.get("parametres"))
        reponse["parametres"] = params
        graine = int(requete["graine"])
        rng = np.random.default_rng(graine)

        progression(0.05, "Lecture des points")
        pts = charger(requete, dossier)
        reponse["nombre_points"]["recus"] = pts.recus
        reponse["nombre_points"]["dans_zone"] = int(len(pts.local))
        if len(pts.local) == 0:
            raise ErreurContrat("ZONE_VIDE", "Aucun point dans la zone : zone non observée.")

        progression(0.15, f"Calcul « {requete['operation']} »")
        avert: list[str] = []
        if module is nettoyage:
            props, traites = module.detecter(pts, params, rng, avert, dossier)
        else:
            props, traites = module.detecter(pts, params, rng, avert)
        reponse["propositions"] = props
        reponse["nombre_points"]["traites"] = int(traites)
        reponse["avertissements"].extend(avert)

        zone = requete.get("zone")
        if zone:
            mini = np.asarray(zone["min"][:2], dtype=float) - pts.origine[:2]
            maxi = np.asarray(zone["max"][:2], dtype=float) - pts.origine[:2]
        else:
            mini, maxi = pts.local[:, :2].min(axis=0), pts.local[:, :2].max(axis=0)
        couv = geometrie.couverture_grille(pts.local[:, :2], mini, maxi, 0.10)
        couv["methode"] = "grille_xy_zone"
        reponse["couverture"] = couv
        reponse["statut"] = "partiel" if avert else "ok"
        progression(1.0, "Terminé")
    except ErreurContrat as e:
        reponse["statut"] = "erreur"
        reponse["erreurs"].append({"code": e.code, "message": e.message})
    except MemoryError:
        reponse["statut"] = "erreur"
        reponse["erreurs"].append({"code": "MEMOIRE", "message": "Mémoire insuffisante : réduire la zone ou augmenter voxel_m."})
    except Exception as e:  # noqa: BLE001 – toute erreur est retournée dans le contrat
        reponse["statut"] = "erreur"
        reponse["erreurs"].append({"code": "ERREUR_INTERNE", "message": f"{type(e).__name__}: {e}", "trace": traceback.format_exc()})
    reponse["duree_s"] = round(time.perf_counter() - t0, 4)
    return reponse


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # lu en UTF-8 par le C#
    if len(argv) == 1 and argv[0] == "--diagnostic":
        print(json.dumps({"contract_version": "1.0.0", "moteur": info_moteur(), "operations": sorted(OPERATIONS)}))
        return 0
    if len(argv) != 2:
        print(__doc__, file=sys.stderr)
        return 2
    chemin_req, chemin_rep = Path(argv[0]), Path(argv[1])
    try:
        requete = json.loads(chemin_req.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as e:
        reponse = reponse_vide(None)
        reponse["erreurs"].append({"code": "REQUETE_ILLISIBLE", "message": str(e)})
    else:
        reponse = traiter(requete, chemin_req.resolve().parent)
    try:
        chemin_rep.write_text(json.dumps(reponse, ensure_ascii=False, indent=1), encoding="utf-8")
    except OSError as e:
        print(f"Réponse impossible à écrire : {e}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
