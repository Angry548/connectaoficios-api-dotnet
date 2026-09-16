using AutoMapper;
using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Domain.Entities;

namespace ConnectaOficios.Api.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Modelo -> DTO
        CreateMap<Usuario, UserResponse>()
            .ForMember(
                dest => dest.Rol,
                opt => opt.MapFrom(src => src.Rol.Nombre)
            )
            .ForMember(
                dest => dest.Estado,
                opt => opt.MapFrom(src => src.Estado.ToString())
            );

        // DTO -> Modelo
        CreateMap<UserRequest, Usuario>()
            .ForMember(
                dest => dest.PasswordHash,
                opt => opt.Ignore()
            )
            .ForMember(
                dest => dest.Rol,
                opt => opt.Ignore()
            )
            .ForMember(
                dest => dest.Estado,
                opt => opt.Ignore()
            )
            .ForMember(
                dest => dest.FechaCreacion,
                opt => opt.Ignore()
            )
            .ForMember(
                dest => dest.FechaActualizacion,
                opt => opt.Ignore()
            )
            .ForMember(
                dest => dest.UltimoAcceso,
                opt => opt.Ignore()
            );
    }
}