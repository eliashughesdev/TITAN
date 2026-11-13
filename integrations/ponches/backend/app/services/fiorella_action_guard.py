"""
Verificación de acciones de Fiorella ("respuestas fantasma").

Un modelo puede contestar "Listo, exporté el archivo" sin haber llamado a
ninguna herramienta, o después de que la herramienta fallara o se negara
por permisos. Antes de entregar la respuesta se compara lo que el texto
afirma con lo que el backend ejecutó realmente.

Lógica pura: sin red ni base de datos (probada en tests).
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from typing import Any


def _plain(text: str) -> str:
    """Minúsculas y sin acentos, para comparar con patrones simples."""
    normalized = unicodedata.normalize("NFKD", text or "")
    return "".join(c for c in normalized if not unicodedata.combining(c)).lower()


# Afirmaciones de haber ejecutado algo (primera persona, pasado) o de un
# resultado ya disponible. Las preguntas y ofrecimientos no cuentan.
_ACTION_STEMS = (
    "export", "gener", "cre", "envi", "sincroniz", "elimin", "borr",
    "actualiz", "registr", "descarg", "program", "clon", "movi", "enrol",
    "guard", "aplic", "ejecut",
)

_CLAIM_PATTERNS = [
    # "he exportado", "ya generé", "hemos enviado"
    re.compile(r"\b(?:he|hemos|ya)\s+(?:" + "|".join(_ACTION_STEMS) + r")\w*(?:ado|ido)\b"),
    # "exporté", "generé", "envié", "sincronicé", "eliminé", "cloné"
    re.compile(r"\b(?:export|gener|envi|sincronic|elimin|borr|actualic|registr|descargu|program|clon|enrol|guard|apliqu|ejecut)e\b"),
    # "el archivo está listo", "reporte generado", "excel disponible para descargar"
    re.compile(r"\b(?:archivo|reporte|informe|excel|exportacion)\b[^.?!]{0,40}\b(?:listo|generad[oa]|disponible|creado|descargar)\b"),
    # "listo, ..." / "hecho." al inicio
    re.compile(r"^\s*(?:listo|hecho)\b[ ,.!]"),
]

# Ofrecimientos y preguntas ("¿quieres que lo exporte?", "puedo generar...")
# no afirman nada: se ignoran esas frases.
_OFFER = re.compile(
    r"\b(?:puedo|podria|podemos|quieres|quiere|deseas|desea|te gustaria|si lo necesitas|si deseas|si quieres|para que)\b"
)


def claims_action(text: str) -> bool:
    plain = _plain(text)
    sentences = re.split(r"(?<=[.!?\n])", plain)
    for sentence in sentences:
        if "?" in sentence or "¿" in sentence or _OFFER.search(sentence):
            continue
        if any(pattern.search(sentence) for pattern in _CLAIM_PATTERNS):
            return True
    return False


@dataclass(frozen=True)
class GuardVerdict:
    """phantom=True: la respuesta afirma una acción que no ocurrió."""

    phantom: bool
    denial_messages: tuple[str, ...]
    succeeded: bool


def evaluate(answer: str, tool_records: list[dict[str, Any]]) -> GuardVerdict:
    results = [
        record.get("result")
        for record in tool_records
        if isinstance(record, dict)
    ]
    results = [r for r in results if isinstance(r, dict)]

    succeeded = any(r.get("ok") is True for r in results)
    denials = tuple(
        str(r.get("message") or "Tu rol no tiene autorización para esta acción.")
        for r in results
        if r.get("error") == "permission_denied"
    )

    return GuardVerdict(
        phantom=claims_action(answer) and not succeeded,
        denial_messages=denials,
        succeeded=succeeded,
    )


RETRY_INSTRUCTION = (
    "VERIFICACIÓN DEL SISTEMA: tu respuesta anterior afirma una acción que "
    "NO se ejecutó (no hubo ninguna herramienta con resultado ok). Si el "
    "usuario pidió una acción, llama ahora a la herramienta correspondiente. "
    "Si no existe una herramienta para eso, dilo claramente y no afirmes "
    "haberlo hecho."
)

NOT_EXECUTED_MESSAGE = (
    "No realicé esa acción: no se ejecutó ninguna operación en el sistema. "
    "Puedo intentarlo de nuevo con más detalles o puedes hacerlo desde el "
    "módulo correspondiente."
)


def honest_answer(verdict: GuardVerdict) -> str:
    """Respuesta sustituta cuando la del modelo no es veraz."""
    if verdict.denial_messages:
        unique = list(dict.fromkeys(verdict.denial_messages))
        return "No puedo hacerlo con tu usuario. " + " ".join(unique)
    return NOT_EXECUTED_MESSAGE
