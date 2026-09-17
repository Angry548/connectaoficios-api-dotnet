using AutoMapper;
using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Api.Security;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ConnectaOficios.Api.Services.Users;

public class UserServices : IUserServices
{
    private readonly ApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly IJwtService _jwtService;

    public UserServices(
        ApplicationDbContext db,
        IMapper mapper,
        IJwtService jwtService)
    {
        _db = db;
        _mapper = mapper;
        _jwtService = jwtService;
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
        {
            return null;
        }

        var entity = _mapper.Map<Usuario>(user);

        entity.Estado = EstadoUsuario.Activo;
        entity.FechaCreacion = DateTime.UtcNow;

        entity.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(user.Password);

        await _db.Usuarios.AddAsync(entity);
        await _db.SaveChangesAsync();

        await _db.Entry(entity)
            .Reference(u => u.Rol)
            .LoadAsync();

        return _mapper.Map<UserResponse>(entity);
    }

    public async Task<LoginResponse?> Login(LoginRequest login)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Correo == login.Correo);

        if (usuario == null)
        {
            return null;
        }

        if (usuario.Estado != EstadoUsuario.Activo)
        {
            return null;
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            login.Password,
            usuario.PasswordHash
        );

        if (!passwordValid)
        {
            return null;
        }

        usuario.UltimoAcceso = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var token = _jwtService.GenerateToken(usuario);

        return new LoginResponse
        {
            Token = token,
            Usuario = _mapper.Map<UserResponse>(usuario)
        };
    }

    public async Task<IEnumerable<UserResponse>> GetAll(
    string? nombre = null,
    string? correo = null,
    int? rolId = null,
    int? estado = null)
    {
        var query = _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .AsQueryable();

        // Filtrar por nombre
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            query = query.Where(u =>
                u.Nombre.Contains(nombre));
        }

        // Filtrar por correo
        if (!string.IsNullOrWhiteSpace(correo))
        {
            query = query.Where(u =>
                u.Correo.Contains(correo));
        }

        // Filtrar por rol
        if (rolId.HasValue)
        {
            query = query.Where(u =>
                u.RolId == rolId.Value);
        }

        // Filtrar por estado
        if (estado.HasValue)
        {
            query = query.Where(u =>
                (int)u.Estado == estado.Value);
        }

        var usuarios = await query
            .OrderBy(u => u.Nombre)
            .ToListAsync();

        return _mapper.Map<IEnumerable<UserResponse>>(usuarios);
    }
    public async Task<UserResponse?> GetById(int id)
    {
        var usuario = await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
        {
            return null;
        }

        return _mapper.Map<UserResponse>(usuario);
    }
    public async Task<UserResponse?> ChangeStatus(
    int id,
    int estado)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
        {
            return null;
        }

        // Las cuentas administrativas se gestionan
        // exclusivamente desde /api/admin/accounts.
        if (usuario.RolId == 3 || usuario.RolId == 4)
        {
            return null;
        }

        if (estado != (int)EstadoUsuario.Activo &&
            estado != (int)EstadoUsuario.Inactivo)
        {
            return null;
        }

        usuario.Estado = (EstadoUsuario)estado;
        usuario.FechaActualizacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return _mapper.Map<UserResponse>(usuario);
    }
    public async Task<PasswordChangeResult> ChangePassword(
    int userId,
    ChangePasswordRequest request)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (usuario == null)
        {
            return new PasswordChangeResult
            {
                Success = false,
                Error = "USER_NOT_FOUND"
            };
        }

        if (usuario.Estado != EstadoUsuario.Activo)
        {
            return new PasswordChangeResult
            {
                Success = false,
                Error = "USER_INACTIVE"
            };
        }

        var currentPasswordIsValid =
            BCrypt.Net.BCrypt.Verify(
                request.CurrentPassword,
                usuario.PasswordHash
            );

        if (!currentPasswordIsValid)
        {
            return new PasswordChangeResult
            {
                Success = false,
                Error = "INVALID_CURRENT_PASSWORD"
            };
        }

        var samePassword = BCrypt.Net.BCrypt.Verify(
            request.NewPassword,
            usuario.PasswordHash
        );

        if (samePassword)
        {
            return new PasswordChangeResult
            {
                Success = false,
                Error = "SAME_PASSWORD"
            };
        }

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(
            request.NewPassword
        );

        usuario.FechaActualizacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new PasswordChangeResult
        {
            Success = true
        };


    }

    public async Task<PasswordResetRequestResult> RequestPasswordReset(
    ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Correo.Trim();

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u =>
                u.Correo == normalizedEmail);

        // No revelamos si el correo existe o no.
        if (usuario == null ||
            usuario.Estado != EstadoUsuario.Activo)
        {
            return new PasswordResetRequestResult
            {
                Accepted = true
            };
        }

        // Token criptográficamente seguro.
        var tokenBytes = RandomNumberGenerator.GetBytes(32);

        var resetToken = Convert.ToBase64String(tokenBytes);

        // Guardamos únicamente el hash SHA-256.
        var tokenHashBytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(resetToken)
        );

        var tokenHash = Convert.ToHexString(
            tokenHashBytes
        );

        usuario.PasswordResetTokenHash = tokenHash;

        usuario.PasswordResetTokenExpiresAt =
            DateTime.UtcNow.AddMinutes(15);

        usuario.FechaActualizacion = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new PasswordResetRequestResult
        {
            Accepted = true,
            ResetToken = resetToken
        };
    }
}