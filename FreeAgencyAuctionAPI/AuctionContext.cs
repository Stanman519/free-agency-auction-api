using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FreeAgencyAuctionAPI
{
    public class AuctionContext : DbContext
    {
        public DbSet<PlayerEntity> Players { get; set; }
        public DbSet<BidEntity> Bids { get; set; }
        public DbSet<OwnerEntity> Owners { get; set; }

        public AuctionContext(DbContextOptions<AuctionContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(
                    @"Server=ec2-54-161-150-170.compute-1.amazonaws.com;Port=5432;Database=dacgk47k91p2vs;User Id=REDACTED_HEROKU_PG_USER;Password=REDACTED_HEROKU_PG_PW");
            }
        }
    }

        [Table("player")]
        public class PlayerEntity
        {
            [Key] 
            public int playerid { get; set; }
            public int espnid { get; set; }
            public int? ownerid { get; set; }
            public string? ownername { get; set; }
            public string firstname { get; set; }
            public string? lastname { get; set; }
            public string position { get; set; }
            public int? salary { get; set; }
            public int? length { get; set; }
            public int? contractvalue { get; set; }
            
        }

        [Table("bidledger")]
        public class BidEntity
        {
            [Key] 
            public int bidid { get; set; }
            public int? playerid { get; set; }
            public string? ownername { get; set; }
            public int? bidlength { get; set; }
            public int? bidsalary { get; set; }
            public DateTime? expires { get; set; }
        }
        [Table("owner")]
        public class OwnerEntity
        {
            public int ownerid { get; set; }
            public string ownername { get; set; }
            public string passwordhash { get; set; }
            public string email { get; set; }
            public int caproom { get; set; }
            public int yearsleft { get; set; }
        }
}