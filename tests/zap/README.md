# OWASP ZAP — Angel Mate (CON-518 a CON-526)

Runner Windows para una instalación existente de ZAP, Java 17+ y Node 18+.
No requiere Docker ni paquetes nuevos. `scan.sh` y `auth.py` conservan su runner
Docker anterior; este runner permite ejecutar ZAP instalado en Windows.

## Ejecución

Primero iniciar las APIs con la infraestructura QA existente. El runner acepta
exclusivamente `http://127.0.0.1:8080` (.NET) y `http://127.0.0.1:8081` (Java).
Confirmar que usan SQL Express aislado y H2 local, nunca conexiones productivas.
`tests/postman/start-local.ps1` documenta el entorno Newman; también se puede
reutilizar el entorno E2E local existente, proporcionando su archivo privado.
El archivo debe corresponder a los servicios que realmente están ejecutándose.

```powershell
# Entorno Newman existente:
./tests/zap/scan.ps1
# Otro entorno QA local ya preparado (no exportar credenciales):
./tests/zap/scan.ps1 -SecretsPath $qaSecretsPath
```

El archivo privado contiene `jwtKey`, `password`, `adminEmail`, `adminPassword`;
también se admite el formato E2E `jwtKey`, `password`, `correo`. Se lee en memoria,
sin imprimir valores. El principal debe tener rol AdministradorPrincipal.
Se puede indicar `-ZapDirectory` y `-JavaExecutable` si la detección no encuentra
sus instalaciones. ZAP usa exclusivamente loopback en el puerto dedicado 8090.
No apuntar este puerto a una sesión interactiva con trabajo pendiente: el runner
crea una sesión nueva. Si inicia su propio daemon, lo apaga al terminar.

## Alcance y evidencia

El runner crea identidades únicas con UUID, un administrador inferior, categoría,
zona, perfil, servicios, solicitud y conversación propios. No usa IDs fijos ni
resetea cuentas existentes. Repite CON-518..522 dos veces con datos nuevos.
Los fixtures se conservan para trazabilidad; solo elimina un servicio desechable
propio para comprobar DELETE. No envía correos, imágenes ni pagos.

| Caso | Comprobación real |
|---|---|
| CON-518 | JWT firmado con clave QA y expirado una hora: 401, sin datos; control firmado vigente: 200. |
| CON-519 | Se altera el primer carácter de la firma de un JWT obtenido por login local: 401, sin datos. |
| CON-520 | Administrador intenta crear una cuenta administrativa: 403; principal consulta y confirma que no se creó. |
| CON-521 | Participante consulta conversación: 200; otro cliente recibe **400**, mensaje explícito de no pertenencia, sin conversación. |
| CON-522 | Trabajador ajeno intenta aceptar solicitud: 403; estado permanece PENDIENTE; trabajador propietario sí puede aceptar. |
| CON-523 | Acceso dirigido a health y OpenAPI públicos de ambas APIs, escaneo pasivo y activo local. |
| CON-524 | POST/GET/PUT/DELETE de servicios QA a través de ZAP; GET propio y búsqueda con parámetros bajo escaneo activo. |
| CON-525 | Creación, consulta y cambio autorizado/no autorizado de solicitudes a través de ZAP; GET propio y paginación propia bajo escaneo activo. |
| CON-526 | Respuestas Java, Content-Type, caché, cabeceras de seguridad y preflight CORS con Origin no permitido, observados mediante ZAP. |

Se importan subconjuntos de los contratos OpenAPI **obtenidos de las APIs reales**.
Solo se importan GET públicos y GET con IDs propios; no se importa todo el CRUD
con parámetros de ejemplo que podrían afectar datos anteriores.

La política activa incluye XSS reflejado (40012), SQL injection (40018), CRLF
(40003) y parameter tampering (40008). Incluye parámetros reales de búsqueda y
paginación. Solo GET, sin callbacks OAST, comandos, lectura de archivos ni métodos
destructivos activos. No es una auditoría exhaustiva. No hay filtros de alertas,
cambios de confianza ni reclasificación automática de falsos positivos.

Pass en CON-523..526 significa que el análisis terminó y generó evidencia;
no significa ausencia de vulnerabilidades. El HTTP 400 de CON-521 es el contrato
observable del servicio local; sería preferible estandarizar denegaciones a 403.

## Entregables locales (ignorados por Git)

- `reports/zap/zap-report.html`: reporte oficial generado por ZAP, para abrir y guardar como PDF.
- `reports/zap/zap-report.json` y `zap-report.xml`: exportaciones oficiales sin filtrar.
- `summary.json`: versión, fechas, duración, métricas, reglas, progreso y número de peticiones activas por scan.
- `functional-1.json`, `functional-2.json`: controles, códigos, estados e IDs propios de ambas repeticiones.
- `http-evidence.json`: método, URL, código y cabeceras de respuesta; sin cuerpos ni Authorization.
- `execution-notes.md`: contexto del entorno y revisión de hallazgos de la entrega final.

La descripción del HTML incluye alcance, casos, duración y métricas de tráfico.
La duración cubre preparación funcional y scans, excluyendo exportación de reportes.
Las severidades cuentan grupos de alertas por sitio del JSON oficial de ZAP;
`alertInstances` cuenta sus ocurrencias. Las URLs se obtienen de mensajes con
respuesta HTTP real; rutas aproximadas ignoran query strings e incluyen probes
negativos, no equivalen a operaciones únicas del contrato.

El script espera finalización 100% de cada scan, peticiones activas mayores que
cero y cola pasiva vacía. Valida fecha de los archivos y ausencia de credenciales
QA/JWT en las exportaciones. La sesión interna puede contener tráfico autenticado:
todo `reports/` permanece ignorado; no versionar sesiones, config o reportes crudos.

Referencia de automatización: [API oficial de ZAP](https://www.zaproxy.org/docs/api/).
