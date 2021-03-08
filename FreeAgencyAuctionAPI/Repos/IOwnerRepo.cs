using System;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface IOwnerRepo
    {
        public Task<OwnerEntity> WinPlayer(BidDTO bid);
    }

    public class OwnerRepo : IOwnerRepo
    {
        private readonly AuctionContext _db;

        public OwnerRepo(AuctionContext db)
        {
            _db = db;
        }

        public async Task<OwnerEntity> WinPlayer(BidDTO bid)
        {
            try
            {
                var owner = await _db.Owners.FirstAsync(o => o.ownername == bid.Bidder);
                owner.yearsleft -= bid.BidLength;
                owner.caproom -= bid.BidSalary;
                _db.SaveChangesAsync();
                return owner;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}