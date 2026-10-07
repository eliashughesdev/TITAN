# ADR-004 - Ollama como AI Runtime Local

## Estado

Accepted.

## Decisión

Ollama será el runtime principal de IA on-premise.

## Razón

Permite procesamiento local y control corporativo sobre los
modelos y sus datos.

## Consecuencias

TitanMDM implementará una capa de orquestación que controle
contexto, herramientas, permisos, confirmación y auditoría.

Los modelos no tendrán acceso directo a infraestructura
crítica.
