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
            CreateMap<BidEntity, BidDTO>();
            CreateMap<BidDTO, BidEntity>();
        }
    }
    public class OwnerProfile : Profile
    {
        public OwnerProfile()
        {
            CreateMap<OwnerEntity, OwnerDTO>();
            CreateMap<OwnerDTO, OwnerEntity>();
        }
    }
}