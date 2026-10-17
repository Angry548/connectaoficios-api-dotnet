# Pruebas unitarias de ConnectaOficios

Proyecto .NET 8 con xUnit, Moq y Coverlet. Referencia a ConnectaOficios.Api.

Desde el directorio de la solución:

```powershell
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

Coverlet genera `coverage.cobertura.xml` dentro de `ConnectaOficios.Tests/TestResults/<id>/`.
Los resultados y las carpetas bin/obj ya están excluidos por el .gitignore existente.

## Organización

- Services/Authentication: JWT, inicio de sesión y recuperación de contraseña.
- Services/Users: registro, búsqueda, paginación, perfil y estado de usuarios.
- Services/Administrators: creación, edición, estado y eliminación de administradores.
- Helpers: utilidades compartidas cuando sean necesarias.

La única prueba inicial es `JwtServiceTests.GenerateToken_WithValidConfiguration_ReturnsSignedTokenWithUserClaims`.
Usa Arrange / Act / Assert y Moq para configurar IConfiguration; valida la firma,
el emisor, la audiencia, los claims del usuario y la expiración del JWT.
No requiere SQL Server, secretos reales ni servicios externos.

Mantener las pruebas independientes y usar datos sintéticos. Simular las interfaces
externas con Moq. Los servicios UserServices y AdminServices usan consultas asíncronas
de EF Core sobre un DbContext concreto: acordar una estrategia de datos antes de
ampliar su cobertura; un IQueryable simulado común no implementa IAsyncQueryProvider.
Las pruebas del proveedor SQL y de endpoints pueden requerir pruebas de integración.
