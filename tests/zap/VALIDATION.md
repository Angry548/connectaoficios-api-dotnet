# Validacion OWASP ZAP - Angel Mate

Ejecucion final: 2026-11-02T12:28:46.589Z; exportacion: 2026-11-02T12:29:24.793Z.
ZAP 2.17.0; Passive + scoped OpenAPI import + focused Active GET scan.
Targets: http://127.0.0.1:8080, http://127.0.0.1:8081. Sin scans productivos.

## Entorno observado

.NET: SQL Express / ConnectaOficiosE2E. Java: H2 en memoria / connectaoficios_e2e / MODE=MySQL. Se reutilizaron los procesos E2E locales existentes; .NET sirve una DLL de otro checkout local, no fue reconstruido por este runner. Estas pruebas verifican los servicios ejecutandose en los puertos indicados, no certifican identidad con el build de este checkout ni paridad MySQL/productiva.
SHA256 DLL .NET ejecutada: 5B80C99D93B73E6AA15BEAD0DB45AB741840DF3AC29B3B9FD64C2C6DFFF727D7.

## Resultados

- CON-518: Pass
- CON-519: Pass
- CON-520: Pass
- CON-521: Pass
- CON-522: Pass
- CON-523: Pass
- CON-524: Pass
- CON-525: Pass
- CON-526: Pass

Dos repeticiones funcionales completas con cuentas, IDs y fixtures nuevos.
CON-521: propietario HTTP 200; no participante HTTP 400 con mensaje explicito de rechazo y sin datos protegidos. Se recomienda 403 por consistencia semantica; no se altero el servicio Java.

High: 0; Medium: 0; Low: 1; Informational: 1.
Total: 2 grupos de alertas por sitio, 3 ocurrencias.
Duracion: 36 s desde preparacion funcional hasta finalizacion pasiva/activa; excluye arranque del daemon y exportacion.
URLs distintas con respuesta HTTP: 300; rutas aproximadas incluyendo probes negativos: 32; mensajes registrados: 380.
5 scans activos finalizados; 329 peticiones activas. Las 4 reglas seleccionadas finalizaron: XSS reflejado, SQL injection, CRLF y parameter tampering.

## Hallazgos conservados

| Nombre | Riesgo | Endpoint | Descripcion y recomendacion |
|---|---|---|---|
| X-Content-Type-Options Header Missing (10021) | Low | GET .NET /health; /swagger/v1/swagger.json | Falta nosniff. Agregar X-Content-Type-Options: nosniff y mantener Content-Type correcto. |
| Content-Type Header Missing (10019) | Informational | OPTIONS Java /api/health | Preflight rechazado sin Content-Type. Revisar manejo uniforme de respuestas OPTIONS/error y tipo del contenido si existe cuerpo. El rechazo CORS no demuestra por si mismo una vulnerabilidad explotable; se conserva la alerta original. |

Cabeceras Java observadas: nosniff, X-Frame-Options DENY, Cache-Control no-cache/no-store, Pragma no-cache y Content-Type application/json en respuestas JSON. Origin no permitido no recibio Access-Control-Allow-Origin. Server Kestrel visible en .NET (observacion manual, sin inventar una alerta ZAP adicional).
No se corrigio codigo de las APIs ni se ocultaron alertas. Pass de CON-523..526 acredita ejecucion y evidencia; no ausencia de vulnerabilidades. Politica activa enfocada, solo GET, sin OAST/callbacks externos ni lectura de archivos/comandos. CRUD destructivo limitado a DELETE de un servicio QA propio. Fixtures restantes se conservan con UUID para trazabilidad.

## Evidencia

HTML, JSON y XML son exportaciones oficiales nuevas de ZAP, sin edicion de hallazgos. reports/ permanece ignorado en Git. functional-1.json, functional-2.json y http-evidence.json conservan evidencia sin JWT, claves o passwords.

- zap-report.html SHA256: 2ac4c45a6444d5f205589220afef52f2aaa2bc675cba0954b893d16e1c912baf
- zap-report.json SHA256: ce3c7b84fa7351c78abbe4e1e92fc7e59562daed7f5ec6ec88766f67e6662903
- zap-report.xml SHA256: 39219e9496402e894948071cd8ee4c6768d743636fea3bc6fdbec5850855a2bd

Casos de Eduardo, Alfredo, Oscar y Nuria intactos: no se editaron sus archivos. Solo se agregaron archivos de Angel bajo tests/zap/.

Documentacion tecnica: https://www.zaproxy.org/docs/api/
