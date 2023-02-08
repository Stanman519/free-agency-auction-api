using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IBidLotService
    {
        public Task<List<LotDTO>> GetAllLots(int leagueId);
        public Task<LotDTO> ClearThisLot(int lotId, int leagueId, int bidId);
        public Task<LotDTO> UpdateLotWithBid(LotDTO lot);
        public Task<BidDTO> PostNewBid(BidDTO newBid);
        Task<BidDTO> Nominate(BidDTO nomination);
        Task<bool> IsLatestBid(BidDTO winningBid);
        Task<List<BidDTO>> GetBidHistory(int leagueId, string playerId);
        Task HandleWinningTasks(BidDTO bid);
        Task<bool> ValidateBidForDbEntry(BidDTO bid);
        // Task SendWinningMessage(BidDTO bid);
    }

    public class BidLotService : IBidLotService
    {
        private readonly IQueueService _queue;
        private readonly IMapper _mapper;
        private readonly IBidLotRepo _repo;
        private readonly IGMBot _bot;
        private readonly ILogger<BidLotService> _logger;


        public BidLotService(IMapper mapper, IQueueService queue, IBidLotRepo repo, IGMBot bot, ILogger<BidLotService> logger)
        {
            _mapper = mapper;
            _repo = repo;
            _bot = bot;
            _queue = queue;
            _logger = logger;
        }

        public async Task<List<LotDTO>> GetAllLots(int leagueId)
        {
            var rightNowUTC = DateTime.UtcNow;  // NEED TO CHECK IF any times expired 
            var preCheckedLots =  await _repo.GetAllLots(leagueId);
            var deadLotsToFix = new List<Task>();
            
            preCheckedLots.ForEach(l =>
            {
                if (l?.Bid?.Expires < rightNowUTC)
                {
                    deadLotsToFix.Add(HandleWinningTasks(l.Bid));
                    l.Bid = null; // don't pass this bid in the lot back to client
                }
            });
            try
            {
                deadLotsToFix.ForEach(async l => await l);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "automated win error");
            }
            return preCheckedLots;
        }

        public async Task<LotDTO> ClearThisLot(int lotId, int leagueId, int bidId)
        {
            var ret = await _repo.ClearThisLot(lotId, leagueId, bidId);
            return _mapper.Map<LotEntity, LotDTO>(ret);
        }

        public async Task<LotDTO> UpdateLotWithBid(LotDTO lot)
        {
            var ret = await _repo.UpdateLotWithBid(lot);
            return _mapper.Map<LotEntity, LotDTO>(ret);
        }

        public async Task<BidDTO> PostNewBid(BidDTO newBid)
        {
            var res = await _repo.AddBid(newBid);
            //var playerEntity = await _playerRepo.GetPlayerById(newBid.Player.MflId);
            var bid = _mapper.Map<BidDTO>(res);
            return bid;

        }

        public async Task<List<BidDTO>> GetBidHistory(int leagueId,string playerId)
        {
            return await _repo.GetBidHistoryByPlayerId(leagueId, playerId);
        }

        public async Task<bool> IsLatestBid(BidDTO winningBid)
        {
            var winningBidEntity = _mapper.Map<BidDTO, BidEntity>(winningBid);
            return await _repo.CheckLatestBidId(winningBidEntity);
        }
        
        public async Task HandleWinningTasks(BidDTO bid)
        {
            var safeLotId = bid.LotId ?? 0;

            await ClearThisLot(safeLotId, bid.LeagueId, bid.BidId);

            try
            {
                var msg = JsonConvert.SerializeObject(bid);
                _queue.SendMessageToQueue(bid);
                //if (string.IsNullOrEmpty(res.Value.MessageId)) _logger.LogError("win message error", bid.BidId);
            }
            catch (Exception e)
            {
                try
                {
                    _logger.LogError("there was an error syncing player {bid.Player.MflId} to mfl.", e);
                    await _bot.NotifyMflError(
                        new ErrorMessage($"there was an error syncing player {bid.Player.MflId} to mfl."));
                }
                catch (Exception exception)
                {
                    _logger.LogError("GM could not be notified with player win failure.", exception);
                }
                return;
            }
            finally
            {
                
            }
        }

        public async Task<BidDTO> Nominate(BidDTO nomination)
        {
            try
            {
                var submittedBid = await _repo.AddBid(nomination);
                //var playerEntity = await _playerRepo.GetPlayerById(nomination.Player.MflId);
                if (submittedBid != null)
                {
                    //submittedBid.LotId = nomination.LotId;
                    //submittedBid.Player = _mapper.Map<PlayerDTO>(playerEntity);
                    return _mapper.Map<BidDTO>(submittedBid);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "nomination error!");
                return null;
            }

            return null;
        }
/*        public async Task PostNewBidChangesToGroup()
        {
            var strForBot = "Players with new bids in the last hour:\n";
            var bidsFromLastHour = await _repo.GetNewBidsFromTheLastHour();
            var emptyLots = (await _repo.GetAllLots()).Where(l => l.Bid == null).ToList();
            var bidsGroupedByPlayer = bidsFromLastHour.GroupBy(b => b.Player.MflId).ToList();
            if (!bidsGroupedByPlayer.Any()) return;
            foreach (var bid in bidsGroupedByPlayer)
            {
                var latestBid = bid.OrderByDescending(b => b.Expires).First();

                strForBot +=
                    $"{latestBid.Player.LastName} - ${latestBid.BidSalary} ({latestBid.Ownername})\n";
            }

            if (emptyLots.Any())
            {
                strForBot += "\nNeeds to nominate:\n";
                foreach (var lot in emptyLots)
                {
                    strForBot += $"{Utils.Owners[lot.LotId]}\n";
                }
            }

            await _bot.NotifyMflError(new ErrorMessage(strForBot));
        }*/
        public async Task<bool> ValidateBidForDbEntry(BidDTO bid)
        {
            var latestBid = await _repo.GetLatestBidForPlayerId(bid.Player.MflId, bid.LeagueId);
            return (latestBid.Bidlength * 5) + latestBid.Bidsalary < (bid.BidLength * 5) + bid.BidSalary;
        }
    }
}