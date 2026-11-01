# Pruebas .NET

xUnit + Moq + Coverlet. Se conserva el test JWT original y se agregan clases por
funcionalidad: UsuarioServiceTests, CuentaServiceTests y AdministradorServiceTests.
Integration/ApiSecurityTests usa WebApplicationFactory para HTTP/autorización.
ServiceFixture usa SQLite en memoria por caso, sin SQL ni servicios externos;
no equivale a probar la semántica y concurrencia de SQL Server.

Desde la raíz:

```bash
dotnet test ConnectaOficios.Tests/ConnectaOficios.Tests.csproj --filter 'FullyQualifiedName~UsuarioServiceTests'
# Líder: informes completos, mismo generador TRX HTML existente
pwsh -File scripts/test-report.ps1
# Todo dentro de Docker
docker compose --profile testing run --build --rm unit-tests
```

Usar datos sintéticos, Arrange/Act/Assert y mocks para servicios externos. No crear
clases con nombres de integrantes. Cada integrante agrega y ejecuta sus casos;
no modifica infraestructura ni genera reportes globales sin asignación.

[README general](../README.md), [matriz Jira](../docs/testing/AUDIT.md),
[contratos JWT y DTOs](../docs/testing/CONTRACTS.md).
