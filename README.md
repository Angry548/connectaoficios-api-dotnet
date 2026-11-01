# ConnectaOficios — Backend .NET

API y Domain .NET 8; solución `ConnectaOficios.slnx`; pruebas xUnit + Moq + Coverlet.
.NET administra identidad, usuarios, roles y JWT. Java administra perfiles, fotos y
negocio. Portal y Mobile consumen ambos. [Contratos](docs/testing/CONTRACTS.md).

## Docker local y SQL Server

Requisitos: Docker con contenedores Linux x64, Compose y memoria suficiente para SQL
Server 2022 Developer (mínimo 2 GB para SQL, adicional para API/build).

PowerShell:
```powershell
Copy-Item .env.example .env
# Editar contraseña SQL y clave JWT exclusivamente locales.
docker compose config --quiet
docker compose up --build -d
docker compose ps
```
Linux / Codespaces:
```bash
cp .env.example .env
# Editar MSSQL_SA_PASSWORD y Jwt__Key antes de continuar.
docker compose config --quiet
docker compose up --build -d
docker compose ps
curl --fail http://localhost:8080/health
```

`.env` está ignorado. Nunca copiar secretos productivos. SQL exige contraseña robusta;
JWT requiere al menos 32 bytes. Los ejemplos LocalTesting son SOLO ENTORNO LOCAL.
No ejecutar `docker compose config` sin `--quiet` al compartir evidencias: expone secretos.
No sobrescribir un `.env` existente. Appsettings versionables no contienen secretos;
fuera de Docker usar variables `ConnectionStrings__DefaultConnection`, `Jwt__Key`,
`Jwt__Issuer`, `Jwt__Audience` (o user-secrets en Development).

Red Compose `backend`: API → `sqlserver,1433`, base `ConnectaOficiosDb`.
SQL y API publicados solo en loopback; SQLSERVER_PORT default 1433. Cambiarlo a 14330
si está ocupado. API http://localhost:8080, Swagger http://localhost:8080/swagger,
OpenAPI http://localhost:8080/swagger/v1/swagger.json. Swagger solo Development/Docker.
Health `/health` comprueba proceso HTTP; el healthcheck SQL ejecuta SELECT 1.

Dockerfile multi-stage publica API y `docker/DatabaseInit`; runtime no root (`app`).
El entrypoint aplica las dos migraciones EF existentes antes del arranque. El auxiliar
rechaza conexiones distintas de sqlserver,1433 / ConnectaOficiosDb / sa y diferencias
modelo/snapshot. No usa EnsureCreated ni un SQL paralelo. `InitialIdentitySchema.sql`
es histórico, no ejecutarlo manualmente. Volumen persistente `connectaoficios-sqlserver-data`.
Cambiar password en `.env` no cambia la almacenada en un volumen existente.

```bash
docker compose logs --tail=100
docker compose stop
docker compose start
docker compose down
# down conserva el volumen; NO usar down -v si se necesitan sus datos.
```

Seed optativo de cuatro roles, idempotente y restringido a Docker SQL local; buzón de
correo local para probar recuperación sin Resend. [Cuentas y credenciales locales](docs/testing/TEST_USERS.example.md).
El bootstrap original sigue disponible con BootstrapAdmin__Nombre/Correo/Password.
No habilitar seed contra producción. El inicializador es para una réplica local.

## Codespaces y ejecución sin Docker

`.devcontainer/devcontainer.json` prepara .NET 8, Docker, Node y PowerShell 7.
Puerto 8080 privado. Abrir Codespace, copiar/editar env y ejecutar Compose como arriba.
Usar la pestaña Ports para Swagger; no publicar el puerto de SQL.
Los comandos usan csproj: funcionan con SDK 8 aunque `.slnx` requiera SDK más reciente.
Con SQL ya disponible y variables locales configuradas:
`dotnet run --project ConnectaOficios.Api --no-launch-profile`.
.NET no carga `.env` automáticamente fuera de Compose.

## Pruebas y reportes

Sin SDK ni PowerShell en el host: `docker compose --profile testing run --build --rm unit-tests`.
Este runner genera los mismos TRX/HTML/coverage en `reports/`, sin depender de SQL ni Resend.
La primera construcción descarga dependencias; las pruebas usan SQLite aislado.


```bash
dotnet test ConnectaOficios.Tests/ConnectaOficios.Tests.csproj
# Solo clase / caso asignado:
dotnet test ConnectaOficios.Tests/ConnectaOficios.Tests.csproj --filter 'FullyQualifiedName~UsuarioServiceTests'
dotnet test ConnectaOficios.Tests/ConnectaOficios.Tests.csproj --filter 'FullyQualifiedName~Login_ValidatesPasswordAndState'
# Líder: suite completa + TRX + HTML ejecución + HTML coverage
pwsh -NoProfile -File scripts/test-report.ps1
# Alternativa Bash (requiere pwsh):
bash scripts/test-report.sh
```

En Windows PowerShell también funciona `./scripts/test-report.ps1`.
Se reutiliza `generate-test-report.ps1` para convertir TRX a HTML y ReportGenerator
fijado en `.config/dotnet-tools.json` para Coverlet. Resultados por ejecución en
`reports/unit/<fecha>/`, coverage en `reports/coverage/index.html`: líneas, ramas,
métodos y clases según soporte del reporte. No mezclar coberturas de ejecuciones previas.
Resultados ignorados en Git. Abrir HTML y `Ctrl + P → Guardar como PDF`.

Tests organizados por funcionalidad, nunca por integrante. SQLite en memoria verifica
servicios y HTTP sin red/SQL/Resend; no sustituye la validación de migraciones y consultas
en SQL Server. [Matriz de casos y auditoría](docs/testing/AUDIT.md).

## Postman y Newman

Importar `tests/postman/local.collection.json` y `local.environment.json` en Postman.
URL local y tokens dinámicos en memoria; no exportar tokens ni environments privados.
La colección crea usuarios locales y elimina su administrador desechable; no limpia
usuarios de registro para no borrar datos ajenos. Password debe coincidir con el seed.

```bash
cd tests
npm ci
mkdir -p ../reports/newman
npx newman run postman/local.collection.json -e postman/local.environment.json --reporters cli,json,junit,htmlextra --reporter-json-export ../reports/newman/results.json --reporter-junit-export ../reports/newman/results.xml --reporter-htmlextra-export ../reports/newman/index.html --reporter-htmlextra-skipSensitiveData
# Equivalente:
npm run api
```

La colección bloquea destinos externos. JSON/TRX pueden incluir datos: mantener los
reportes locales; revisar/sanear antes de compartir. HTML omite datos sensibles.

## Seguridad ZAP

```bash
bash tests/zap/scan.sh
```

API y SQL deben estar healthy. ZAP importa OpenAPI local en la red Docker y obtiene
JWT de principal mediante hook. Reportes HTML/JSON/XML en reports/zap. Scan activo:
puede modificar datos locales, ejecutar después de la suite funcional. No acepta URL
productiva. Advertencias ZAP requieren análisis; no equivalen a PASS automático.
Roles, 401 sin token, firma inválida, inactivos y validación se prueban además con HTTP
in-process en `Integration/ApiSecurityTests.cs` y Postman. No confundir scan con auditoría
completa de seguridad. Revisar headers y exposición de datos en los hallazgos.

## k6 Load y Stress

Escenarios separados; incluyen login concurrente, listado, búsqueda y filtros de usuarios.
Siempre pageSize=10. Objetivos iniciales: Load errores <1%, p95 <2s; Stress errores <5%,
p95 <5s. Ajustar según mediciones, registrar CPU/RAM SQL/API y punto de saturación.

```bash
mkdir -p reports/k6
# k6 instalado localmente:
k6 run --summary-export reports/k6/load.json tests/k6/load.js
k6 run --summary-export reports/k6/stress.json tests/k6/stress.js
# Solo Docker, sin instalar k6:
docker run --rm --network connectaoficios_backend -v "$PWD/tests/k6:/scripts:ro" -v "$PWD/reports/k6:/reports" -e BASE_URL=http://connectaoficios-api:8080 grafana/k6:latest run --summary-export /reports/load.json /scripts/load.js
docker run --rm --network connectaoficios_backend -v "$PWD/tests/k6:/scripts:ro" -v "$PWD/reports/k6:/reports" -e BASE_URL=http://connectaoficios-api:8080 grafana/k6:latest run --summary-export /reports/stress.json /scripts/stress.js
```

`TEST_PASSWORD` configurable; solo cuentas locales. No ejecutar Load y Stress juntos.
Guardar salida de consola junto al JSON para la evidencia. El login usa BCrypt:
la saturación CPU es parte de la medición, no deshabilitar hashing para obtener PASS.

## Integración con Java y avatares

[Contrato exacto JWT y procedimiento](docs/testing/CONTRACTS.md). Ambos backends locales
comparten configuración JWT. `tests/integration/dotnet-java.ps1` exige ruta Java real.
No se afirma compatibilidad end-to-end hasta verificar ID/rol en Java local.
.NET ya devuelve ID+rol para batch de fotos; Portal combinará perfiles sin N+1.

## Trabajo de integrantes y líder

Integrantes agregan casos en la clase/módulo asignado, ejecutan su filtro y confirman
PASS; posteriormente commit y push en su futura rama conforme al flujo del equipo.
No modifican Docker, scripts generales, cobertura ni infraestructura sin asignación.
No generan reportes globales. En esta preparación no se hacen ramas, commit ni push.

Líder: integrar las ramas en una fase posterior, ejecutar suite completa, generar TRX,
HTML de ejecución y coverage; después Newman, ZAP, k6 Load y k6 Stress por separado.
Registrar versiones, fecha, configuración local sin secretos y resultados reales.
No presentar herramientas preparadas como pruebas ejecutadas.

## Importar datos existentes a Docker

Importacion DATA ONLY sobre el esquema de EF Core, preservando IDs y hashes.
Consultar [procedimiento seguro, conflictos y validacion](docker/import/README.md).
La preparacion no importa ni borra datos; no eliminar el volumen Docker.
