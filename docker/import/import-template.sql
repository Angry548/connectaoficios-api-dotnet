-- DATA ONLY. Copiar a un archivo privado ignorado y completar solo las tablas #.
-- No ejecutar el template vacio: el guard impide cambios accidentales.
-- Ejecutar exclusivamente con docker exec y sqlcmd -b -x, en una sola sesion.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'ConnectaOficiosDb'
    THROW 51000, 'Base destino incorrecta.', 1;

-- Tablas temporales de transporte; no crean otro esquema de aplicacion.
CREATE TABLE #Roles (
    Id int NOT NULL PRIMARY KEY, Nombre nvarchar(50) COLLATE DATABASE_DEFAULT NOT NULL UNIQUE,
    Descripcion nvarchar(200) COLLATE DATABASE_DEFAULT NULL, Activo bit NOT NULL
);
CREATE TABLE #Usuarios (
    Id int NOT NULL PRIMARY KEY, Nombre nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    Correo nvarchar(150) COLLATE DATABASE_DEFAULT NOT NULL UNIQUE, PasswordHash nvarchar(255) COLLATE DATABASE_DEFAULT NOT NULL,
    Telefono nvarchar(25) COLLATE DATABASE_DEFAULT NULL, RolId int NOT NULL, Estado int NOT NULL,
    FechaCreacion datetime2 NOT NULL, FechaActualizacion datetime2 NULL,
    UltimoAcceso datetime2 NULL, PasswordResetTokenHash nvarchar(64) COLLATE DATABASE_DEFAULT NULL,
    PasswordResetTokenExpiresAt datetime2 NULL
);
-- PEGAR AQUI INSERT INTO #Roles (...) VALUES (...);
-- y despues INSERT INTO #Usuarios (...) VALUES (...);
-- Usar columnas explicitas, literales N'...' escapando comillas como '',
-- fechas ISO 8601 y NULL real. No incluir GO, USE, DDL ni IDENTITY_INSERT exportado.
-- Copiar PasswordHash literalmente (BCrypt incluye su salt).

IF NOT EXISTS (SELECT 1 FROM #Roles) OR NOT EXISTS (SELECT 1 FROM #Usuarios)
    THROW 51001, 'Template sin datos: no se ha importado nada.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Mantener API parada y evitar escritores concurrentes.
    DECLARE @n bigint;
    SELECT @n = COUNT_BIG(*) FROM dbo.Roles WITH (TABLOCKX, HOLDLOCK);
    SELECT @n = COUNT_BIG(*) FROM dbo.Usuarios WITH (TABLOCKX, HOLDLOCK);
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Usuarios')
               AND (is_disabled = 1 OR is_not_trusted = 1))
        THROW 51002, 'FK deshabilitada o no confiable; revisar antes de importar.', 1;
    IF EXISTS (SELECT 1 FROM #Roles s JOIN dbo.Roles d ON s.Nombre = d.Nombre WHERE s.Id <> d.Id)
       OR EXISTS (SELECT 1 FROM #Usuarios s JOIN dbo.Usuarios d ON s.Correo = d.Correo WHERE s.Id <> d.Id)
        THROW 51003, 'Nombre de rol/correo existente con otro ID. No remapear IDs.', 1;
    -- Idempotencia estricta: solo omitir filas iguales, nunca ocultar conflictos.
    IF EXISTS (
        SELECT 1 FROM #Roles s JOIN dbo.Roles d ON s.Id = d.Id
        WHERE EXISTS (
            SELECT CONVERT(varbinary(max), s.Nombre), CONVERT(varbinary(max), s.Descripcion), s.Activo
            EXCEPT
            SELECT CONVERT(varbinary(max), d.Nombre), CONVERT(varbinary(max), d.Descripcion), d.Activo))
        THROW 51004, 'Rol existente distinto: revisar origen y destino.', 1;
    IF EXISTS (
        SELECT 1 FROM #Usuarios s JOIN dbo.Usuarios d ON s.Id = d.Id
        WHERE EXISTS (
            SELECT CONVERT(varbinary(max), s.Nombre), CONVERT(varbinary(max), s.Correo),
                   CONVERT(varbinary(max), s.PasswordHash), CONVERT(varbinary(max), s.Telefono),
                   s.RolId, s.Estado, s.FechaCreacion, s.FechaActualizacion, s.UltimoAcceso,
                   CONVERT(varbinary(max), s.PasswordResetTokenHash), s.PasswordResetTokenExpiresAt
            EXCEPT
            SELECT CONVERT(varbinary(max), d.Nombre), CONVERT(varbinary(max), d.Correo),
                   CONVERT(varbinary(max), d.PasswordHash), CONVERT(varbinary(max), d.Telefono),
                   d.RolId, d.Estado, d.FechaCreacion, d.FechaActualizacion, d.UltimoAcceso,
                   CONVERT(varbinary(max), d.PasswordResetTokenHash), d.PasswordResetTokenExpiresAt))
        THROW 51005, 'Usuario existente distinto: no se sobrescribio ninguna credencial.', 1;
    IF EXISTS (SELECT 1 FROM #Usuarios s WHERE NOT EXISTS (SELECT 1 FROM #Roles r WHERE r.Id=s.RolId)
               AND NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Id=s.RolId))
        THROW 51006, 'Usuario con RolId inexistente.', 1;
    IF EXISTS (SELECT 1 FROM #Usuarios WHERE Estado NOT IN (1,2))
        THROW 51007, 'Estado desconocido para este backend.', 1;

    SET IDENTITY_INSERT dbo.Roles ON;
    INSERT dbo.Roles (Id, Nombre, Descripcion, Activo)
    SELECT Id, Nombre, Descripcion, Activo FROM #Roles s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles d WHERE d.Id=s.Id);
    SET IDENTITY_INSERT dbo.Roles OFF;

    SET IDENTITY_INSERT dbo.Usuarios ON;
    INSERT dbo.Usuarios (Id, Nombre, Correo, PasswordHash, Telefono, RolId, Estado,
        FechaCreacion, FechaActualizacion, UltimoAcceso, PasswordResetTokenHash, PasswordResetTokenExpiresAt)
    SELECT Id, Nombre, Correo, PasswordHash, Telefono, RolId, Estado,
        FechaCreacion, FechaActualizacion, UltimoAcceso, PasswordResetTokenHash, PasswordResetTokenExpiresAt
    FROM #Usuarios s WHERE NOT EXISTS (SELECT 1 FROM dbo.Usuarios d WHERE d.Id=s.Id);
    SET IDENTITY_INSERT dbo.Usuarios OFF;
    COMMIT;
    SELECT N'Importacion completada; ejecutar validacion documentada.' AS Resultado;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK;
    SET IDENTITY_INSERT dbo.Roles OFF;
    SET IDENTITY_INSERT dbo.Usuarios OFF;
    THROW;
END CATCH;
