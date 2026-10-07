# ADR-002 - Modular Monolith

## Estado

Accepted.

## Decisión

El backend principal continuará como modular monolith.

## Razón

Permite mantener consistencia transaccional y velocidad de
desarrollo sin introducir complejidad operacional prematura.

## Consecuencias

Los módulos deben mantener límites claros.

Los servicios externos como Ponches pueden permanecer
separados cuando exista una justificación tecnológica.
