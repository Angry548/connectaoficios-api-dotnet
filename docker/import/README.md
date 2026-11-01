# Importar datos existentes a Docker

Procedimiento preparado, no ejecutado. Solo destino local SQL `127.0.0.1:1433`,
API `127.0.0.1:8080`. No conectar scripts de escritura al origen ni a Somee.
EF Core conserva la responsabilidad exclusiva del esquema. No importar un BACPAC
sobre esta base: incluye esquema. No ejecutar el SQL historico InitialIdentitySchema.sql.
No tocar migraciones, DbContext ni `__EFMigrationsHistory`.

## Esquema auditado y orden

Fuentes: `InitialIdentitySchema`, `AddPasswordResetFields`, configuraciones y entidades
actuales. No se ha inspeccionado el esquema de la base origen: verificar compatibilidad
antes de exportar. Si contiene tablas/columnas adicionales, detenerse y evaluar su propietario.

| Orden | Tabla | Identity | Dependencia |
| --- | --- | --- | --- |
| 1 | dbo.Roles | Id int (1,1) | Ninguna |
| 2 | dbo.Usuarios | Id int (1,1) | RolId -> Roles.Id, FK_Usuarios_Roles_RolId, DELETE RESTRICT |

No existe tabla Administradores ni Estados: administradores = Usuarios con RolId 3 o 4;
Estado = 1 Activo, 2 Inactivo. Roles iniciales de migrations: 1 Cliente, 2 Trabajador,
3 Administrador, 4 AdministradorPrincipal. Roles tiene Nombre unico y Usuarios Correo unico,
ademas de sus PK. No hay mas tablas de negocio administradas por este DbContext.
`__EFMigrationsHistory` es infraestructura y queda fuera de exportacion/importacion.

Preservar todos los IDs y RolId. Java referencia el ID de usuario en PerfilCliente,
PerfilTrabajador, PerfilAdministrador, fotografias y relaciones; regenerarlo o remapearlo
rompe esas asociaciones. Importar .NET no copia los perfiles ni las fotos de Java.

PasswordHash contiene BCrypt con salt incorporado: no existe columna Salt separada.
Copiar literalmente el hash, estado, rol, fechas y campos de recuperacion. No rehashear,
resetear ni convertir passwords a texto. Los archivos exportados contienen credenciales
hasheadas y datos personales: almacenamiento privado, sin sincronizacion publica ni Git.

## Estrategias y recomendacion

**A (recomendada): destino sin usuarios de testing, con esquema existente.** Primero
inventariar y respaldar datos locales. Con API parada, revisar expresamente los IDs de
usuarios desechables y las referencias Java antes de autorizar cualquier limpieza.
No basta con reconocer un correo: confirmar que la fila es realmente descartable.
Esta preparacion NO incluye ni ejecuta DELETE/TRUNCATE. Una futura limpieza debe ser
transaccional y limitada a IDs revisados, hijos antes que padres, sin tocar historial EF.
Conservar los cuatro Roles de migrations y comprobar que coincidan exactamente con origen.
Una base recien migrada ya contiene roles; no tratarla como completamente vacia.
Si hay datos locales utiles, no aplicar A sin decidir como conservarlos.

**B: importacion idempotente estricta.** El template inserta IDs ausentes y omite filas
ya identicas. Un mismo ID con contenido diferente o mismo correo/nombre con otro ID
aborta toda la transaccion, sin sobrescribir ni remapear. Ignorar ciegamente esos conflictos
podria asociar perfiles Java a otra persona. Resolverlos antes de reintentar.
Tras login, UltimoAcceso cambia: una reimportacion del mismo snapshot detectara diferencia;
esto es intencional y requiere revision, no actualizacion automatica.

LocalTestSeed crea cuatro usuarios por correo si no existen:
`cliente.test@connectaoficios.local`, `trabajador.test@connectaoficios.local`,
`admin.test@connectaoficios.local`, `adminprincipal.test@connectaoficios.local`.
Sus IDs son autogenerados: pueden ocupar los IDs del origen aunque el correo sea distinto.
BootstrapAdmin tambien puede crear un principal. Antes de importar, editar el `.env` local:
`LocalTesting__SeedEnabled=false` y dejar BootstrapAdmin__Nombre/Correo/Password vacios.
No imprimir `.env` ni `docker inspect` completo. Reiniciar un contenedor no aplica cambios
de variables: al terminar se recreara la API con Compose.

## Preparar el archivo privado (sin ejecutar contra origen)

1. Obtener una exportacion consistente de SOLO dbo.Roles y dbo.Usuarios, mediante acceso
   de lectura al origen (SSMS Generate Scripts, Advanced: Types of data to script = Data only).
   Coordinar snapshot o pausa de escrituras para que roles/usuarios correspondan al mismo momento.
2. Copiar `import-template.sql` a `datos-local.sql` dentro de esta carpeta ignorada.
3. Adaptar los INSERT exportados para cargar `#Roles` y `#Usuarios` en el bloque indicado.
   Incluir columnas explicitas, IDs originales y todos los campos del template. No insertar
   directamente en dbo desde el bloque de carga. Si el origen carece de campos reset, revisar
   compatibilidad y usar NULL solo para esos campos ausentes, sin cambiar PasswordHash.
4. Eliminar del export USE, GO, CREATE/ALTER/DROP, IDENTITY_INSERT y comandos sqlcmd
   como :connect, :r, :!!. Revisar el archivo completo; no ejecutar un export sin revisar.
   El template solo crea tablas temporales de transporte, no un segundo esquema persistente.
5. Comparar inventario de origen/destino (conteos y IDs/correos/roles/estados). Resolver
   conflictos antes de escribir. Mantener API y otros escritores detenidos durante importacion.

El template usa transaccion, XACT_ABORT, locks, INSERT con ID explicito e IDENTITY_INSERT
ON/OFF secuencial para cada tabla. Si falla revierte las inserciones y apaga IDENTITY_INSERT.
No deshabilita FK ni indices. No necesita reseed automatico: insertar IDs explicitos mayores
actualiza el contador Identity; comprobarlo despues. No reducir contadores a ciegas.

## Docker y sqlcmd (PowerShell)

La ruta configurada por el healthcheck de compose.yaml es `/opt/mssql-tools18/bin/sqlcmd`.
**No verificada en vivo en esta sesion:** Docker no esta disponible en PATH de la consola
usada para preparar estos archivos. Detectar la ruta real en tu imagen antes de importar:

```powershell
$sqlcmdPath = docker exec connectaoficios-sqlserver sh -c 'if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then echo /opt/mssql-tools18/bin/sqlcmd; elif [ -x /opt/mssql-tools/bin/sqlcmd ]; then echo /opt/mssql-tools/bin/sqlcmd; else command -v sqlcmd; fi'
if ($LASTEXITCODE -ne 0 -or -not $sqlcmdPath) { throw 'sqlcmd no encontrado' }
$sqlcmdPath = $sqlcmdPath.Trim()
$sqlcmdPath
# Lectura solamente; utiliza la contraseña LOCAL ya presente en el entorno del contenedor.
docker exec connectaoficios-sqlserver sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec "$1" -S localhost -U sa -C -b -x -d ConnectaOficiosDb -Q "SELECT DB_NAME() AS Base, @@SERVERNAME AS Servidor;"' sh $sqlcmdPath
if ($LASTEXITCODE -ne 0) { throw 'Verificacion SQL fallo' }
```

Revisar destino y archivo antes de los siguientes comandos, que **si importan**:

```powershell
docker compose stop connectaoficios-api
if ($LASTEXITCODE -ne 0) { throw 'No se pudo parar API' }
docker cp .\docker\import\datos-local.sql connectaoficios-sqlserver:/tmp/import.sql
if ($LASTEXITCODE -ne 0) { throw 'Copia fallo' }
docker exec connectaoficios-sqlserver sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec "$1" -S localhost -U sa -C -b -x -d ConnectaOficiosDb -i /tmp/import.sql' sh $sqlcmdPath
if ($LASTEXITCODE -ne 0) { throw 'Importacion fallo; revisar antes de continuar' }
```

No pasar password con -P ni escribirlo en archivos versionados. SQLCMDPASSWORD se establece
solo dentro del proceso; -b propaga errores y -x evita expansion de variables del export.
-C acepta el certificado del SQL local. No usar `docker compose down -v` ni borrar volumen.
Referencia: [sqlcmd de Microsoft](https://learn.microsoft.com/en-us/sql/tools/sqlcmd-utility).

## Validacion posterior (solo lectura, sin hashes)

Ejecutar estas consultas con SSMS contra `127.0.0.1:1433`, base ConnectaOficiosDb, o guardar
un `validacion.sql` privado y usar los comandos docker cp/sqlcmd anteriores cambiando el archivo.
No compartir el inventario con correos reales sin sanearlo.

```sql
SELECT COUNT_BIG(*) AS Usuarios FROM dbo.Usuarios;
SELECT COUNT_BIG(*) AS Roles FROM dbo.Roles;
SELECT COUNT_BIG(*) AS Administradores FROM dbo.Usuarios WHERE RolId IN (3,4);
SELECT Id, Nombre, Descripcion, Activo FROM dbo.Roles ORDER BY Id;
SELECT u.Id, u.Correo, u.RolId, r.Nombre AS Rol, u.Estado,
       CASE u.Estado WHEN 1 THEN 'Activo' WHEN 2 THEN 'Inactivo' ELSE 'Desconocido' END AS EstadoNombre
FROM dbo.Usuarios u JOIN dbo.Roles r ON r.Id=u.RolId ORDER BY u.Id;
SELECT RolId, Estado, COUNT_BIG(*) AS Cantidad FROM dbo.Usuarios GROUP BY RolId, Estado;
SELECT MIN(Id) AS MinId, MAX(Id) AS MaxId, IDENT_CURRENT('dbo.Usuarios') AS IdentityActual FROM dbo.Usuarios;
SELECT MIN(Id) AS MinId, MAX(Id) AS MaxId, IDENT_CURRENT('dbo.Roles') AS IdentityActual FROM dbo.Roles;
SELECT Id FROM dbo.Usuarios u WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Id=u.RolId);
SELECT Correo, COUNT(*) AS Cantidad FROM dbo.Usuarios GROUP BY Correo HAVING COUNT(*) > 1;
SELECT Nombre, COUNT(*) AS Cantidad FROM dbo.Roles GROUP BY Nombre HAVING COUNT(*) > 1;
SELECT Id, Estado FROM dbo.Usuarios WHERE Estado NOT IN (1,2);
DBCC CHECKCONSTRAINTS ('dbo.Usuarios') WITH ALL_CONSTRAINTS;
SELECT name, is_disabled, is_not_trusted FROM sys.foreign_keys
WHERE parent_object_id=OBJECT_ID('dbo.Usuarios');
SELECT OBJECT_NAME(object_id) AS Tabla, name AS Columna, seed_value, increment_value, last_value
FROM sys.identity_columns WHERE object_id IN (OBJECT_ID('dbo.Roles'),OBJECT_ID('dbo.Usuarios'));
```

Comparar conteos e inventario con snapshot origen: con estrategia B puede haber filas locales
adicionales. Confirmar mismos IDs por correo, roles y estados; comprobar igualdad exacta de
hashes en comparacion privada (el template ya valida duplicados), nunca listarlos en reportes.
FK debe quedar enabled y trusted (0/0); consultas de huerfanos/duplicados/estados desconocidos
no deben devolver filas. Si aparece una constraint deshabilitada, detener el flujo y revisar;
para recuperarla solo tras corregir violaciones: `ALTER TABLE dbo.Usuarios WITH CHECK CHECK
CONSTRAINT FK_Usuarios_Roles_RolId;`. No ejecutar NOCHECK como atajo.

## Login local posterior

Con importacion y validacion correctas y seed/bootstrap desactivados en `.env`:

```powershell
docker compose up -d --no-deps --force-recreate connectaoficios-api
if ($LASTEXITCODE -ne 0) { throw 'Arranque API fallo' }
docker compose ps
Invoke-RestMethod http://127.0.0.1:8080/health
# Esperar healthy antes de continuar. Usar una cuenta importada activa y su password conocido.
$correo = Read-Host 'Correo de cuenta importada'
$secreto = Read-Host 'Password existente' -AsSecureString
$credencial = [pscredential]::new($correo, $secreto)
try {
    $body = @{ correo=$correo; password=$credencial.GetNetworkCredential().Password } | ConvertTo-Json -Compress
    $login = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8080/api/users/login' -ContentType 'application/json' -Body $body
    if (-not $login.token) { throw 'Login sin token' }
    [pscustomobject]@{ Id=$login.usuario.id; RolId=$login.usuario.rolId; Estado=$login.usuario.estado; TokenRecibido=$true }
} finally {
    $body=$null; $login=$null; $credencial=$null; $secreto=$null
}
```

No imprimir respuesta completa ni token. Comparar Id/RolId con el inventario esperado.
401: revisar password conocido, hash BCrypt y Estado=1; no resetear password para forzar exito.
Login actualiza UltimoAcceso. No se ha ejecutado este login: aun no se han importado cuentas.
Despues de verificar, retirar `/tmp/import.sql` del contenedor y gestionar la retencion privada
del export; nunca versionar datos. `.gitignore` no protege archivos ya rastreados ni cifrados.

## Migracion local ejecutada

La copia local se realizo DATA ONLY, con transaccion y comparacion exacta de todos los
campos de Usuarios antes del COMMIT. Se conservaron los IDs originales, RolId, estados
y hashes, sin modificar Roles ni esquema. Los IDs de .NET Docker quedan alineados con
los del origen utilizados por perfiles, fotografias y relaciones Java. Java no fue
modificado; esto no constituye una prueba end-to-end de sus perfiles o fotografias.
Los respaldos, snapshots y auxiliar de ejecucion permanecen privados e ignorados.
La validacion SQL termino correctamente. El login con una cuenta importada queda pendiente:
no hay una contraseña original conocida disponible en configuracion que corresponda a un
AdministradorPrincipal importado. No se han reseteado credenciales. El operador puede ejecutar
el auxiliar privado `docker/import/Test-ImportedLogin.ps1`, que solicita la contraseña mediante
Read-Host -AsSecureString y solo muestra HTTP, existencia de token, ID y rol. No enviar passwords
por chat. El Portal responde HTTP 200 en localhost:5173; su login aun no fue verificado.
