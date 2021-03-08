using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IOwnerServiceLayer
    {
        public Task<OwnerDTO> WinPlayer(BidDTO bid);
    }
    public class OwnerServiceLayer : IOwnerServiceLayer
    {
        private readonly IMapper _mapper;
        private readonly IOwnerRepo _repo;

        public OwnerServiceLayer(IMapper mapper, IOwnerRepo repo)
        {
            _mapper = mapper;
            _repo = repo;
        }
        public async Task<OwnerDTO> WinPlayer(BidDTO bid)
        {
            var ret = await _repo.WinPlayer(bid);
            return _mapper.Map<OwnerEntity, OwnerDTO>(ret);
        }
    }
}