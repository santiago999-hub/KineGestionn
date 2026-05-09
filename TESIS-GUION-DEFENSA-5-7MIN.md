# Guion de defensa oral (5 a 7 minutos)

## 0. Apertura (30-45 segundos)
Objetivo de mi trabajo: fortalecer KineGestion en trazabilidad operativa, calidad interna y experiencia de usuario, mediante mejoras de auditoria, validacion automatizada e internacionalizacion.

## 1. Problema y contexto (45-60 segundos)
Antes de las mejoras, la auditoria tenia alcance funcional limitado, no contaba con exportacion en formatos de analisis extendido y la interfaz no tenia soporte bilingue integral. Esto impactaba la capacidad de control operativo y escalabilidad de uso.

## 2. Solucion implementada (90-120 segundos)
- Se robustecio la auditoria de eventos para altas, modificaciones y bajas logicas.
- Se ampliaron filtros operativos a entidad, id, usuario, accion y rango temporal.
- Se habilitaron exportaciones en CSV y XLSX para explotacion de datos.
- Se centralizaron entidad y accion en enums compartidos para coherencia UI/servicios/export.
- Se incorporo internacionalizacion ES/EN en auditoria y layout con selector persistente de idioma.

## 3. Metodologia de validacion (45-60 segundos)
Se aplico comparacion antes/despues sobre los mismos modulos y se verifico no regresion mediante suites automatizadas de Core/Data y Web.

## 4. Resultados principales (90-120 segundos)
- Core/Data: 49/49 -> 53/53 pruebas en verde (+8.16%).
- Web: 51/51 -> 61/61 pruebas en verde (+19.61%).
- Total: 100/100 -> 114/114 pruebas en verde (+14.00%).
- Filtros de auditoria: 3 -> 6 (+100%).
- Exportaciones: 0 -> 2 formatos (CSV y XLSX).
- Idiomas funcionales: 1 (ES) -> 2 (ES, EN) (+100%).

## 5. Impacto y conclusiones (45-60 segundos)
Los resultados confirman mejora concreta en robustez, trazabilidad y usabilidad. El sistema queda mejor preparado para evolucionar con menor riesgo tecnico y mayor capacidad de analisis operativo.

## 6. Cierre y trabajo futuro (30-45 segundos)
Como continuidad, propongo incorporar benchmark de rendimiento con p50/p95 para consultas y exportaciones de gran volumen, y completar cobertura formal por lineas/ramas.

## Preguntas esperables del jurado y respuesta breve
1. Como aseguras que no hubo regresiones?
Respuesta: con ejecucion de suites automatizadas al cierre (114/114 en verde) y pruebas especificas del bloque incorporado.

2. Por que CSV y XLSX?
Respuesta: CSV ofrece interoperabilidad simple y XLSX mejora consumo operativo por usuarios no tecnicos.

3. Que aporte academico diferencial muestra el trabajo?
Respuesta: integra mejora arquitectonica (enums y contratos), mejora operativa (auditoria/export) y mejora de UX (i18n) con evidencia cuantitativa reproducible.
