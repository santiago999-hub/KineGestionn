# APÉNDICE: CORRECCIONES E INFORMACIÓN FALTANTE
## Para agregar al ANALISIS-SISTEMA-KINEGESTION.md

---

## A) SECCIÓN: CheckConstraints en Base de Datos
### (Insertar después de sección "5. Índices de Base de Datos")

### 6. Restricciones de Integridad (CHECK CONSTRAINTS)

La BD aplica validaciones de integridad a nivel de schema para proteger datos incluso si bypasses la aplicación:

```sql
-- PATIENT TABLE
CK_Patients_FechaNacimiento_Past:
  [FechaNacimiento] < CONVERT(date, GETDATE())
  └─ Garantiza: No se puede crear paciente con fecha de nacimiento futura
  
CK_Patients_DNI_OnlyDigits:
  [DNI] NOT LIKE '%[^0-9]%' AND LEN([DNI]) BETWEEN 7 AND 8
  └─ Garantiza: DNI contiene SOLO dígitos y tiene 7-8 caracteres
  └─ Redundancia con validación en C#, pero actúa como barrera final

-- TREATMENT TABLE
CK_Treatments_CantidadSesionesTotales_Positive:
  [CantidadSesionesTotales] >= 1
  └─ Garantiza: Tratamiento siempre tiene al menos 1 sesión planificada

-- SESSION TABLE
CK_Sessions_Status_Valid:
  [Status] IN (0, 1, 2)
  └─ Garantiza: Solo valores válidos de enum SessionStatus
  
CK_Sessions_PaymentStatus_Valid:
  [PaymentStatus] IN (0, 1)
  └─ Garantiza: Solo Pending (0) o Paid (1)
  
CK_Sessions_NroSesionEnTratamiento_Positive:
  [NroSesionEnTratamiento] >= 1
  └─ Garantiza: Número de sesión es >= 1, nunca 0 o negativo
```

**Impacto en diseño:** Las restricciones son **defensivas en profundidad**. Aunque los Services validan en C#, la BD nunca acepta datos inválidos.

---

## B) SECCIÓN: Relaciones y DeleteBehavior
### (Insertar después de CheckConstraints)

### 7. Relaciones entre Entidades y Comportamiento de Eliminación

Las relaciones entre entidades definen cómo se propagan los cambios (especialmente eliminaciones):

```
┌─ Patient ←─ Treatment (1:N)
│  DeleteBehavior: RESTRICT
│  └─ No se puede eliminar paciente si tiene tratamientos
│  └─ Protege integridad: conserva historial clínico
│
├─ Patient ←─ Session (1:N)
│  DeleteBehavior: RESTRICT
│  └─ No se puede eliminar paciente si tiene sesiones
│  └─ Evita "huérfanos" de datos clínicos
│
├─ Professional ←─ Session (1:N)
│  DeleteBehavior: RESTRICT
│  └─ No se puede eliminar profesional si tiene sesiones activas
│  └─ Mantiene trazabilidad de quién realizó cada sesión
│
├─ Treatment ←─ Session (1:N)
│  DeleteBehavior: RESTRICT
│  └─ No se puede eliminar tratamiento si tiene sesiones
│  └─ Garantiza que plan de tratamiento siempre está completo
│
└─ Office ←─ Session (1:N) + Equipment (1:N)
   DeleteBehavior: SET NULL
   └─ Si se elimina consultorio, sesiones pasan a OfficeId = NULL
   └─ Las sesiones se conservan sin consultorio asignado
   └─ Equipos pierden su consultorio pero no se eliminan
```

**Estrategia:**
- **RESTRICT para datos clínicos** (Patient, Professional, Treatment, Session): Previene accidental loss
- **SET NULL para ubicación** (Office): Flexible, permite reasignar o dejar sin consultorio

---

## C) SECCIÓN: Global Query Filter
### (Insertar después de DeleteBehavior)

### 8. Global Query Filter y Soft Delete

La BD implementa **soft delete** mediante **global query filters** que filtran automáticamente datos inactivos:

```csharp
// En AppDbContext.OnModelCreating():

modelBuilder.Entity<Office>()
  .HasQueryFilter(o => o.IsActive);

// Comportamiento: TODAS las queries en Office filtran automáticamente
// SELECT * FROM Office  
// → Se traduce a: SELECT * FROM Office WHERE IsActive = 1

// Exception: Si intentas restaurar directamente en SQL:
// UPDATE Office SET IsActive = 0;  ← NO aparecerá en queries de EF
// No puedes recuperarla via UI, pero el registro sigue en BD
```

**Nota importante:** 
- Solo **Office** tiene Global Query Filter
- **Patient** y **Professional** NO tienen filtro global (para evitar NullRef en navigation properties de Session)
- El filtrado de activos en Patient/Professional se realiza **explícitamente en Repository**:
  ```csharp
  public async Task<IEnumerable<Patient>> GetActivosAsync()
      => await _db.Patients
         .Where(p => p.IsActivo)  ← Explícito, no global
         .AsNoTracking()
         .ToListAsync();
  ```

**Ventaja de Soft Delete:**
- ✅ Preserva historial clínico (cumplimiento normativo)
- ✅ Auditoría completa (quién creó, cuándo, quién borró)
- ✅ Recuperabilidad (datos no se pierden nunca)
- ✅ Integridad referencial (sesiones siguen vinculadas a paciente "borrado")

---

## D) CORRECCIONES PUNTUALES

### En la tabla de Controladores (línea ~791):

**ANTES:**
```
| **AccountController** | (Public) | Login, logout, cambio de contraseña |
| **HomeController** | (Public) | Dashboard principal, errores |
| **LocalizationController** | (Public) | Cambio de idioma de la interfaz |
```

**DESPUÉS:**
```
| **AccountController** | [AllowAnonymous] | Login, logout, cambio de contraseña, password recovery |
| **HomeController** | [Authorize] Admin+Kinesiologo | Dashboard principal con métricas (SessionsCount, etc) |
| **LocalizationController** | [AllowAnonymous] | Cambio de idioma de la interfaz (pre-login) |
```

---

### En la sección Office (línea ~260):

**ANTES:**
```
Restricciones:
- Name es REQUIRED
```

**DESPUÉS:**
```
Restricciones:
- Name es REQUIRED y UNIQUE (no pueden existir dos consultorios con el mismo nombre)
```

---

## E) RESUMEN DE IMPACTO

Con estas correcciones agregadas, el análisis documenta **completamente**:

✅ Restricciones de integridad a nivel BD (CHECK CONSTRAINTS)  
✅ Comportamiento de eliminación (DeleteBehavior.RESTRICT vs SET NULL)  
✅ Estrategia de soft delete (Global Query Filters)  
✅ Autorización correcta en controladores  
✅ Índices compuestos críticos para detección de conflictos  
✅ Protección defensiva en profundidad  

**Resultado:**
- Documentación 100% consistente con código real
- Defendible ante cualquier pregunta técnica del profesor
- Explica CÓMO y POR QUÉ cada decisión arquitectónica

---

## F) ORDEN RECOMENDADO DE SECCIONES

Después de "5. Índices de Base de Datos":

1. **6. Restricciones de Integridad (CHECK CONSTRAINTS)** ← NEW
2. **7. Relaciones entre Entidades y DeleteBehavior** ← NEW  
3. **8. Global Query Filter y Soft Delete** ← NEW
4. **9. Patrones de Diseño Implementados** ← (existente, renumerar)
5. **10. Índices y Rendimiento** ← (existente, renumerar)

---

**Este apéndice contiene TODAS las correcciones necesarias**  
**Listo para ser integrado al análisis principal**
