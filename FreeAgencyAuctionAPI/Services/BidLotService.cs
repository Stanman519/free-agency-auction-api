using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IBidLotService
    {
        public Task<List<BidDTO>> GetActiveBids();
        public Task<LotDTO> ClearThisLot(int lotId);
        public Task<LotDTO> UpdateLotWithBid(LotDTO lot);
    }

    public class BidLotService : IBidLotService
    {
        private readonly IMapper _mapper;
        private readonly IBidLotRepo _repo;

        public BidLotService(IMapper mapper, IBidLotRepo repo)
        {
            _mapper = mapper;
            _repo = repo;
        }

        public async Task<List<BidDTO>> GetActiveBids()
        {
            return await _repo.GetActiveBids();
        }

        public async Task<LotDTO> ClearThisLot(int lotId)
        {
            var ret = await _repo.ClearThisLot(lotId);
            return _mapper.Map<LotEntity, LotDTO>(ret);
        }

        public async Task<LotDTO> UpdateLotWithBid(LotDTO lot)
        {
            var ret = await _repo.UpdateLotWithBid(lot);
            return _mapper.Map<LotEntity, LotDTO>(ret);
        }
    }
}