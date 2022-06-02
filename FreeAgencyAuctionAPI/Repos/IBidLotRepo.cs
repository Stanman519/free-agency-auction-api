using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IBidLotRepo
    {
        Task<List<LotDTO>> GetAllLots();
        Task<LotEntity> ClearThisLot(int lotId);
        Task<LotEntity> UpdateLotWithBid(LotDTO lot);
        Task<BidDTO> AddBid(BidEntity newBid);
        Task<bool> CheckLatestBidId(BidEntity winningBidEntity);
        Task<List<BidEntity>> GetBidHistoryByPlayerId(string playerId);
        Task<BidEntity> GetLatestBidForPlayerId(string mflId);
        Task SendWinMessageToDb(BidEntity map);
    }

    public class BidLotRepo : IBidLotRepo
    {
        private readonly AuctionContext _db;
        private readonly ILogger<BidLotRepo> _logger;

        public BidLotRepo(AuctionContext db, ILogger<BidLotRepo> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<LotDTO>> GetAllLots()
        {
            try
            {
                var activeBids = from lot in _db.Lots
                    join bid in _db.Bids on lot.bidid equals bid.bidid into bidResult
                    from br in bidResult.DefaultIfEmpty()
                    join player in _db.Players on br.mflid equals player.mflid into bidWithPlayer
                    from p in bidWithPlayer.DefaultIfEmpty()
                    orderby lot.lotid
                    select new LotDTO
                    {
                        LotId = lot.lotid,
                        Bid = br.bidid == null ? null : new BidDTO
                        {
                            Player = new PlayerDTO
                            {
                                Age = p.age,
                                ContractValue = p.contractvalue == null ? 0 : p.contractvalue,
                                FirstName = p.firstname,
                                FullName = p.fullname,
                                Headshot = p.headshot,
                                LastName = p.lastname,
                                Length = p.length,
                                MflId = p.mflid.ToString(),
                                Position = p.position,
                                Team = p.team,
                                ActionShot = p.actionshot
                            },
                            Expires = br.expires,
                            BidSalary = br.bidsalary == null ? 0 : br.bidsalary,
                            BidLength = br.bidlength == null ? 0 : br.bidlength,
                            Ownername = br.ownername ?? "",
                            BidId = br.bidid == null ? 0 : br.bidid,
                            LotId = lot.lotid
                        }
                    };
                return activeBids.ToList();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<LotEntity> ClearThisLot(int lotId)
        {
            try
            {
                var refreshLot = await _db.Lots.FirstAsync(l => l.lotid == lotId);
                refreshLot.bidid = null;
                await _db.SaveChangesAsync();
                return refreshLot;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<LotEntity> UpdateLotWithBid(LotDTO lot)
        {
            try
            {
                var lotToUpdate = await _db.Lots.FirstAsync(l => l.lotid == lot.LotId);
                
                lotToUpdate.bidid = lot.Bid.BidId;
                await _db.SaveChangesAsync();
                return lotToUpdate;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<List<BidEntity>> GetBidHistoryByPlayerId(string playerId)
        {
            try
            {
                var x = await _db.Bids.Where(_ => _.mflid == playerId).ToListAsync();
                return x;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
        }

        public async Task<BidDTO> AddBid(BidEntity newBid)
        {
            try
            {
                await _db.Bids.AddAsync(newBid);
                await _db.SaveChangesAsync();
                var player = await _db.Players.FirstOrDefaultAsync(p => p.mflid == newBid.mflid);
                return new BidDTO
                {
                    BidId = newBid.bidid,
                    BidLength = newBid.bidlength,
                    BidSalary = newBid.bidsalary, 
                    Ownername = newBid.ownername,
                    OwnerId = newBid.ownerid,
                    Expires = newBid.expires,
                    Player = new PlayerDTO
                    {
                        MflId = newBid.mflid.ToString(),
                        FirstName = player.firstname,
                        LastName = player.lastname
                    }
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return null;
            }
        }

        public async Task<BidEntity> GetLatestBidForPlayerId(string mflId)
        {
            try
            {
                return await _db.Bids.OrderByDescending(_ => _.bidid).FirstOrDefaultAsync(b => b.mflid == mflId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task SendWinMessageToDb(BidEntity bid)
        {
            var winMsg = new WinMsg
            {
                bidid = bid.bidid,
                bidlength = bid.bidlength,
                bidsalary = bid.bidsalary,
                expires = bid.expires,
                mflid = bid.mflid,
                ownerid = bid.ownerid,
                ownername = bid.ownername,
                proccessed = false
            };
            await _db.WinMessages.AddAsync(winMsg);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> CheckLatestBidId(BidEntity winningBidEntity)
        {
            try
            {
                var latestDbBid = await _db.Bids.OrderByDescending(_ => _.bidid)
                    .FirstOrDefaultAsync(b => b.mflid == winningBidEntity.mflid);
                return latestDbBid.bidid == winningBidEntity.bidid;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return false;
            }
        }
    }
}