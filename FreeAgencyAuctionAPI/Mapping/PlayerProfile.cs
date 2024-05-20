using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Models.Confidence;
using FreeAgencyAuctionAPI.OverUnders;
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
    public class MatchupProfile : Profile
    {
        public MatchupProfile()
        {
            CreateMap<NflMatchupDTO, NflTeamMatchup>()
                .ForMember(dest => dest.Year, opt => opt.MapFrom(src => src.Year))
                .ForMember(dest => dest.Week, opt => opt.MapFrom(src => src.Week))
                .ForMember(dest => dest.Pickable, opt => opt.MapFrom(src => src.Pickable))
                .ForMember(dest => dest.Right, opt => opt.MapFrom(src => src.Right.Tricode))
                .ForMember(dest => dest.Winner, opt => opt.MapFrom(src => src.Winner.Tricode))
                .ForMember(dest => dest.Left, opt => opt.MapFrom(src => src.Left.Tricode));
            CreateMap<NflTeamMatchup, NflMatchupDTO>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Week, opt => opt.MapFrom(src => src.Week))
                .ForMember(dest => dest.Year, opt => opt.MapFrom(src => src.Year))
                .ForMember(dest => dest.Pickable, opt => opt.MapFrom(src => src.Pickable))
                .ForMember(dest => dest.Right, opt => opt.MapFrom(src => src.RightTeam))
                .ForMember(dest => dest.Winner, opt => opt.MapFrom(src => src.WinningTeam))
                .ForMember(dest => dest.Left, opt => opt.MapFrom(src => src.LeftTeam));
        }
    }
    public class NflPickProfile : Profile
    {
        public NflPickProfile()
        {
            CreateMap<NflPicksDTO, Pick>()
                .ForMember(dest => dest.Choice, opt => opt.MapFrom(src => src.Choice))
                .ForMember(dest => dest.MatchupId, opt => opt.MapFrom(src => src.MatchupId))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.OwnerId))
                .ForMember(dest => dest.Points, opt => opt.MapFrom(src => src.Points));
            CreateMap<Pick, NflPicksDTO>()
                .ForMember(dest => dest.Choice, opt => opt.MapFrom(src => src.Choice))
                .ForMember(dest => dest.MatchupId, opt => opt.MapFrom(src => src.MatchupId))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.OwnerId))
                .ForMember(dest => dest.Points, opt => opt.MapFrom(src => src.Points))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id));
        }
    }

    public class NflPropProfile : Profile
    {
        public NflPropProfile()
        {
            CreateMap<PropPickDTO, ExtraPick>()
                .ForMember(dest => dest.Choice, opt => opt.MapFrom(src => src.Choice))
                .ForMember(dest => dest.PropId, opt => opt.MapFrom(src => src.PropId))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.OwnerId));
            CreateMap<ExtraPick, PropPickDTO>()
                .ForMember(dest => dest.Choice, opt => opt.MapFrom(src => src.Choice))
                .ForMember(dest => dest.PropId, opt => opt.MapFrom(src => src.PropId))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.OwnerId))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id));
            CreateMap<PropDTO, Prop>()
                .ForMember(dest => dest.Year, opt => opt.MapFrom(src => src.Year))
                .ForMember(dest => dest.Week, opt => opt.MapFrom(src => src.Week))
                .ForMember(dest => dest.Pickable, opt => opt.MapFrom(src => src.Pickable))
                .ForMember(dest => dest.OptionB, opt => opt.MapFrom(src => src.OptionB))
                .ForMember(dest => dest.Winner, opt => opt.MapFrom(src => src.Winner))
                .ForMember(dest => dest.OptionA, opt => opt.MapFrom(src => src.OptionA));
            CreateMap<Prop, PropDTO>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Week, opt => opt.MapFrom(src => src.Week))
                .ForMember(dest => dest.Year, opt => opt.MapFrom(src => src.Year))
                .ForMember(dest => dest.Pickable, opt => opt.MapFrom(src => src.Pickable))
                .ForMember(dest => dest.OptionB, opt => opt.MapFrom(src => src.OptionB))
                .ForMember(dest => dest.Winner, opt => opt.MapFrom(src => src.Winner))
                .ForMember(dest => dest.OptionA, opt => opt.MapFrom(src => src.OptionA));
        }
    }
    public class NflTeamProfile : Profile
    {
        public NflTeamProfile()
        {
            CreateMap<NflTeam, NflTeamDTO>()
                .ReverseMap();
        }
    }
    public class OverUnderPickProfile : Profile
    {
        public OverUnderPickProfile()
        {
            CreateMap<OverUnderPickDTO, OverUnderPick>();
        }
    }
    public class NflTeamBaseProfile : Profile
    {
        public NflTeamBaseProfile()
        {
            CreateMap<NflTeam, NflTeamBaseDTO>()
                .ReverseMap();
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
                .ForMember(dest => dest.Expires, opt => opt.MapFrom(src => src.Expires))
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
                .ForMember(dest => dest.ConfidencePaid, opt => opt.MapFrom(src => src.ConfidencePaid))
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
                .ForMember(dest => dest.NominatedBy, opt => opt.MapFrom(src => src.Nominatedby))
                .ForMember(dest => dest.Bid, opt => opt.MapFrom((lot, lotDTO, i, context ) => context.Mapper.Map<BidDTO>(lot.Bid)));
            CreateMap<LotDTO, LotEntity>()
                .ForMember(dest => dest.Nominatedby, opt => opt.MapFrom(src => src.NominatedBy))
                .ForMember(dest => dest.Bid, opt => opt.MapFrom((lotDTO, lot, i, context) => context.Mapper.Map<BidEntity>(lotDTO.Bid)));
        }
    }
    public class TransactionProfile : Profile
    {
        public TransactionProfile()
        {
            CreateMap<Transaction, TransactionDTO>();
            CreateMap<TransactionDTO, Transaction>()
             .ForMember(dest => dest.Leagueid,
                 opts => opts.MapFrom(src => src.LeagueId))
                // .ForMember(dest => dest.Transactionid,
                // opts => opts.MapFrom(src => src.TransactionId))
                // .ForMember(dest => dest.Timestamp,
                //     opts => opts.MapFrom(src => src.Timestamp))
                // .ForMember(dest => dest.Franchiseid,
                //     opts => opts.MapFrom(src => src.FranchiseId))
                // .ForMember(dest => dest.Salary,
                //     opts => opts.MapFrom(src => src.Salary))
                // .ForMember(dest => dest.Amount,
                //     opts => opts.MapFrom(src => src.Amount))
                // .ForMember(dest => dest.Playername,
                //     opts => opts.MapFrom(src => src.PlayerName))
                // .ForMember(dest => dest.Position,
                //     opts => opts.MapFrom(src => src.Position))
                // .ForMember(dest => dest.Team,
                //     opts => opts.MapFrom(src => src.Team))
                // .ForMember(dest => dest.Years,
                //     opts => opts.MapFrom(src => src.Years))
                // .ForMember(dest => dest.Yearoftransaction,
                //     opts => opts.MapFrom(src => src.YearOfTransaction))
                ;
        }
    }
}