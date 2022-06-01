using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.Extensions.Logging;

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
        Task HandleWinningTasks(BidDTO bid);
        Task<bool> ValidateBidForDbEntry(BidDTO bid);
        // Task SendWinningMessage(BidDTO bid);
    }

    public class BidLotService : IBidLotService
    {
        private readonly IMapper _mapper;
        private readonly IBidLotRepo _repo;
        private readonly IPlayerRepo _playerRepo;
        private readonly IMflService _mfl;
        private readonly IPlayerServiceLayer _pService;
        private readonly IGMBot _bot;
        private readonly ILogger<BidLotService> _logger;
        private readonly IOwnerServiceLayer _oService;


        public BidLotService(IMapper mapper, IBidLotRepo repo, IPlayerRepo playerRepo, IMflService mfl, 
            IPlayerServiceLayer pService, IOwnerServiceLayer oService, IGMBot bot, ILogger<BidLotService> logger)
        {
            _mapper = mapper;
            _repo = repo;
            _playerRepo = playerRepo;
            _mfl = mfl;
            _pService = pService;
            _oService = oService;
            _bot = bot;
            _logger = logger;
        }

        public async Task<List<LotDTO>> GetAllLots()
        {
            var rightNowUTC = DateTime.UtcNow;  // NEED TO CHECK IF any times expired 
            var preCheckedLots =  await _repo.GetAllLots();
            var deadLotsToFix = new List<Task>();
            
            preCheckedLots.ForEach(l =>
            {
                if (l?.Bid?.Expires.ToUniversalTime() < rightNowUTC)
                {
                    deadLotsToFix.Add(HandleWinningTasks(l.Bid));
                    l.Bid = null; // don't pass this bid in the lot back to client
                }
            });
            // try
            // {
            //     deadLotsToFix.ForEach(async l => await l);
            // }
            // catch (Exception e)
            // {
            //     Console.WriteLine(e);
            //     throw;
            // }
            return preCheckedLots;
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
            //TODO: check to make sure thhis is the latest bid
            var newBidEntity = _mapper.Map<BidDTO, BidEntity>(newBid);
            var res = await _repo.AddBid(newBidEntity);
            var playerEntity = await _playerRepo.GetPlayerById(newBid.Player.MflId);
            var player = _mapper.Map<PlayerDTO>(playerEntity);
            res.LotId = newBid.LotId;
            res.Player = player;
            return res;

        }

        public async Task<List<BidDTO>> GetBidHistory(string playerId)
        {
            var bids = await _repo.GetBidHistoryByPlayerId(playerId);
            Console.WriteLine(bids);
            return _mapper.Map<List<BidDTO>>(bids);
        }

        public async Task<bool> IsLatestBid(BidDTO winningBid)
        {
            var winningBidEntity = _mapper.Map<BidDTO, BidEntity>(winningBid);
            return await _repo.CheckLatestBidId(winningBidEntity);
        }

        // public async Task SendWinningMessage(BidDTO bid)
        // {
        //     // TODO: ?? make sure that this bid is actually a winner.  does the player have an owner already? etc.
        //     try
        //     {
        //         await _rabbit.SendWinMessage(new WinMessage(bid));
        //         var safeLotId = bid.LotId ?? 0;
        //         await ClearThisLot(safeLotId);
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine(e);
        //         throw;
        //     }
        // }
        
        public async Task HandleWinningTasks(BidDTO bid)
        {
            
            
            // make this a task of void and just shoot back error messages instead
            var safeLotId = bid.LotId ?? 0;
            // do two batches because Entity framework cant do async.
            // var lotTask = ClearThisLot(safeLotId);
            // var addPlayerRespTask =  _mfl.AddPlayerToTeam(bid);
            //
            // var playerTask =  _pService.WinPlayer(bid);
            // var contractTask = _mfl.GiveNewContractToPlayer(bid);
            // var capSpaceTask = _mfl.GetSalaryCapRoom();
            
            var timer = new Stopwatch();
            // TODO: we used to do the owner budget call in the win POST ... do it now with SignalR?
            try
            {

                timer.Start();

                // await Task.WhenAll(lotTask, addPlayerRespTask);
                // await Task.WhenAll(playerTask, contractTask, capSpaceTask);
                var dbPlayer = await _playerRepo.GetPlayerById(bid.Player.MflId);
                if (dbPlayer.ownerid == null || dbPlayer.ownerid > 0) return;
                var lotTask = await ClearThisLot(safeLotId);
                await _mfl.AddPlayerToTeam(bid);
                
                await _pService.WinPlayer(bid);
                await _mfl.GiveNewContractToPlayer(bid);
                var capSpaceTask = await _mfl.GetSalaryCapRoom();
                await _oService.UpdateCapSpaceForOwners(capSpaceTask.OrderBy(_ => _.ownerid).Select(c => c.caproom).ToList());
                await _oService.SendWinningMessageToChat(dbPlayer.firstname, dbPlayer.lastname, bid.BidSalary,
                    bid.BidLength, bid.Ownername);
                timer.Stop();
                _logger.LogInformation("time for winning tasks to complete: {time}", timer.Elapsed);
            }
            catch (Exception e)
            {
                try
                {
                    _logger.LogError("there was an error syncing player {bid.Player.MflId} to mfl.", bid.Player.MflId);
                    // await _bot.NotifyMflError(
                    //     new ErrorMessage($"there was an error syncing player {bid.Player.MflId} to mfl."));
                }
                catch (Exception exception)
                {
                    _logger.LogError("GM could not be notified with player win failure.");
                    // TODO: should i add the player back to the lot to retry the call? ehhh prob no cuz repeat failures
                }
                throw;
            }
        }

        public async Task<BidDTO> Nominate(BidDTO nomination)
        {
            var bidToSubmit = _mapper.Map<BidEntity>(nomination);
            var playerToAddTempOwner = new PlayerEntity
            {
                ownerid = -1,
                mflid = nomination.Player.MflId
            };
            try
            {
                var submittedBid = await _repo.AddBid(bidToSubmit);
                var playerWithTempOwner = await _playerRepo.SetPlayerOwner(playerToAddTempOwner); 
                // var playerEntity = await _playerRepo.GetPlayerById(nomination.Player.MflId);  REDUNDANT
                var player = _mapper.Map<PlayerDTO>(playerWithTempOwner);
                if (submittedBid != null && playerWithTempOwner != null)
                {
                    submittedBid.LotId = nomination.LotId;
                    submittedBid.Player = player;
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

        public async Task<bool> ValidateBidForDbEntry(BidDTO bid)
        {
            var latestBid = await _repo.GetLatestBidForPlayerId(bid.Player.MflId);
            return (latestBid.bidlength * 5) + latestBid.bidsalary < (bid.BidLength * 5) + bid.BidSalary;
        }
    }
}