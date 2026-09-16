using AutoMapper;
using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConnectaOficios.Api.Services.Users;

public class UserServices : IUserServices
{
    private readonly ApplicationDbContext _db;
    private readonly IMapper _mapper;

    public UserServices(ApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<UserResponse?> Register(UserRequest user)
    {
        if (user.RolId != 1 && user.RolId != 2)
        {
            return null;
        }

        var emailExists = await _db.Usuarios
            .AnyAsync(u => u.Correo == user.Correo);

        if (emailExists)
            return null;

        var entity = _mapper.Map<Usuario>(user);

        entity.Estado = EstadoUsuario.Activo;
        entity.FechaCreacion = DateTime.UtcNow;

        entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.Password);

        await _db.Usuarios.AddAsync(entity);
        await _db.SaveChangesAsync();

        await _db.Entry(entity)
            .Reference(u => u.Rol)
            .LoadAsync();

        return _mapper.Map<UserResponse>(entity);
    }
}