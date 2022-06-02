using AutoMapper;
using FreeAgencyAuctionAPI.Models;

namespace FreeAgencyAuctionAPI.Mapping
{
    public class PlayerProfile : Profile
    {
        public PlayerProfile()
        {
            CreateMap<PlayerEntity, PlayerDTO>()
                .ForMember(dest => dest.ActionShot, opt => opt.MapFrom(src => src.actionshot));
            CreateMap<PlayerDTO, PlayerEntity>();

        }
    }

    public class BidProfile : Profile
    {
        public BidProfile()
        {
            CreateMap<BidEntity, BidDTO>()
                .ForMember(dest => dest.LotId, opt => opt.Ignore())
                .ForMember(dest => dest.Player, opt => opt.Ignore())
                .ForMember(dest => dest.BidLength, opt => opt.MapFrom(src => src.bidlength))
                .ForMember(dest => dest.BidSalary, opt => opt.MapFrom(src => src.bidsalary))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.ownerid));
            CreateMap<BidDTO, BidEntity>()
                .ForMember(dest => dest.mflid, opt => opt.MapFrom(src => src.Player.MflId))
                .ForMember(dest => dest.ownername, opt => opt.MapFrom(src => src.Ownername))
                .ForMember(dest => dest.ownerid, opt => opt.MapFrom(src => src.OwnerId))
                .ForSourceMember(src => src.LotId, opt => opt.DoNotValidate())
                .ForSourceMember(src => src.Player, opt => opt.DoNotValidate());
            CreateMap<WinMsg, BidDTO>()
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.ownerid))
                .ForMember(dest => dest.LotId, opt => opt.Ignore())
                .ForMember(dest => dest.BidSalary, opt => opt.MapFrom(src => src.bidsalary))
                .ForMember(dest => dest.BidLength, opt => opt.MapFrom(src => src.bidlength))
                .ForMember(dest => dest.BidId, opt => opt.MapFrom(src => src.bidid))
                .ForMember(dest => dest.Ownername, opt => opt.MapFrom(src => src.ownername))
                .ForMember(dest => dest.Expires, opt => opt.MapFrom(src => src.expires));
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