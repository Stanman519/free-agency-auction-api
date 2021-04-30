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
        Task<BidDTO> AddBid(BidEntity newBid);
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
                var activeBids = from l in _db.Lots
                    join b in _db.Bids on l.bidid equals b.bidid
                    join p in _db.Players on b.playerid equals p.playerid
                    select new BidDTO
                    {
                        PlayerId = b.playerid,
                        Expires = b.expires,
                        BidSalary = b.bidsalary,
                        BidLength = b.bidlength,
                        Ownername = b.ownername,
                        BidId = b.bidid,
                        LotId = l.lotid,
                        PlayerFirstName = p.firstname,
                        PlayerLastName = p.lastname
                    };
                return activeBids.ToList();
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

        public async Task<BidDTO> AddBid(BidEntity newBid)
        {
            try
            {
                await _db.Bids.AddAsync(newBid);
                await _db.SaveChangesAsync();
                var player = await _db.Players.FirstOrDefaultAsync(p => p.playerid == newBid.playerid);
                return new BidDTO
                {
                    BidId = newBid.bidid,
                    BidLength = newBid.bidlength,
                    BidSalary = newBid.bidsalary,
                    PlayerId = newBid.playerid,
                    Ownername = newBid.ownername,
                    Expires = newBid.expires,
                    PlayerFirstName = player.firstname,
                    PlayerLastName = player.lastname
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}