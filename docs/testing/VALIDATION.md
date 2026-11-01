# Validación final

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
