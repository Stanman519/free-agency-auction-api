using AutoMapper;
using FreeAgencyAuctionAPI.Models;

namespace FreeAgencyAuctionAPI.Mapping
{
    public class PlayerProfile : Profile
    {
        public PlayerProfile()
        {
            CreateMap<PlayerEntity, PlayerDTO>();
            CreateMap<PlayerDTO, PlayerEntity>();

        }
    }

    public class BidProfile : Profile
    {
        public BidProfile()
        {
            CreateMap<BidEntity, BidDTO>()
                .ForMember(dest => dest.LotId, opt => opt.Ignore())
                .ForMember(dest => dest.PlayerLastName, opt => opt.Ignore())
                .ForMember(dest => dest.PlayerFirstName, opt => opt.Ignore());
            CreateMap<BidDTO, BidEntity>()
                .ForSourceMember(src => src.LotId, opt => opt.DoNotValidate())
                .ForSourceMember(src => src.PlayerFirstName, opt => opt.DoNotValidate())
                .ForSourceMember(src => src.PlayerLastName, opt => opt.DoNotValidate());
        }
    }
    public class OwnerProfile : Profile
    {
        public OwnerProfile()
        {
            CreateMap<OwnerEntity, OwnerDTO>()
                .ForMember(dest => dest.Password, opt => opt.MapFrom(src => src.password_hash));
            CreateMap<OwnerDTO, OwnerEntity>()
                .ForMember(dest => dest.password_hash, opt => opt.MapFrom(src => src.Password));
        }
    }

    public class LotProfile : Profile
    {
        public LotProfile()
        {
            CreateMap<LotEntity, LotDTO>();
            CreateMap<LotDTO, LotEntity>();
        }
    }
}