# Contratos .NET y Java

.NET es fuente de identidad y acceso; Java administra perfiles y fotografías.
No hay FotoUsuario, fotoUrl, PerfilAdministrador ni Cloudinary en .NET.

`UserResponse`: id, nombre, correo, telefono, rolId, rol, estado, fechaCreacion.
`UserSearchResponse`: id, nombre, correo, rolId, rol; solo cuentas activas.
`LoginResponse`: token y usuario (UserResponse). `GET /api/users/me` devuelve
UserResponse para los cuatro roles. El PUT existente de cuenta conserva sus permisos
Cliente/Trabajador. No se modificó Portal ni Mobile.

Portal puede reunir ID+rol de `items` y resolver fotos por batch Java; no hacer una
petición por fila. La ruta conceptual `/api/perfiles/fotos` debe verificarse en el
repositorio Java antes de implementar Portal. No se dispone de ese contrato aquí.

JWT firmado HS256 con bytes UTF-8 de Jwt:Key (sin decodificar base64):

| Claim en payload | Valor |
| --- | --- |
| sub | ID numérico de usuario como string |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier | El mismo ID |
| http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name | Nombre |
| email | Correo |
| http://schemas.microsoft.com/ws/2008/06/identity/claims/role | Rol exacto |
| jti | UUID distinto por emisión |
| iss | Jwt:Issuer |
| aud | Jwt:Audience |
| exp | Unix timestamp UTC; duración configurada, fallback 120 minutos |

No se emiten claims cortos `role` ni `roles`. .NET aplica mapeo inbound estándar,
autorización por ClaimTypes.Role, firma/issuer/audience/lifetime y ClockSkew cero.
También comprueba que la cuenta continúe activa y conserve el rol del token.
Java debe compartir clave LOCAL, algoritmo, issuer y audience y extraer el rol URI.
La desactivación se comprueba en .NET por petición; no revoca automáticamente un JWT
ya emitido en Java. La revocación entre APIs requiere un contrato posterior.

Listados crecientes existentes:

| Endpoint | Autorización | Resultado |
| --- | --- | --- |
| GET /api/admin/users | Administrador o AdministradorPrincipal | Cliente/Trabajador paginados |
| GET /api/admin/accounts/ | AdministradorPrincipal | Administradores paginados |

Filtros: nombre, correo, rolId, estado (1 Activo, 2 Inactivo), page (default 1),
pageSize (default 10, rango 1..100 para conservar compatibilidad).
Respuesta existente: items, page, pageSize, totalItems, totalPages,
hasPreviousPage, hasNextPage. No se renombra totalItems a total.
Cambiar filtros permite solicitar page=1. Orden estable por nombre y luego ID.

No paginados conservados: `/api/admin/users/search` y
`/api/admin/accounts/search`: autocompletado con texto mínimo 2 y limit default 10,
máximo 20; devuelven arrays acotados. Para búsquedas completas usar los listados
paginados con filtros. `/WeatherForecast` devuelve cinco elementos sintéticos;
`/api/admin/statistics/users` devuelve agregados, no filas de usuarios.

Integración Java local: levantar ambos servicios, igualar configuración JWT,
ejecutar `pwsh -File tests/integration/dotnet-java.ps1 -ProtectedPath /ruta/real`.
La ruta Java se exige explícitamente para no inventar un endpoint de identidad.
Verificar en Java ID y rol para los cuatro usuarios, acceso permitido y 403 por rol,
y 401 para firma alterada. Este script comprueba aceptación/rechazo HTTP; hace falta
una respuesta Java que exponga el principal para comprobar identidad semántica.
