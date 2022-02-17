using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IBidLotService
    {
        public Task<List<LotDTO>> GetAllLots();
        public Task<LotDTO> ClearThisLot(int lotId);
        public Task<LotDTO> UpdateLotWithBid(LotDTO lot);
        public Task<BidDTO> PostNewBid(BidDTO newBid);
        Task<BidDTO> Nominate(BidDTO nomination);
        Task<bool> IsLatestBid(BidDTO winningBid);
        Task<List<BidDTO>> GetBidHistory(string playerId);
    }

    public class BidLotService : IBidLotService
    {
        private readonly IMapper _mapper;
        private readonly IBidLotRepo _repo;
        private readonly IPlayerRepo _playerRepo;

        public BidLotService(IMapper mapper, IBidLotRepo repo, IPlayerRepo playerRepo)
        {
            _mapper = mapper;
            _repo = repo;
            _playerRepo = playerRepo;
        }

        public async Task<List<LotDTO>> GetAllLots()
        {
            return await _repo.GetAllLots();
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

        public async Task<BidDTO> PostNewBid(BidDTO newBid)
        {
            var newBidEntity = _mapper.Map<BidDTO, BidEntity>(newBid);
            var res = await _repo.AddBid(newBidEntity);
            res.LotId = newBid.LotId;
            return res;

        }

        public async Task<List<BidDTO>> GetBidHistory(string playerId)
        {
            var bids = await _repo.GetBidHistoryByPlayerId(playerId);
            return _mapper.Map<List<BidDTO>>(bids);
        }

        public async Task<bool> IsLatestBid(BidDTO winningBid)
        {
            var winningBidEntity = _mapper.Map<BidDTO, BidEntity>(winningBid);
            return await _repo.CheckLatestBidId(winningBidEntity);
        }

        public async Task<BidDTO> Nominate(BidDTO nomination)
        {
            var bidToSubmit = new BidEntity
            {
                mflid = nomination.Player.MflId,
                ownername = nomination.Ownername,
                bidlength = nomination.BidLength,
                bidsalary = nomination.BidSalary,
                expires = nomination.Expires
            };
            var playerToAddTempOwner = new PlayerEntity
            {
                ownerid = -1,
                mflid = nomination.Player.MflId
            };

            try
            {
                var submittedBid = await _repo.AddBid(bidToSubmit);
                var playerWithTempOwner = await _playerRepo.SetPlayerOwner(playerToAddTempOwner);
                if (submittedBid != null && playerWithTempOwner != null)
                {
                    submittedBid.LotId = nomination.LotId;
                    return submittedBid;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

            return null;
        }
    }
}