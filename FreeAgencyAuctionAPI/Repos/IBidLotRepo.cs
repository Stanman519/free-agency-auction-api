using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IBidLotRepo
    {
        Task<List<BidDTO>> GetActiveBids();
        Task<LotEntity> ClearThisLot(int lotId);
        Task<LotEntity> UpdateLotWithBid(LotDTO lot);
    }

    public class BidLotRepo : IBidLotRepo
    {
        private readonly AuctionContext _db;

        public BidLotRepo(AuctionContext db)
        {
            _db = db;
        }

        public async Task<List<BidDTO>> GetActiveBids()
        {
            try
            {
                var activeBids = await _db.Lots
                    .Join(_db.Bids, l => l.bidid, b => b.bidid, (l, b) => new BidDTO
                    {
                        PlayerId = b.playerid,
                        Expires = b.expires,
                        BidSalary = b.bidsalary,
                        BidLength = b.bidlength,
                        Bidder = b.ownername,
                        BidId = b.bidid,
                        LotId = l.lotid
                    }).ToListAsync();
                return activeBids;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public async Task<LotEntity> UpdateLotWithBid(LotDTO lot)
        {
            try
            {
                var lotToUpdate = await _db.Lots.FirstAsync(l => l.lotid == lot.LotId);
                lotToUpdate.bidid = lot.BidId;
                await _db.SaveChangesAsync();
                return lotToUpdate;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}