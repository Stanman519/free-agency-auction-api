using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using System.Linq;

namespace FreeAgencyAuctionAPI.Mapping
{
    public class PlayerProfile : Profile
    {
        public PlayerProfile()
        {
            CreateMap<PlayerEntity, PlayerDTO>()
                .ForMember(dest => dest.ActionShot, opt => opt.MapFrom(src => src.Actionshot));
            CreateMap<PlayerDTO, PlayerEntity>();

        }
    }

    public class BidProfile : Profile
    {
        public BidProfile()
        {
            CreateMap<BidEntity, BidDTO>()
                .ForMember(dest => dest.LotId, opt => opt.MapFrom(src => src.Lots.FirstOrDefault(l => l.Bidid == src.Bidid).Lotid))
                .ForMember(dest => dest.Ownername, opt => opt.MapFrom(src => src.LeagueOwner.Owner.Ownername))
                .ForMember(dest => dest.Player, opt => opt.MapFrom((bid, bidDTO, i, context)  => context.Mapper.Map<PlayerDTO>(bid.Player)))
                .ForMember(dest => dest.BidLength, opt => opt.MapFrom(src => src.Bidlength))
                .ForMember(dest => dest.BidSalary, opt => opt.MapFrom(src => src.Bidsalary))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.Ownerid));

            CreateMap<BidDTO, BidEntity>()
                .ForMember(dest => dest.Mflid, opt => opt.MapFrom(src => src.Player.MflId))
                .ForMember(dest => dest.Bidlength, opt => opt.MapFrom(src => src.BidLength))
                .ForMember(dest => dest.Bidsalary, opt => opt.MapFrom(src => src.BidSalary))
                .ForMember(dest => dest.Ownerid, opt => opt.MapFrom(src => src.OwnerId))
                .ForMember(dest => dest.Leagueid, opt => opt.MapFrom(src => src.LeagueId))
                .ForMember(dest => dest.Player, opt => opt.Ignore())
                .ForMember(dest => dest.League, opt => opt.Ignore())
                .ForMember(dest => dest.LeagueOwner, opt => opt.Ignore())
                .ForMember(dest => dest.Ownerid, opt => opt.MapFrom(src => src.OwnerId));


        }
    }
    public class OwnerProfile : Profile
    {
        public OwnerProfile()
        {
            CreateMap<OwnerEntity, OwnerDTO>()
                .ForMember(dest => dest.Password, opt => opt.MapFrom(src => src.PasswordHash)).ReverseMap();
        }
    }

    public class LotProfile : Profile
    {
        //private readonly IMapper _mapper;

        public LotProfile()
        {
            //_mapper = mapper;
            CreateMap<LotEntity, LotDTO>()
                .ForMember(dest => dest.Bid, opt => opt.MapFrom((lot, lotDTO, i, context ) => context.Mapper.Map<BidDTO>(lot.Bid)))
                ;
            CreateMap<LotDTO, LotEntity>()
                .ForMember(dest => dest.Bid, opt => opt.MapFrom((lotDTO, lot, i, context) => context.Mapper.Map<BidEntity>(lotDTO.Bid)));
        }
    }
}