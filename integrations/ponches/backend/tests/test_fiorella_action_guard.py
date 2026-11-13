"""Verificación de acciones de Fiorella y negativas por permisos (RBAC)."""

from __future__ import annotations

import pytest

from app.services.fiorella_action_guard import (
    NOT_EXECUTED_MESSAGE,
    claims_action,
    evaluate,
    honest_answer,
)


@pytest.mark.parametrize(
    "text",
    [
        "Listo, exporté los ponches de hoy a Excel.",
        "He generado el reporte de asistencia.",
        "Ya sincronicé el colaborador con los relojes.",
        "El archivo está listo para descargar.",
        "Hecho. Eliminé al colaborador del reloj Almacén.",
    ],
)
def test_detecta_afirmaciones_de_acciones(text: str) -> None:
    assert claims_action(text)


@pytest.mark.parametrize(
    "text",
    [
        "Hay 22 relojes en línea y 8 fuera de línea.",
        "¿Quieres que exporte los ponches de hoy?",
        "Puedo generar el reporte si me indicas las fechas.",
        "No encontré colaboradores con ese nombre.",
        "Para exportar necesitas el permiso de exportación.",
    ],
)
def test_consultas_y_ofrecimientos_no_cuentan_como_accion(text: str) -> None:
    assert not claims_action(text)


def test_accion_sin_ninguna_herramienta_es_fantasma() -> None:
    verdict = evaluate("Listo, exporté el archivo.", [])
    assert verdict.phantom
    assert honest_answer(verdict) == NOT_EXECUTED_MESSAGE


def test_accion_respaldada_por_herramienta_exitosa_es_valida() -> None:
    records = [{"tool": "export_punches_excel", "result": {"ok": True, "action": {"type": "download"}}}]
    assert not evaluate("Listo, exporté el archivo.", records).phantom


def test_negativa_por_permisos_se_explica_al_usuario() -> None:
    records = [
        {
            "tool": "export_punches_excel",
            "result": {
                "ok": False,
                "error": "permission_denied",
                "permission": "reports.export",
                "message": "Tu rol no tiene autorización para exportar reportes a Excel.",
            },
        }
    ]
    verdict = evaluate("He exportado el reporte.", records)
    assert verdict.phantom
    answer = honest_answer(verdict)
    assert "no tiene autorización para exportar" in answer
    assert answer.startswith("No puedo hacerlo con tu usuario.")


def test_herramientas_respetan_operaciones_delegadas() -> None:
    from app.services import fiorella_tools

    limited = {"source": "titan", "role": "delegate", "operations": ["attendance.read"]}
    result = fiorella_tools.tool_device_health(limited)
    assert result["ok"] is False
    assert result["error"] == "permission_denied"
    assert result["permission"] == "devices.read"
    assert "relojes" in result["message"]


def test_exportar_exige_permiso_propio_aunque_pueda_consultar() -> None:
    from app.services.fiorella_tools_modules.export_tools import export_punches_excel

    reader = {"source": "titan", "role": "delegate", "operations": ["attendance.read"]}
    result = export_punches_excel(reader)
    assert result["error"] == "permission_denied"
    assert result["permission"] == "reports.export"
