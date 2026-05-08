# Ronda 4 - Comparacion de aislamiento en COUNT + INSERT

Fecha: 08/05/2026

## Contexto

En SessionRepository.AddAsync se calcula NroSesionEnTratamiento con este patron:

1. COUNT(*) de sesiones por TreatmentId
2. validacion del limite del tratamiento
3. INSERT de la nueva sesion con NroSesionEnTratamiento = count + 1

La duda era si RepeatableRead alcanzaba o si habia que subir a Serializable.

## Hipotesis

- RepeatableRead iba a ser mas rapido.
- Serializable iba a ser mas correcto para este caso porque evita phantoms entre el COUNT y el INSERT.

## Escenario de prueba

Prueba reproducible contra SQL Server Express local sobre KineGestionDB:

- tabla auxiliar con indice por (TreatmentId, NroSesionEnTratamiento)
- dos sesiones concurrentes
- ambas ejecutan:
  - BEGIN TRAN
  - COUNT(*) WHERE TreatmentId = 1
  - WAITFOR DELAY '00:00:01'
  - INSERT con nro = count + 1
  - COMMIT
- 3 rondas por nivel de aislamiento

## Resultados observados

Promedio de 3 rondas:

| IsolationLevel | Tiempo promedio | Filas comprometidas | Duplicados de NroSesionEnTratamiento |
| --- | ---: | ---: | ---: |
| RepeatableRead | 1333.7 ms | 2.0 | 1.0 |
| Serializable | 2112.3 ms | 1.0 | 0.0 |

Detalle cualitativo:

- RepeatableRead fue mas rapido en la muestra, pero permitio que ambas transacciones leyeran el mismo COUNT y grabaran el mismo NroSesionEnTratamiento.
- Serializable fue mas lento porque serializa el acceso al rango leido. En una corrida inspeccionada aparte, SQL Server devolvio error 1205 (deadlock victim), por lo que no conviene usarlo sin retry.

## Conclusiones

- Si el criterio es solo velocidad bruta, RepeatableRead gana.
- Si el criterio es correccion del numero de sesion bajo concurrencia, RepeatableRead no sirve para este patron.
- Para este caso, Serializable es la opcion correcta entre las dos comparadas.
- Para hacerlo operativo en produccion, Serializable debe ir acompaniado de un retry corto para deadlocks transitorios.

## Decision aplicada

Se actualizo SessionRepository.AddAsync para:

- usar IsolationLevel.Serializable
- reintentar hasta 3 veces ante SqlException 1205
- mantener la validacion del limite y el calculo de NroSesionEnTratamiento dentro de la misma transaccion

## Recomendacion extra

Si en una fase posterior se quiere blindar la unicidad tambien a nivel esquema, conviene evaluar un indice unico por:

- Sessions(TreatmentId, NroSesionEnTratamiento)

Eso no reemplaza la logica transaccional, pero agrega una red de seguridad a nivel base de datos.
