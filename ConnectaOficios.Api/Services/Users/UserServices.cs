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
        // Verificar que el correo no esté registrado
        var emailExists = await _db.Usuarios
            .AnyAsync(u => u.Correo == user.Correo);

        if (emailExists)
            return null;

        // Crear entidad a partir del DTO
        var entity = _mapper.Map<Usuario>(user);

        // Configurar datos iniciales del usuario
        entity.Estado = EstadoUsuario.Activo;
        entity.FechaCreacion = DateTime.UtcNow;

        /*
         * La contraseña será cifrada con BCrypt
         * en la siguiente subtarea.
         */
        entity.PasswordHash = user.Password;

        await _db.Usuarios.AddAsync(entity);
        await _db.SaveChangesAsync();

        // Cargar el rol para construir la respuesta
        await _db.Entry(entity)
            .Reference(u => u.Rol)
            .LoadAsync();

        return _mapper.Map<UserResponse>(entity);
    }
}