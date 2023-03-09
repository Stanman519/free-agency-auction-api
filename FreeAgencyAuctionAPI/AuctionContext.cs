using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI
{
    public partial class AuctionContext : DbContext
    {
        public DbSet<PlayerEntity> Players { get; set; }
        public DbSet<BidEntity> Bids { get; set; }
        public DbSet<OwnerEntity> Owners { get; set; }
        public DbSet<LotEntity> Lots { get; set; }
        public DbSet<SuggestionEntity> Suggestions { get; set; }
        public DbSet<LeagueOwnerEntity> LeagueOwners { get; set; }
        public DbSet<LeagueEntity> Leagues { get; set; }
        public DbSet<ContractEntity> Contracts { get; set; }
        public DbSet<FranchiseTagPlayer> FranchiseTagPlayers { get; set; }
        public DbSet<FranchiseTagLeague> FranchiseTagLeagues { get; set; }
        public DbSet<Buyout> Buyouts { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }


        public AuctionContext(DbContextOptions<AuctionContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Globalid).HasName("PK_transaction");

                entity.ToTable("transaction");

                entity.Property(e => e.Globalid).HasColumnName("globalid");
                entity.Property(e => e.Timestamp).HasColumnType("datetime").HasColumnName("timestamp");
                entity.Property(e => e.Transactionid).HasColumnName("transactionid");
                entity.Property(e => e.Leagueid).HasColumnName("leagueid");
                entity.Property(e => e.Franchiseid).HasColumnName("franchiseid");
                entity.Property(e => e.Salary).HasColumnName("salary");
                entity.Property(e => e.Amount).HasColumnName("amount");
                entity.Property(e => e.Playername).HasColumnName("playername");
                entity.Property(e => e.Position).HasColumnName("position");
                entity.Property(e => e.Team).HasColumnName("team");
                entity.Property(e => e.Years).HasColumnName("years");
                entity.Property(e => e.Yearoftransaction).HasColumnName("yearoftransaction");
                entity.HasOne(d => d.League).WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.Leagueid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_transaction_League");
            });
            modelBuilder.Entity<BidEntity>(entity =>
            {
                entity.HasKey(e => e.Bidid).HasName("PK_bidledger");

                entity.ToTable("bid");

                entity.Property(e => e.Bidid).HasColumnName("bidid").ValueGeneratedOnAdd();
                entity.Property(e => e.Bidlength).HasColumnName("bidlength");
                entity.Property(e => e.Bidsalary).HasColumnName("bidsalary");
                entity.Property(e => e.Expires)
                    .HasColumnType("datetime")
                    .HasColumnName("expires");
                entity.Property(e => e.Leagueid).HasColumnName("leagueid");
                entity.Property(e => e.Mflid).HasColumnName("mflid");
                entity.Property(e => e.Ownerid).HasColumnName("ownerid");

                entity.HasOne(d => d.League).WithMany(p => p.Bids)
                    .HasForeignKey(d => d.Leagueid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_bid_League");

                entity.HasOne(d => d.Player).WithMany(p => p.Bids)
                    .HasForeignKey(d => d.Mflid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_bid_player");

                entity.HasOne(d => d.LeagueOwner).WithMany(p => p.Bids)
                    .HasForeignKey(d => d.Ownerid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_bid_leagueowner");
            });

            modelBuilder.Entity<ContractEntity>(entity =>
            {
                entity.ToTable("contract");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Bidid).HasColumnName("bidid");
                entity.Property(e => e.Contractvalue).HasColumnName("contractvalue");
                entity.Property(e => e.Leagueid).HasColumnName("leagueid");
                entity.Property(e => e.Length).HasColumnName("length");
                entity.Property(e => e.Mflid).HasColumnName("mflid");
                entity.Property(e => e.Ownerid).HasColumnName("ownerid");
                entity.Property(e => e.Salary).HasColumnName("salary");

                entity.HasOne(d => d.Bid).WithMany(p => p.Contracts)
                    .HasForeignKey(d => d.Bidid)
                    .HasConstraintName("FK_contract_bid");

                entity.HasOne(d => d.League).WithMany(p => p.Contracts)
                    .HasForeignKey(d => d.Leagueid)
                    .HasConstraintName("FK_contract_League");

                entity.HasOne(d => d.Player).WithMany(p => p.Contracts)
                    .HasForeignKey(d => d.Mflid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_contract_player");

                entity.HasOne(d => d.Owner).WithMany(p => p.Contracts)
                    .HasForeignKey(d => d.Ownerid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_contract_leagueowner");
            });


            modelBuilder.Entity<LeagueEntity>(entity =>
            {
                entity.HasKey(e => e.Mflid);

                entity.ToTable("League");

                entity.Property(e => e.Mflid)
                    .ValueGeneratedNever()
                    .HasColumnName("mflid");
                entity.Property(e => e.Commishcookie).HasColumnName("commishcookie");
                entity.Property(e => e.Isauctioning).HasColumnName("isauctioning");
                entity.Property(e => e.Mflhash).HasColumnName("mflhash");
                entity.Property(e => e.Name)
                    .HasMaxLength(80)
                    .HasColumnName("name");
            });

            modelBuilder.Entity<LeagueOwnerEntity>(entity =>
            {
                entity.HasKey(e => e.Leagueownerid).HasName("PK_leagueowner");
                entity.ToTable("leagueowner");

                entity.Property(e => e.Leagueownerid).HasColumnName("leagueownerid");
                entity.Property(e => e.Caproom).HasColumnName("caproom");
                entity.Property(e => e.Teamname).HasColumnName("teamname");
                entity.Property(e => e.Leagueid).HasColumnName("leagueid");
                entity.Property(e => e.Mflfranchiseid).HasColumnName("mflfranchiseid");
                entity.Property(e => e.Ownerid).HasColumnName("ownerid");
                entity.Property(e => e.Yearsleft).HasColumnName("yearsleft");

                entity.HasOne(d => d.League).WithMany(p => p.Leagueowners)
                    .HasForeignKey(d => d.Leagueid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_leagueowner_League");

                entity.HasOne(d => d.Owner).WithMany(p => p.Leagueowners)
                    .HasForeignKey(d => d.Ownerid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_leagueowner_owner");
            });

            modelBuilder.Entity<LotEntity>(entity =>
            {
                entity.ToTable("lot");

                entity.Property(e => e.Lotid)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("lotid");
                entity.Property(e => e.Bidid).HasColumnName("bidid");
                entity.Property(e => e.Nominatedby).HasColumnName("nominatedby");
                entity.Property(e => e.Leagueid).HasColumnName("leagueid");

                entity.HasOne(d => d.Bid).WithMany(p => p.Lots)
                    .HasForeignKey(d => d.Bidid)
                    .HasConstraintName("FK_bidid_fkey");
                entity.HasOne(d => d.Nominator).WithMany(p => p.Lots)
                    .HasForeignKey(d => d.Nominatedby)
                    .HasConstraintName("FK_lot_leagueowner");

                entity.HasOne(d => d.League).WithMany(p => p.Lots)
                    .HasForeignKey(d => d.Leagueid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_lot_League");
            });

            modelBuilder.Entity<OwnerEntity>(entity =>
            {
                entity.ToTable("owner");

                entity.Property(e => e.Ownerid)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("ownerid");
                entity.Property(e => e.Displayname)
                    .HasMaxLength(50)
                    .HasColumnName("displayname");
                entity.Property(e => e.Ownername)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("ownername");
                entity.Property(e => e.PasswordHash)
                    .IsUnicode(false)
                    .HasColumnName("password_hash");
                entity.Property(e => e.Premium).HasColumnName("premium");
            });

            modelBuilder.Entity<PlayerEntity>(entity =>
            {
                entity.HasKey(e => e.Mflid);

                entity.ToTable("player");

                entity.Property(e => e.Mflid)
                    .ValueGeneratedNever()
                    .HasColumnName("mflid");
                entity.Property(e => e.Actionshot).HasColumnName("actionshot");
                entity.Property(e => e.Age).HasColumnName("age");
                entity.Property(e => e.Cbsid)
                    .HasMaxLength(50)
                    .HasColumnName("cbsid");
                entity.Property(e => e.College).HasColumnName("college");
                entity.Property(e => e.Draftpick).HasColumnName("draftpick");
                entity.Property(e => e.Draftround).HasColumnName("draftround");
                entity.Property(e => e.Draftteam)
                    .HasMaxLength(50)
                    .HasColumnName("draftteam");
                entity.Property(e => e.Draftyear).HasColumnName("draftyear");
                entity.Property(e => e.Espnid)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("espnid");
                entity.Property(e => e.Firstname)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("firstname");
                entity.Property(e => e.Fullname)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("fullname");
                entity.Property(e => e.Headshot).HasColumnName("headshot");
                entity.Property(e => e.Height).HasColumnName("height");
                entity.Property(e => e.IsActive).HasColumnName("isActive");
                entity.Property(e => e.Jersey).HasColumnName("jersey");
                entity.Property(e => e.Lastname)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("lastname");
                entity.Property(e => e.Lastseasonpts)
                    .HasColumnType("numeric(4, 1)")
                    .HasColumnName("lastseasonpts");
                entity.Property(e => e.Position)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("position");
                entity.Property(e => e.Rotowireid)
                    .HasMaxLength(50)
                    .HasColumnName("rotowireid");
                entity.Property(e => e.Rotoworldid)
                    .HasMaxLength(50)
                    .HasColumnName("rotoworldid");
                entity.Property(e => e.Team)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("team");
                entity.Property(e => e.Weight).HasColumnName("weight");
            });

            modelBuilder.Entity<SuggestionEntity>(entity =>
            {
                entity.ToTable("suggestions");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Mflid)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("mflid");
                entity.Property(e => e.Ownerid).HasColumnName("ownerid");
                entity.Property(e => e.Suggestion).HasColumnName("suggestion");
                entity.Property(e => e.YearMax).HasColumnName("yearMax");
                entity.Property(e => e.YearMin).HasColumnName("yearMin");
            });
            modelBuilder.Entity<FranchiseTagLeague>(entity =>
            {
                entity.ToTable("franchisetagleague");

                entity.Property(e => e.Mflleagueid)
                    .HasColumnName("mflleagueid");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.QB)
                    .HasColumnName("qb");
                entity.Property(e => e.RB)
                    .HasColumnName("rb");
                entity.Property(e => e.WR)
                    .HasColumnName("wr");
                entity.Property(e => e.TE)
                    .HasColumnName("te");
                entity.HasOne(e => e.League)
                .WithMany(l => l.FranchiseTagLeagues)
                .HasForeignKey(d => d.Mflleagueid)
                    .HasConstraintName("FK_franchisetagleauge_League"); 

                entity.HasKey(e => new {
                    e.Mflleagueid,
                    e.Year
                    });
            });

            modelBuilder.Entity<Buyout>(entity =>
            {
                entity.ToTable("buyout");

                entity.Property(e => e.BuyoutId)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("buyoutid");
                entity.Property(e => e.LeagueId)
                    .HasColumnName("leagueid");
                entity.Property(e => e.PlayerId)
                    .HasColumnName("playerid");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.LeagueOwnerId)
                    .HasColumnName("leagueownerid");
                entity.Property(e => e.OriginalSalary)
                    .HasColumnName("originalsalary");
                entity.HasOne(e => e.League)
                .WithMany(l => l.Buyouts)
                .HasForeignKey(d => d.LeagueId)
                .HasConstraintName("FK_buyout_League");
                entity.HasOne(e => e.Player)
                .WithMany(l => l.Buyouts)
                .HasForeignKey(d => d.PlayerId)
                .HasConstraintName("FK_buyout_player");
                entity.HasOne(e => e.LeagueOwner)
                .WithMany(l => l.Buyouts)
                .HasForeignKey(d => d.LeagueOwnerId)
                .HasConstraintName("FK_buyout_leagueowner");

                entity.HasKey(e => e.BuyoutId);
            });

            modelBuilder.Entity<FranchiseTagPlayer>(entity =>
            {
                entity.ToTable("franchisetagplayer");

                entity.Property(e => e.Mflplayerid)
                    .HasColumnName("mflplayerid");
                entity.Property(e => e.Mflleagueid)
                    .HasColumnName("mflleagueid");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.Leagueownerid)
                    .HasColumnName("leagueownerid");
                entity.Property(e => e.Franchisetagid)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("franchisetagid");
                entity.Property(e => e.Originalsalary)
                    .HasColumnName("originalsalary");
                entity.Property(e => e.Tagprice)
                    .HasColumnName("tagprice");
                entity.Property(e => e.Position)
                     .HasMaxLength(8)
                    .IsUnicode(false)
                    .HasColumnName("position");
                entity.Property(e => e.Fullname)
                    .HasMaxLength(80)
                    .IsUnicode(false)
                      .HasColumnName("fullname");
                entity.HasOne(e => e.Player)
                    .WithMany(p => p.FranchiseTags)
                     .HasForeignKey(d => d.Mflplayerid)
                    .HasConstraintName("FK_franchisetagplayer_player");
                entity.HasOne(e => e.Leagueowner)
                    .WithMany(p => p.FranchiseTags)
                    .HasForeignKey(d => d.Leagueownerid)
                    .HasConstraintName("FK_franchisetagplayer_leagueowner");
                entity.HasOne(e => e.FranchiseTagLeagueData)
                    .WithMany(p => p.FranchiseTagPlayers)
                    .HasForeignKey(d => new
                    {
                        d.Mflleagueid,
                        d.Year
                    })
                    .HasConstraintName("FK_franchisetagplayer_franchisetagleauge");
            });

            OnModelCreatingPartial(modelBuilder);
        }



        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }


    public partial class PlayerEntity
    {
        [Key]
        public int Mflid { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Position { get; set; }
        public string? Fullname { get; set; }
        public string? Team { get; set; }
        public int? Age { get; set; }
        public int? Height { get; set; }
        public int? Weight { get; set; }
        public string? Headshot { get; set; }
        public string? Actionshot { get; set; }
        public string? Espnid { get; set; }
        public string? College { get; set; }
        public string? Rotowireid { get; set; }
        public int? Draftround { get; set; }
        public int? Draftpick { get; set; }
        public int? Draftyear { get; set; }
        public int? Jersey { get; set; }
        public string? Draftteam { get; set; }
        public string? Cbsid { get; set; }
        public string? Rotoworldid { get; set; }
        public decimal? Lastseasonpts { get; set; }
        public bool? IsActive { get; set; }
        public virtual ICollection<Buyout> Buyouts { get; } = new List<Buyout>();
        public virtual ICollection<FranchiseTagPlayer> FranchiseTags { get; } = new List<FranchiseTagPlayer>();
        public virtual ICollection<BidEntity> Bids { get; } = new List<BidEntity>();
        public virtual ICollection<ContractEntity> Contracts { get; } = new List<ContractEntity>();

    }
    public partial class BidEntity
    {
        [Key]
        public int Bidid { get; set; }
        public int Bidlength { get; set; }
        public int Bidsalary { get; set; }
        public DateTime Expires { get; set; }
        public int Mflid { get; set; }
        public int Ownerid { get; set; }
        public int Leagueid { get; set; }
        public virtual ICollection<ContractEntity> Contracts { get; } = new List<ContractEntity>();
        public virtual LeagueEntity League { get; set; } = null!;
        public virtual ICollection<LotEntity> Lots { get; } = new List<LotEntity>();
        public virtual PlayerEntity Player { get; set; } = null!;
        public virtual LeagueOwnerEntity LeagueOwner { get; set; } = null!;
    }
    public partial class LeagueOwnerEntity
    {
        [Key]
        public int Leagueownerid { get; set; }
        public int Leagueid { get; set; }
        public int Ownerid { get; set; }
        public int Mflfranchiseid { get; set; }
        public int? Caproom { get; set; }
        public int? Yearsleft { get; set; }
        public string Teamname { get; set; }
        public virtual ICollection<FranchiseTagPlayer> FranchiseTags { get; } = new List<FranchiseTagPlayer>();
        public virtual ICollection<Buyout> Buyouts { get; } = new List<Buyout>();
        public virtual ICollection<BidEntity> Bids { get; } = new List<BidEntity>();
        public virtual ICollection<LotEntity> Lots { get; } = new List<LotEntity>();
        public virtual ICollection<ContractEntity> Contracts { get; } = new List<ContractEntity>();
        public virtual LeagueEntity League { get; set; } = null!;
        public virtual OwnerEntity Owner { get; set; } = null!;
    }

    public partial class SuggestionEntity
    {
        public int Ownerid { get; set; }
        public int Suggestion { get; set; }
        public string Mflid { get; set; } = null!;
        public int? YearMin { get; set; }
        public int? YearMax { get; set; }
        [Key]
        public int Id { get; set; }
        public SuggestionEntity()
        {

        }

        public SuggestionEntity(int owner, string mfl, int salary)
        {
            Ownerid = owner;
            Mflid = mfl;
            Suggestion = salary;
        }
    }

    public partial class LotEntity
    {
        [Key]
        public int Lotid { get; set; }
        public int? Bidid { get; set; }
        public int Leagueid { get; set; }
        public int? Nominatedby { get; set; }
        public virtual LeagueOwnerEntity? Nominator { get; set; }
        public virtual BidEntity? Bid { get; set; }
        public virtual LeagueEntity League { get; set; } = null!;
    }

    public partial class OwnerEntity
    {
        [Key]
        public int Ownerid { get; set; }
        public string? Ownername { get; set; }
        public string? PasswordHash { get; set; }
        public string? Displayname { get; set; }
        public bool? Premium { get; set; }
        public bool istest { get; set; }
        public string Avatar { get; set; }
        public string authid { get; set; }
        public virtual ICollection<LeagueOwnerEntity> Leagueowners { get; } = new List<LeagueOwnerEntity>();
    }

    public partial class LeagueEntity
    {
        [Key]
        public int Mflid { get; set; }
        public string Name { get; set; } = null!;
        public string? Mflhash { get; set; }
        public string? Commishcookie { get; set; }
        public bool Isauctioning { get; set; }
        public bool Istest { get; set; }
        public virtual ICollection<Buyout> Buyouts { get; } = new List<Buyout>();
        public virtual ICollection<BidEntity> Bids { get; } = new List<BidEntity>();
        public virtual ICollection<FranchiseTagLeague> FranchiseTagLeagues { get; } = new List<FranchiseTagLeague>();
        public virtual ICollection<Transaction> Transactions { get; } = new List<Transaction>();
        public virtual ICollection<ContractEntity> Contracts { get; } = new List<ContractEntity>();
        public virtual ICollection<LeagueOwnerEntity> Leagueowners { get; } = new List<LeagueOwnerEntity>();
        public virtual ICollection<LotEntity> Lots { get; } = new List<LotEntity>();
    }

    public partial class ContractEntity
    {
        [Key]
        public int Id { get; set; }
        public int Mflid { get; set; }
        public int Length { get; set; }
        public int Salary { get; set; }
        public int Contractvalue { get; set; }
        public int Ownerid { get; set; }
        public int? Leagueid { get; set; }
        public int? Bidid { get; set; }
        public virtual BidEntity? Bid { get; set; }
        public virtual LeagueEntity? League { get; set; }
        public virtual PlayerEntity Player { get; set; } = null!;
        public virtual LeagueOwnerEntity Owner { get; set; } = null!;

    }
    public partial class Transaction
    {
        public DateTime? Timestamp { get; set; }
        public int Transactionid { get; set; }
        public int Franchiseid { get; set; }
        public int? Salary { get; set; }
        public decimal Amount { get; set; }
        public string Playername { get; set; }
        public string Position { get; set; }
        public string Team { get; set; }
        public int Years { get; set; }
        public int? Yearoftransaction { get; set; }
        public int Leagueid { get; set; }
        public int Globalid { get; set; }
        public virtual LeagueEntity League { get; set; } = null!;
    }
    public partial class FranchiseTagPlayer
    {

        public int Mflplayerid { get; set; }
        [Key]
        public int Franchisetagid { get; set; }
        public int Year { get; set; }
        public int Leagueownerid { get; set; }
        public int Mflleagueid { get; set; }
        public int Originalsalary { get; set; }
        public int Tagprice { get; set; }
        public string Position { get; set; }
        public string Fullname { get; set; }
        public virtual LeagueOwnerEntity Leagueowner { get; set; }
        public virtual PlayerEntity Player { get; set; }
        public virtual FranchiseTagLeague FranchiseTagLeagueData { get; set; }
    }
    public partial class FranchiseTagLeague
    {
        [Key]
        public int Mflleagueid { get; set; }
        [Key]
        public int Year { get; set; }
        public int QB { get; set; }
        public int RB { get; set; }
        public int WR { get; set; }
        public int TE { get; set; }
        public virtual LeagueEntity League { get; set; }
        public virtual ICollection<FranchiseTagPlayer> FranchiseTagPlayers { get; } = new List<FranchiseTagPlayer>();
    }
    public partial class Buyout
    {
        [Key]
        public int BuyoutId { get; set; }
        public int LeagueId { get; set; }
        public int PlayerId { get; set; }
        public int Year { get; set; }
        public int LeagueOwnerId { get; set; }
        public int OriginalSalary { get; set; }
        public double BuyoutPenalty { get; set; }
        public virtual LeagueEntity League { get; set; }
        public virtual LeagueOwnerEntity LeagueOwner { get; set; }
        public virtual PlayerEntity Player { get; set; }
    }
}