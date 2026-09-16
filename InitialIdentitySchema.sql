IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Roles] (
    [Id] int NOT NULL IDENTITY,
    [Nombre] nvarchar(50) NOT NULL,
    [Descripcion] nvarchar(200) NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Usuarios] (
    [Id] int NOT NULL IDENTITY,
    [Nombre] nvarchar(100) NOT NULL,
    [Correo] nvarchar(150) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [Telefono] nvarchar(25) NULL,
    [RolId] int NOT NULL,
    [Estado] int NOT NULL,
    [FechaCreacion] datetime2 NOT NULL,
    [FechaActualizacion] datetime2 NULL,
    [UltimoAcceso] datetime2 NULL,
    CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Usuarios_Roles_RolId] FOREIGN KEY ([RolId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Descripcion', N'Nombre') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] ON;
INSERT INTO [Roles] ([Id], [Activo], [Descripcion], [Nombre])
VALUES (1, CAST(1 AS bit), N'Usuario que solicita servicios', N'Cliente'),
(2, CAST(1 AS bit), N'Usuario que ofrece servicios', N'Trabajador'),
(3, CAST(1 AS bit), N'Usuario con permisos administrativos', N'Administrador'),
(4, CAST(1 AS bit), N'Usuario con permisos administrativos completos', N'AdministradorPrincipal');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Descripcion', N'Nombre') AND [object_id] = OBJECT_ID(N'[Roles]'))
    SET IDENTITY_INSERT [Roles] OFF;
GO

CREATE UNIQUE INDEX [IX_Roles_Nombre] ON [Roles] ([Nombre]);
GO

CREATE UNIQUE INDEX [IX_Usuarios_Correo] ON [Usuarios] ([Correo]);
GO

CREATE INDEX [IX_Usuarios_RolId] ON [Usuarios] ([RolId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916151532_InitialIdentitySchema', N'8.0.31');
GO

COMMIT;
GO

