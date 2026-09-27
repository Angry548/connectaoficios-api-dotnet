using AutoMapper;
using ConnectaOficios.Api.DTOs.Admins;
using ConnectaOficios.Api.DTOs.Common;
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

    public async Task<PagedResult<UserResponse>> GetAll(
        string? nombre = null,
        string? correo = null,
        int? rolId = null,
        int? estado = null,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Where(u =>
                u.RolId == 3 ||
                u.RolId == 4)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var nombreFiltro = nombre.Trim();

            query = query.Where(u =>
                EF.Functions.Like(
                    u.Nombre,
                    $"%{nombreFiltro}%"
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(correo))
        {
            var correoFiltro = correo.Trim();

            query = query.Where(u =>
                EF.Functions.Like(
                    u.Correo,
                    $"%{correoFiltro}%"
                )
            );
        }

        if (rolId.HasValue)
        {
            query = query.Where(u =>
                u.RolId == rolId.Value
            );
        }

        if (estado.HasValue)
        {
            query = query.Where(u =>
                (int)u.Estado == estado.Value
            );
        }

        var totalItems = await query.CountAsync();

        var administradores = await query
            .OrderBy(u => u.Nombre)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = _mapper
            .Map<IEnumerable<UserResponse>>(administradores)
            .ToList();

        return new PagedResult<UserResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(
                    totalItems / (double)pageSize
                )
        };
    }

    public async Task<IEnumerable<UserSearchResponse>> Search(
        string texto,
        int? rolId = null,
        int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var termino = texto.Trim();

        if (termino.Length < 2)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 20);

        var query = _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .Where(u =>
                (u.RolId == 3 || u.RolId == 4) &&
                u.Estado == EstadoUsuario.Activo)
            .AsQueryable();

        if (rolId.HasValue)
        {
            query = query.Where(u =>
                u.RolId == rolId.Value
            );
        }

        query = query.Where(u =>
            EF.Functions.Like(
                u.Nombre,
                $"%{termino}%"
            ) ||
            EF.Functions.Like(
                u.Correo,
                $"%{termino}%"
            )
        );

        return await query
            .OrderBy(u => u.Nombre)
            .ThenBy(u => u.Id)
            .Take(limit)
            .Select(u => new UserSearchResponse
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Correo = u.Correo,
                RolId = u.RolId,
                Rol = u.Rol.Nombre
            })
            .ToListAsync();
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
        if (admin.RolId != 3 &&
            admin.RolId != 4)
        {
            return null;
        }

        var normalizedEmail = admin.Correo.Trim();

        var emailExists = await _db.Usuarios
            .AnyAsync(u =>
                u.Correo == normalizedEmail);

        if (emailExists)
        {
            return null;
        }

        var entity = new Usuario
        {
            Nombre = admin.Nombre.Trim(),
            Correo = normalizedEmail,
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

    public async Task<AdminOperationResult> Update(
        int id,
        AdminAccountUpdateRequest admin)
    {
        var entity = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                (u.RolId == 3 || u.RolId == 4));

        if (entity == null)
        {
            return new AdminOperationResult
            {
                Success = false,
                Error = "NOT_FOUND"
            };
        }

        if (admin.Nombre == null &&
            admin.Correo == null &&
            admin.Telefono == null)
        {
            return new AdminOperationResult
            {
                Success = false,
                Error = "NO_FIELDS"
            };
        }

        if (admin.Nombre != null)
        {
            if (string.IsNullOrWhiteSpace(
                admin.Nombre))
            {
                return new AdminOperationResult
                {
                    Success = false,
                    Error = "INVALID_NAME"
                };
            }

            entity.Nombre = admin.Nombre.Trim();
        }

        if (admin.Correo != null)
        {
            if (string.IsNullOrWhiteSpace(
                admin.Correo))
            {
                return new AdminOperationResult
                {
                    Success = false,
                    Error = "INVALID_EMAIL"
                };
            }

            var normalizedEmail =
                admin.Correo.Trim();

            var emailExists = await _db.Usuarios
                .AnyAsync(u =>
                    u.Correo == normalizedEmail &&
                    u.Id != id);

            if (emailExists)
            {
                return new AdminOperationResult
                {
                    Success = false,
                    Error = "EMAIL_EXISTS"
                };
            }

            entity.Correo = normalizedEmail;
        }

        if (admin.Telefono != null)
        {
            entity.Telefono =
                string.IsNullOrWhiteSpace(
                    admin.Telefono)
                    ? null
                    : admin.Telefono.Trim();
        }

        entity.FechaActualizacion =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new AdminOperationResult
        {
            Success = true,
            Admin = _mapper.Map<UserResponse>(
                entity
            )
        };
    }

    public async Task<AdminOperationResult> ChangeStatus(
        int id,
        int estado,
        int currentUserId)
    {
        if (estado !=
                (int)EstadoUsuario.Activo &&
            estado !=
                (int)EstadoUsuario.Inactivo)
        {
            return new AdminOperationResult
            {
                Success = false,
                Error = "INVALID_STATUS"
            };
        }

        var administrador = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                (u.RolId == 3 ||
                 u.RolId == 4));

        if (administrador == null)
        {
            return new AdminOperationResult
            {
                Success = false,
                Error = "NOT_FOUND"
            };
        }

        if (administrador.Id ==
                currentUserId &&
            estado ==
                (int)EstadoUsuario.Inactivo)
        {
            return new AdminOperationResult
            {
                Success = false,
                Error = "CANNOT_DISABLE_SELF"
            };
        }

        if (administrador.RolId == 4 &&
            estado ==
                (int)EstadoUsuario.Inactivo &&
            administrador.Estado ==
                EstadoUsuario.Activo)
        {
            var activePrincipals =
                await _db.Usuarios.CountAsync(u =>
                    u.RolId == 4 &&
                    u.Estado ==
                        EstadoUsuario.Activo);

            if (activePrincipals <= 1)
            {
                return new AdminOperationResult
                {
                    Success = false,
                    Error =
                        "LAST_ACTIVE_PRINCIPAL"
                };
            }
        }

        administrador.Estado =
            (EstadoUsuario)estado;

        administrador.FechaActualizacion =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new AdminOperationResult
        {
            Success = true,
            Admin = _mapper.Map<UserResponse>(
                administrador
            )
        };
    }
}