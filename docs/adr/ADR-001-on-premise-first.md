# ADR-001 - On-Premise First

## Estado

Accepted.

## Decisión

TitanMDM tendrá una arquitectura On-Premise First.

## Razón

La organización requiere control local de infraestructura,
dispositivos, SQL Server y operaciones TI.

## Consecuencias

La solución no puede depender obligatoriamente de Azure.

Sin embargo las abstracciones deberán mantener compatibilidad
con despliegues híbridos futuros.
