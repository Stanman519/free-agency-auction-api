using AutoMapper;
using FreeAgencyAuctionAPI.Models;

namespace FreeAgencyAuctionAPI.Mapping
{
    public class PlayerProfile : Profile
    {
        public PlayerProfile()
        {
            CreateMap<PlayerEntity, PlayerDTO>();
                // .ForMember(dest => dest.length,
                //     opts => opts.MapFrom(src => src.length))
                // .ForMember(dest => dest.position,
                //     opts => opts.MapFrom(src => src.position))
                // .ForMember(dest => dest.salary,
                //     opts => opts.MapFrom(src => src.salary))
               
            
        }
    }
}