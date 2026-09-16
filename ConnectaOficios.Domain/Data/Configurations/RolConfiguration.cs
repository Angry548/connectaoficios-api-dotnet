using ConnectaOficios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConnectaOficios.Domain.Data.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombre)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Nombre)
            .IsUnique();

        builder.Property(r => r.Descripcion)
            .HasMaxLength(200);

        builder.Property(r => r.Activo)
            .IsRequired();

        builder.HasData(
            new Rol
            {
                Id = 1,
                Nombre = "Cliente",
                Descripcion = "Usuario que solicita servicios",
                Activo = true
            },
            new Rol
            {
                Id = 2,
                Nombre = "Trabajador",
                Descripcion = "Usuario que ofrece servicios",
                Activo = true
            },
            new Rol
            {
                Id = 3,
                Nombre = "Administrador",
                Descripcion = "Usuario con permisos administrativos",
                Activo = true
            },
            new Rol
            {
                Id = 4,
                Nombre = "AdministradorPrincipal",
                Descripcion = "Usuario con permisos administrativos completos",
                Activo = true
            }
        );
    }
}
