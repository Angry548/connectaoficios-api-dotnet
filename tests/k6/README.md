# Rendimiento k6 — Angel Mate (CON-553 a CON-565)

Requiere k6 2.x y dos APIs de QA en loopback: .NET `http://127.0.0.1:8080`
y Java `http://127.0.0.1:8081`. El runner rechaza un BASE_URL diferente y hace
preflight de ambas APIs. Sin Docker ni dependencias adicionales.

```powershell
$secrets = Get-Content -Raw $qaSecretsPath | ConvertFrom-Json
# El archivo ignorado contiene el principal QA como adminEmail/adminPassword o correo/password.
./tests/k6/run-performance.ps1 -SecretsPath $qaSecretsPath
```

La credencial se pasa desde el archivo QA local al entorno del proceso y no se
guarda en los scripts, variables versionadas o reportes. `setup()` obtiene un JWT
local, que solo vive en memoria de k6. Los escenarios son consultas y login;
no crean ni modifican datos de negocio.

Load usa una piscina compartida de hasta 5 VUs durante 80 segundos y rota entre
login, usuarios, filtros, búsqueda de servicios, filtros de promociones, reportes
con tamaños 5/10/20 y estadísticas. El perfil se ejecuta dos veces.

Stress aumenta 1 → 3 → 5 → 8 → 10 → 12 VUs y sostiene el pico por 20 segundos;
después mantiene 6 VUs durante 90 segundos en autenticación con consultas de
lectura periódicas. Duración planeada: 3m 30s.

Las métricas nativas de k6 incluyen promedio, mediana, p90, p95, p99, mínimo,
máximo, solicitudes fallidas, requests/s, iteraciones, checks y VUs. Tendencias
adicionales dan latencia/error por caso y latencia/error por endpoint. CON-565
ordena las latencias p95/p99 por endpoint para observar degradación. No se ha
definido SLA de negocio; los resultados son mediciones descriptivas locales.

Se usa el Web Dashboard oficial incluido en k6 para exportar HTML autocontenido;
el JSON procede del resumen final real entregado por k6. Los HTML conservan la
última ejecución de cada perfil; `load-summary.json` registra las dos ejecuciones.

Archivos locales ignorados por Git: `reports/k6/load-report.html`,
`reports/k6/stress-report.html`, `reports/k6/load-summary.json` y
`reports/k6/stress-summary.json`. Las cuentas de prueba deben tener permisos
AdministradorPrincipal para los endpoints administrativos.

Referencia: [Web Dashboard y exportación HTML de k6](https://grafana.com/docs/k6/latest/results-output/web-dashboard/).
