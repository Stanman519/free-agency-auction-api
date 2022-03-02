using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        Task HandleWinningTasks(BidDTO bid);
    }

    public class BidLotService : IBidLotService
    {
        private readonly IMapper _mapper;
        private readonly IBidLotRepo _repo;
        private readonly IPlayerRepo _playerRepo;
        private readonly IMflService _mfl;
        private readonly IPlayerServiceLayer _pService;
        private readonly IGMBot _bot;
        private readonly IOwnerServiceLayer _oService;


        public BidLotService(IMapper mapper, IBidLotRepo repo, IPlayerRepo playerRepo, IMflService mfl, IPlayerServiceLayer pService, IOwnerServiceLayer oService, IGMBot bot)
        {
            _mapper = mapper;
            _repo = repo;
            _playerRepo = playerRepo;
            _mfl = mfl;
            _pService = pService;
            _oService = oService;
            _bot = bot;
        }

        public async Task<List<LotDTO>> GetAllLots()
        {
            var rightNowUTC = DateTime.UtcNow;  // NEED TO CHECK IF any times expired 
            var preCheckedLots =  await _repo.GetAllLots();
            var deadLotsToFix = new List<Task>();

            preCheckedLots.ForEach(l =>
            {
                if (l?.Bid?.Expires < rightNowUTC)
                {
                    deadLotsToFix.Add(HandleWinningTasks(l.Bid));
                }
            });
            if (deadLotsToFix.Count > 0)
            {
                try
                {
                    await Task.WhenAll(deadLotsToFix);
                    return await _repo.GetAllLots();
                }
                catch (Exception e)
                {
                    //TODO: this feels wrong.  I still need to return the lots.
                    Console.WriteLine(e);
                    throw;
                }
            }
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

        public async Task HandleWinningTasks(BidDTO bid)
        {
            // make this a task of void and just shoot back error messages instead
            var safeLotId = bid.LotId ?? 0;
            var lotTask = ClearThisLot(safeLotId);
            var addPlayerRespTask =  _mfl.AddPlayerToTeam(bid);
            var playerTask =  _pService.WinPlayer(bid);
            var contractTask = _mfl.GiveNewContractToPlayer(bid);
            var capSpaceTask = _mfl.GetSalaryCapRoom();
            var taskList = new List<Task> {addPlayerRespTask, playerTask, contractTask, capSpaceTask};

            if (bid.LotId != null) taskList.Add(lotTask);
            try
            {
                await Task.WhenAll(taskList);
                await _oService.UpdateCapSpaceForOwners(capSpaceTask.Result.OrderBy(_ => _.ownerid).Select(c => c.caproom).ToList());
            }
            catch (Exception e)
            {
                try
                {
                    await _bot.NotifyMflError(
                        new ErrorMessage($"there was an error syncing player {bid.Player.MflId} to mfl"));
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
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
                var playerEntity = await _playerRepo.GetPlayerById(nomination.Player.MflId);
                var player = _mapper.Map<PlayerDTO>(playerEntity);
                if (submittedBid != null && playerWithTempOwner != null && player != null)
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
    }
}