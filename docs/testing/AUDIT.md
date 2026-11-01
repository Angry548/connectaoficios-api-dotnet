# Auditoría Backend .NET

Inventario antes de modificaciones: rama main, solución .slnx con API/Domain net8.0
y un proyecto Tests net8.0. Sin repositorios separados: UserServices y AdminServices
consultan ApplicationDbContext directamente. Minimal APIs para usuarios, administración,
cuentas administrativas y estadísticas; controlador WeatherForecast de plantilla.
AutoMapper 16.2, BCrypt 4.2, EF SQL Server/JWT 8.0.31, Swashbuckle 10.2, Resend 0.18.
xUnit 2.5.3, Moq 4.20.72, Test SDK 17.8 y Coverlet collector 6.0 existentes.
No se cambió framework de pruebas ni hashing. No había infraestructura Postman/Newman,
k6 ni ZAP. Existía generador PowerShell de HTML desde TRX (sin seguimiento).

Dockerfile, Compose, README, .env.example, .dockerignore, .gitattributes y docker/
ya estaban sin seguimiento al iniciar. Se conservaron y ampliaron; no atribuir su
creación original a esta auditoría. `.env` existía e ignorado: no se sobrescribió.
No había un reporte final versionado utilizable como evidencia de esta ejecución.

## Matriz Jira y pruebas reales

Estado inicial frente a casos solicitados; final indica evidencia de la suite actual.

| Caso | Inicial | Evidencia final |
| --- | --- | --- |
| Generar JWT válido y firmado | YA EXISTE | JwtServiceTests original |
| JWT identificador | YA EXISTE | sub y NameIdentifier en test original |
| JWT rol | YA EXISTE | ClaimTypes.Role en test original; roles en HTTP |
| JWT issuer/audience | YA EXISTE | Validación criptográfica original |
| Login password incorrecta | NO EXISTE | UsuarioServiceTests theory |
| Login cuenta inactiva | NO EXISTE | UsuarioServiceTests + HTTP |
| Registro válido | NO EXISTE | UsuarioServiceTests |
| Correo duplicado | NO EXISTE | UsuarioServiceTests + Newman |
| Registrar Cliente | NO EXISTE | UsuarioServiceTests theory rol 1 |
| Registrar Trabajador | NO EXISTE | UsuarioServiceTests theory rol 2 |
| Campos obligatorios inválidos | NO EXISTE | ApiSecurityTests HTTP + Newman |
| Crear Administrador | NO EXISTE | AdministradorServiceTests |
| Actualizar Administrador | NO EXISTE | AdministradorServiceTests |
| Desactivar Administrador | NO EXISTE | AdministradorServiceTests |
| Eliminar Administrador | NO EXISTE | AdministradorServiceTests |
| Último Principal no desactivar/eliminar | NO EXISTE | AdministradorServiceTests |
| Consultar usuario | NO EXISTE | UsuarioServiceTests |
| Usuario inexistente | NO EXISTE | UsuarioServiceTests |
| Activar usuario | NO EXISTE | UsuarioServiceTests theory estado 1 |
| Desactivar usuario | NO EXISTE | UsuarioServiceTests theory estado 2 |
| Buscar/filtrar | NO EXISTE | UsuarioServiceTests SQLite LIKE y paginación |
| Cuenta autenticada | NO EXISTE | ApiSecurityTests /me |
| Actualizar datos personales | NO EXISTE | CuentaServiceTests |
| Cambiar contraseña | NO EXISTE | CuentaServiceTests |
| Contraseña actual incorrecta | NO EXISTE | CuentaServiceTests theory |
| Restablecer contraseña | NO EXISTE | CuentaServiceTests, hash y token de un solo uso |

Sin duplicación del test JWT original. Suite añadida por módulos, no por personas.
Las theories cuentan como varios casos de ejecución. Los tests de servicios con
SQLite son pruebas de componente con DB aislada; HTTP utiliza WebApplicationFactory.
No describir toda la suite como unitarias puras.

## Correcciones

- Validación DataAnnotations común: Minimal APIs net8 no la ejecutaban. Registro
  también valida al invocar directamente el servicio.
- Defaults 20 → 10 en endpoints/interfaces/servicios; forma PagedResult conservada.
- /me GET aditivo. DTOs ya tenían identidad suficiente: no se agregaron fotos.
- JWT: rechazo de cuentas eliminadas/inactivas y roles obsoletos por petición .NET.
- Swagger solo Development/Docker; no expuesto automáticamente en Production.
- Configuración sensible retirada de appsettings versionables. Si era vigente debe
  rotarse; no se editó el historial Git ni se contactaron servicios remotos.
- Eliminación/desactivación administrativa usa transacción Serializable: la
  comprobación de último Principal y escritura quedan dentro de una transacción.
  SQL puede abortar una operación por deadlock al competir; no se elimina la guarda.
- Seed solo Docker SQL local, sin sobrescribir cuentas; correo local sin Resend.

## Cobertura pendiente de ampliar por integrantes

Los casos Jira principales tienen evidencia base. Sigue siendo PARCIAL la cobertura
exhaustiva de ramas: expiración y configuración inválida JWT, campos máximos, correos
inválidos en cada actualización, todos los errores de cuenta/admin, tokens reset
expirados, envíos fallidos, estadísticas, concurrencia SQL de Principales, paginación
extrema y collation SQL Server. No interpretar PASS como cobertura total de seguridad.
La concurrencia y semántica SQL requieren SQL Server real; SQLite no las certifica.

## Endpoints auditados

- Públicos: POST /api/users/register, /login, /password/forgot, /password/reset;
  GET /health y /WeatherForecast (cinco datos sintéticos).
- Identidad autenticada: GET /api/users/me y /api/users/{id}; PUT /api/users/me
  (Cliente/Trabajador), PUT /api/users/me/password.
- Administración: GET /api/admin/users, /users/search, /users/{id};
  PATCH /api/admin/users/{id}/status; GET /api/admin/statistics/users.
- Principal: GET/POST /api/admin/accounts/, GET /search, GET/PUT/DELETE /{id},
  PATCH /{id}/status.

Los dos listados de usuarios son paginados. Las dos búsquedas son arrays acotados,
sin metadatos de página por compatibilidad de autocompletado. Estadísticas son
agregados; ningún otro listado de usuarios creciente fue encontrado.

## Evidencias

Ver VALIDATION.md para resultados medidos y archivos generados. Reportes y tokens
temporales no se versionan. README documenta reproducción Windows/Linux/Codespaces,
flujo de integrantes y secuencia de informes del líder.
