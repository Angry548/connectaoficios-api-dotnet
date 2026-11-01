# SOLO ENTORNO LOCAL

Seed optativo: `LocalTesting__SeedEnabled=true`, entorno `Docker`, servidor
`sqlserver,1433`, base `ConnectaOficiosDb`. Nunca se ejecuta contra otro destino.

| Correo | Rol | ID de rol |
| --- | --- | --- |
| cliente.test@connectaoficios.local | Cliente | 1 |
| trabajador.test@connectaoficios.local | Trabajador | 2 |
| admin.test@connectaoficios.local | Administrador | 3 |
| adminprincipal.test@connectaoficios.local | AdministradorPrincipal | 4 |

Password de ejemplo **SOLO ENTORNO LOCAL**: `LocalOnly-Connecta-2026!`.
Configurable mediante `LocalTesting__Password`. Newman y k6 deben usar el mismo valor.
El seed crea cuentas faltantes; no modifica contraseña, rol ni estado de cuentas
existentes. Los IDs de usuario son asignados por SQL: no confundir con IDs de rol.

Recuperación sin Resend: `LocalTesting__EmailEnabled=true` guarda correos y códigos
en `/tmp/connectaoficios-mail` dentro del contenedor. Inspeccionar únicamente allí:
`docker compose exec connectaoficios-api sh -c 'cat /tmp/connectaoficios-mail/*.json'`.
No compartir esa salida ni incluirla en evidencias. Se pierde al recrear el contenedor.
No existe endpoint HTTP para leer códigos. Deshabilitado fuera de Docker.
