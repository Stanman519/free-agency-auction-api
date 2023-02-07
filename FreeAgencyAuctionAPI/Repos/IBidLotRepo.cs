using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IBidLotRepo
    {
        Task<List<LotDTO>> GetAllLots(int leagueId);
        Task<LotEntity> ClearThisLot(int lotId, int leagueId, int bidId);
        Task<LotEntity> UpdateLotWithBid(LotDTO lot);
        Task<BidEntity> AddBid(BidDTO newBid);
        Task<bool> CheckLatestBidId(BidEntity winningBidEntity);
        Task<List<BidDTO>> GetBidHistoryByPlayerId(int leagueId, string playerId);
        Task<BidEntity> GetLatestBidForPlayerId(int mflId, int leagueId);
    }

    public class BidLotRepo : IBidLotRepo
    {
        private readonly AuctionContext _db;
        private readonly ILogger<BidLotRepo> _logger;
        private readonly IMapper _mapper;

        public BidLotRepo(AuctionContext db, ILogger<BidLotRepo> logger, IMapper mapper)
        {
            _db = db;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<List<LotDTO>> GetAllLots(int leagueId)
        {
            try
            {
                var activeBids = await _db.Lots.Where(_ => _.Leagueid == leagueId).ToListAsync();
                var lots = _mapper.Map<List<LotDTO>>(activeBids);
                return lots;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error fetching lots");
                return null;
            }
        }

        public async Task<LotEntity> ClearThisLot(int lotId, int leagueId, int bidId)
        {
            try
            {
                var refreshLot = await _db.Lots.FirstAsync(l => l.Lotid == lotId && l.Leagueid == leagueId && l.Bidid == bidId);
                if (refreshLot != null) 
                {
                    refreshLot.Bidid = null;
                    await _db.SaveChangesAsync();
                }

                return refreshLot;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error clearing lot");
                return null;
            }
        }

        public async Task<LotEntity> UpdateLotWithBid(LotDTO lot)
        {
            try
            {
                var lotToUpdate = await _db.Lots.FirstAsync(l => l.Lotid == lot.LotId && l.Leagueid == lot.LeagueId);
                
                lotToUpdate.Bidid = lot.Bid.BidId;
                await _db.SaveChangesAsync();
                return lotToUpdate;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating lot");
                return null;
            }
        }

        public async Task<List<BidDTO>> GetBidHistoryByPlayerId(int leagueId, string playerId)
        {
            try
            {
                var x = await _db.Bids.Where(_ => _.Mflid == int.Parse(playerId) && _.Leagueid == leagueId)
                    .Select(b => new BidDTO
                    {
                        Ownername = b.LeagueOwner.Owner.Ownername,
                        BidId = b.Bidid,
                        BidLength = b.Bidlength,
                        BidSalary = b.Bidsalary,
                        OwnerId = b.Ownerid,
                        Expires = b.Expires,
                        LeagueId = b.Leagueid,
                        Player = _mapper.Map<PlayerDTO>(b.Player)
                    }).ToListAsync();
                    return x;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error gettin bid history" );
                throw;
            }
        }

        public async Task<BidEntity> AddBid(BidDTO newBid)
        {
            try
            {
                var bidEntity = _db.Bids.CreateProxy();
                bidEntity.Bidlength = newBid.BidLength;
                bidEntity.Bidsalary = newBid.BidSalary;
                bidEntity.Mflid = newBid.Player.MflId;
                bidEntity.Expires = newBid.Expires;
                bidEntity.Leagueid = newBid.LeagueId;
                bidEntity.Ownerid = newBid.OwnerId;
                //bidEntity = _mapper.Map<BidEntity>(newBid);
                await _db.Bids.AddAsync(bidEntity);
                await _db.SaveChangesAsync();
                var x = bidEntity.Player;
                return bidEntity;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "add bid error");
                return null;
            }
        }

        public async Task<BidEntity> GetLatestBidForPlayerId(int mflId, int leagueId)
        {
            try
            {
                return await _db.Bids.OrderByDescending(_ => _.Bidid).FirstOrDefaultAsync(b => b.Mflid == mflId && b.Leagueid == leagueId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get latest bid error");
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
                var latestDbBid = await _db.Bids.OrderByDescending(_ => _.Bidid).FirstOrDefaultAsync(b => b.Mflid == winningBidEntity.Mflid && b.Leagueid == winningBidEntity.Leagueid);
                return latestDbBid.Bidid == winningBidEntity.Bidid;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "latest bid verify error");
                return false;
            }
        }

        public async Task<List<WinMsg>> GetAllWinMessages()
        {
            try
            {
                return await _db.WinMessages.ToListAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task MarkAllWinMessagesAsProcessed(int bidId)
        {
            try
            {
                var winsToChange = await _db.WinMessages.Where(w => w.bidid == bidId).ToListAsync();
                winsToChange.ForEach(w => w.proccessed = true);
                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<List<BidDTO>> GetNewBidsFromTheLastHour()
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            try
            {
                return await _db.Bids.Where(b => b.expires >= oneHourAgo.AddHours(12)).Join(_db.Players, b => b.mflid, p => p.mflid, (b,p) => new BidDTO
                {
                    BidLength = b.bidlength,
                    BidSalary = b.bidsalary,
                    BidId = b.bidid,
                    Expires = b.expires,
                    OwnerId = b.ownerid,
                    Ownername = b.ownername,
                    Player = new PlayerDTO
                    {
                        FirstName = p.firstname,
                        LastName = p.lastname,
                        Position = p.position,
                        MflId = p.mflid
                    }
                }).ToListAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}