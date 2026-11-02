# Validación final

## Cierre de casos de Angel Mate — 2 de noviembre de 2026

Ejecución local con `./scripts/test-report.ps1`: xUnit + Moq, `dotnet test`,
Coverlet/XPlat Code Coverage y ReportGenerator 5.4.3. Resultado validado:
56 total, 56 Passed, 0 Failed, 0 Skipped; éxito 100%.
Cobertura global API + Domain: líneas 48.1% (1550/3216), ramas 56.9%
(139/244), métodos 83.4% (111/133). Incluye migraciones y toda la suite,
con pruebas de componentes SQLite y HTTP; no son exclusivamente unitarias puras.

Reportes locales ignorados por Git:

- Ejecución HTML: `reports/unit/20261102-024951-297/index.html`.
- TRX: `reports/unit/20261102-024951-297/resultado_pruebas.trx`.
- Cobertura HTML: `reports/coverage/index.html`.
- Cobertura XML: `reports/unit/20261102-024951-297/b9233a05-6029-4e6e-ba5f-58f902ed2d57/coverage.cobertura.xml`.

Evidencia reutilizada sin modificar ni reasignar autoría de pruebas existentes:

| Casos | Pruebas existentes |
| --- | --- |
| CON-241–244 | JwtServiceTests.GenerateToken_WithValidConfiguration_ReturnsSignedTokenWithUserClaims |
| CON-245–246 | UsuarioServiceTests.Login_ValidatesPasswordAndState |
| CON-247, 249–250 | UsuarioServiceTests.Register_ValidPublicRole_ReturnsIdentity |
| CON-248 | UsuarioServiceTests.Register_DuplicateEmail_Rejected |
| CON-251 | ApiSecurityTests.Register_InvalidRequiredFields_Returns400 |
| CON-252–253 | AdministradorServiceTests.CreateAndUpdateAdministrator |
| CON-254–255 | AdministradorServiceTests.DisableAndDeleteAdministrator |
| CON-256 | AdministradorServiceTests.LastActivePrincipal_CannotBeDisabledOrDeleted |
| CON-257–258 | UsuarioServiceTests.GetById_ContainsPortalIdentity / GetById_Missing_ReturnsNull |
| CON-260 | UsuarioServiceTests.ChangeStatus_PublicUser |
| CON-261 | UsuarioServiceTests.List_FiltersAndPaginates / Search_ContainsRoleAndIsBounded |
| CON-262–263 | CuentaServiceTests.ChangePassword_RequiresCurrentPassword |
| CON-376 | ApiSecurityTests.CurrentAccount_ReturnsIdentityWithoutPhoto |
| CON-377 | CuentaServiceTests.UpdateCurrentUser_UpdatesPersonalData |
| CON-378 | CuentaServiceTests.ResetPassword_UsesOneTimeTokenAndStoresOnlyHash |

Única clase nueva: `UsuarioValidationAndActivationTests`, de Angel Mate.
Dos métodos Theory, ocho escenarios: CON-251 valida cada campo obligatorio
inválido por separado y ausencia de persistencia (seis); CON-259 verifica la
transición de Inactivo a Activo persistida para Cliente y Trabajador (dos).
La prueba previa de estado 1 partía de una cuenta activa y no cubría reactivación.
Todos los archivos de pruebas preexistentes permanecen intactos, incluidos los
de Eduardo, Alfredo, Óscar y Nuria. Sin cambios productivos ni infraestructura.

Los resultados históricos siguientes corresponden a la ejecución anterior.

Sesión del 31 de octubre de 2026 (El Salvador). El runner Docker usa UTC; por eso
el directorio de su ejecución final corresponde al 1 de noviembre UTC.

| Verificación | Resultado real |
| --- | --- |
| Rama inicial/final | main; sin cambios de rama |
| Test original | 1 PASS antes de ampliar la suite |
| Suite final en Docker / SDK 8 | 38 total, 38 PASS, 0 FAIL, 0 omitidos |
| Coverage líneas | 46.2%, 1488/3214 |
| Coverage ramas | 54%, 132/244 |
| Coverage métodos | 82.7%, 110/133 |
| API Docker build | PASS, publish Release API + DatabaseInit |
| Runner Docker tests + reportes | PASS |
| Compose config --quiet | PASS |
| SQL Server + API | Ambos healthy; volumen existente conservado |
| Health / OpenAPI | HTTP 200 |
| Newman | 28 requests, 37 aserciones, cero fallos |
| k6 load.js y stress.js | inspect PASS; no se ejecutaron escenarios |
| ZAP | Configuración y hook preparados; scan NO ejecutado |
| Integración Java | Procedimiento/script preparados; NO ejecutada sin Java local |
| Codespaces | Configuración preparada; Codespace NO abierto |
| git diff --check | PASS, código 0; avisos de normalización LF/CRLF no son errores |
| Commit / push / merge / ramas nuevas | Ninguno |

Se conservó main: era la rama real y la primera indicada; la mención final a master
no se aplicó porque habría requerido contradecir la instrucción de no cambiar rama.

Artefactos locales, ignorados por Git:

- `reports/unit/20261101-020229-612/resultado_pruebas.trx`
- `reports/unit/20261101-020229-612/index.html`
- `reports/coverage/index.html` y `Summary.txt`
- `reports/newman/index.html`, `results.json`, `results.xml`, `console.log`
- `reports/docker-tests.log`

Coverage incluye migraciones, bootstrap, DTOs y endpoints: no se excluyeron para
elevar el porcentaje. La suite combina una prueba JWT, pruebas de componentes con
SQLite aislado y pruebas HTTP. Falta ampliar las ramas indicadas en AUDIT.md;
38 PASS no significa cobertura completa ni certificación de seguridad/rendimiento.

La imagen API arrancó aplicando su inicializador EF y el seed local controlado.
No se borró ningún volumen. Los contenedores API/SQL quedaron ejecutándose.
Newman crea registros sintéticos y elimina solo su administrador desechable.

Se retiraron valores sensibles de appsettings versionables; si seguían vigentes,
rotarlos fuera de esta tarea. El historial no fue alterado. Despliegues futuros
deben inyectar connection string y secretos mediante variables/configuración segura.

Listado de archivos: FILES.md. Salida de `git status --short`: GIT_STATUS.txt.
