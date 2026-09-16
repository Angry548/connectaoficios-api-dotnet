using AutoMapper;
using ConnectaOficios.Api.DTOs.Admins;
using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConnectaOficios.Api.Services.Admins;

public class AdminServices : IAdminServices
{
    private readonly ApplicationDbContext _db;
    private readonly IMapper _mapper;

    public AdminServices(
        ApplicationDbContext db,
        IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<IEnumerable<UserResponse>> GetAll()
    {
        var administradores = await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Where(u =>
                u.RolId == 3 ||
                u.RolId == 4)
            .OrderBy(u => u.Nombre)
            .ToListAsync();

        return _mapper.Map<IEnumerable<UserResponse>>(administradores);
    }

    public async Task<UserResponse?> GetById(int id)
    {
        var administrador = await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                (u.RolId == 3 || u.RolId == 4));

        if (administrador == null)
        {
            return null;
        }

        return _mapper.Map<UserResponse>(administrador);
    }

    public async Task<UserResponse?> Create(
        AdminAccountRequest admin)
    {
        // Solo se pueden crear cuentas administrativas.
        if (admin.RolId != 3 && admin.RolId != 4)
        {
            return null;
        }

        var emailExists = await _db.Usuarios
            .AnyAsync(u => u.Correo == admin.Correo);

        if (emailExists)
        {
            return null;
        }

        var entity = new Usuario
        {
            Nombre = admin.Nombre.Trim(),
            Correo = admin.Correo.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                admin.Password
            ),
            Telefono = admin.Telefono?.Trim(),
            RolId = admin.RolId,
            Estado = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow
        };

        await _db.Usuarios.AddAsync(entity);
        await _db.SaveChangesAsync();

        await _db.Entry(entity)
            .Reference(u => u.Rol)
            .LoadAsync();

        return _mapper.Map<UserResponse>(entity);
    }
}