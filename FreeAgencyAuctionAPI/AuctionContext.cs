using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FreeAgencyAuctionAPI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
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
        public DbSet<NflTeam> NflTeams { get; set; }
        public DbSet<NflTeamMatchup> NflTeamMatchups { get; set; }
        public DbSet<Pick> NflPicks { get; set; }
        public DbSet<ExtraPick> ExtraPicks { get; set; }
        public DbSet<Prop> Props { get; set; } 
        public DbSet<SeasonWins> SeasonWins { get;set; }
        public DbSet<OverUnderPick> OverUnderPicks { get; set; }
        public DbSet<WaiverExtension> WaiverExtensions { get; set; }
        public DbSet<CapEatCandidate> CapEatCandidates { get; set; }
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<Pool> Pools { get; set; }
        public DbSet<PoolUser> PoolUsers { get; set; }
        /*
                public DbSet<NflTeamEntity> NflTeams { get; set; }
                public DbSet<NflOverPickEntity> NflPicks { get; set; }
        */

        public AuctionContext(DbContextOptions<AuctionContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<NflTeam>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_nflteam");
                entity.ToTable("nflteam");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Tricode).HasColumnName("tricode");
                entity.Property(e => e.City).HasColumnName("city");
                entity.Property(e => e.Logo).HasColumnName("logo");
                entity.Property(e => e.SecondaryLogo).HasColumnName("secondaryLogo");
                entity.Property(e => e.Primary).HasColumnName("primary");
                entity.Property(e => e.Secondary).HasColumnName("secondary");
                entity.Property(e => e.Tertiary).HasColumnName("tertiary");
                entity.Property(e => e.Tricode).HasColumnName("tricode");
                entity.Property(e => e.League).HasColumnName("league");
                entity.Property(e => e.SportsDataId).HasColumnName("sportsdataid");

            });

            modelBuilder.Entity<Pick>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_pick");
                entity.ToTable("pick");
                entity.Property(e => e.OwnerId).HasColumnName("ownerid");
                entity.Property(e => e.MatchupId).HasColumnName("matchupid");
                entity.Property(e => e.Choice).HasColumnName("choice");
                entity.Property(e => e.Points).HasColumnName("points");
                entity.HasOne(d => d.Owner).WithMany(p => p.ConfidencePicks)
                    .HasForeignKey(d => d.OwnerId)
                    .HasConstraintName("FK_pick_owner");
                entity.HasOne(d => d.ChosenTeam).WithMany(p => p.ChosenPicks)
                    .HasForeignKey(d => d.Choice)
                    .HasConstraintName("FK_pick_nflteam");
                entity.HasOne(d => d.NflTeamMatchup).WithMany(p => p.ChosenPicks)
                    .HasForeignKey(d => d.MatchupId)
                    .HasConstraintName("FK_pick_matchup");

            });
            modelBuilder.Entity<ExtraPick>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_extrapick");
                entity.ToTable("extrapick");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OwnerId).HasColumnName("ownerid");
                entity.Property(e => e.Choice).HasColumnName("choice");
                entity.Property(e => e.PropId).HasColumnName("propid");
                entity.HasOne(d => d.Owner).WithMany(p => p.ExtraPicks)
                    .HasForeignKey(d => d.OwnerId)
                    .HasConstraintName("FK_extrapick_owner");
                entity.HasOne(d => d.Prop).WithMany(p => p.ChosenProps)
                    .HasForeignKey(d => d.PropId)
                    .HasConstraintName("FK_extrapick_prop");
            });
            modelBuilder.Entity<NflTeamMatchup>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_matchup");
                entity.ToTable("matchup");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Left).HasColumnName("left");
                entity.Property(e => e.Right).HasColumnName("right");
                entity.Property(e => e.Year).HasColumnName("year");
                entity.Property(e => e.Week).HasColumnName("week");
                entity.Property(e => e.Pickable).HasColumnName("pickable");
                entity.Property(e => e.Winner).HasColumnName("winner");
                entity.HasOne(e => e.LeftTeam).WithMany(e => e.LeftMatchups).HasForeignKey(d => d.Left).HasConstraintName("FK_matchup_nflteam_left");
                entity.HasOne(e => e.RightTeam).WithMany(e => e.RightMatchups).HasForeignKey(d => d.Right).HasConstraintName("FK_matchup_nflteam_right");
                entity.HasOne(e => e.WinningTeam).WithMany(e => e.WinMatchups).HasForeignKey(d => d.Winner).HasConstraintName("FK_matchup_nflteam_winner");
            });
            modelBuilder.Entity<Prop>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_prop");
                entity.ToTable("prop");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OptionA).HasColumnName("optionA");
                entity.Property(e => e.OptionB).HasColumnName("optionB");
                entity.Property(e => e.Year).HasColumnName("year");
                entity.Property(e => e.Week).HasColumnName("week");
                entity.Property(e => e.Pickable).HasColumnName("pickable");
                entity.Property(e => e.Winner).HasColumnName("winner");
               
            });
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
                entity.Property(e => e.IsBuyoutSzn).HasColumnName("isbuyoutszn");
                entity.Property(e => e.IsTaxiSzn).HasColumnName("istaxicutszn");
                entity.Property(e => e.IsFranchiseTagSzn).HasColumnName("isfranchisetagszn");
                entity.Property(e => e.FirstYear).HasColumnName("firstyear");
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

            modelBuilder.Entity<PoolUser>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK_pooluser");
                entity.ToTable("pooluser");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.PoolId).HasColumnName("poolid");
                entity.Property(e => e.OwnerId).HasColumnName("ownerid");
                entity.Property(e => e.IsPaid).HasColumnName("ispaid");


                entity.HasOne(d => d.Pool).WithMany(p => p.PoolUsers)
                    .HasForeignKey(d => d.PoolId)
                    .HasConstraintName("FK_pooluser_owner");

                entity.HasOne(d => d.Owner).WithMany(p => p.PoolUsers)
                    .HasForeignKey(d => d.OwnerId)
                    .HasConstraintName("FK_pooluser_pool");
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
                entity.HasOne(d => d.LotOwner).WithMany(p => p.Lots)
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
                entity.Property(e => e.ConfidencePaid).HasColumnName("confidencepaid");
                entity.Property(e => e.StreamToken)
                    .HasColumnName("streamtoken");
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

                entity.HasKey(e => new
                {
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
            modelBuilder.Entity<WaiverExtension>(entity =>
            {
                entity.ToTable("waiverextension");

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");
                entity.Property(e => e.LeagueId)
                    .HasColumnName("leagueid");
                entity.Property(e => e.PlayerId)
                    .HasColumnName("playerid");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.LeagueOwnerId)
                    .HasColumnName("leagueownerid");
                entity.HasOne(e => e.League)
                .WithMany(l => l.WaiverExtensions)
                .HasForeignKey(d => d.LeagueId)
                .HasConstraintName("FK_waiverextension_League");
                entity.HasOne(e => e.Player)
                .WithMany(l => l.WaiverExtensions)
                .HasForeignKey(d => d.PlayerId)
                .HasConstraintName("FK_waiverextension_player");
                entity.HasOne(e => e.LeagueOwner)
                .WithMany(l => l.WaiverExtensions)
                .HasForeignKey(d => d.LeagueOwnerId)
                .HasConstraintName("FK_waiverextension_leagueowner");

                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<SeasonWins>(entity =>
            {
                entity.ToTable("seasonwins");

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");
                entity.Property(e => e.FranchiseId)
                    .HasColumnName("franchiseid");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.BaseOverUnder)
                    .HasColumnName("baseoverunder");
                entity.Property(e => e.RealWins)
                    .HasColumnName("realwins");
                entity.Property(e => e.GamesRemaining)
                    .HasColumnName("gamesremaining");
                entity.HasOne(e => e.Franchise)
                    .WithMany(l => l.SeasonWins)
                    .HasForeignKey(d => d.FranchiseId)
                    .HasConstraintName("FK_seasonwins_nflteam");
                entity.HasKey(e => e.Id);
            });
            modelBuilder.Entity<OverUnderPick>(entity =>
            {
                entity.ToTable("overunderpick");

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");
                entity.Property(e => e.LineId)
                    .HasColumnName("lineid");
                entity.Property(e => e.UserId)
                    .HasColumnName("userid");
                entity.Property(e => e.IsOver)
                    .HasColumnName("isover");
                entity.Property(e => e.PoolId)
                    .HasColumnName("poolid");
                entity.Property(e => e.LineAdjustment)
                    .HasColumnName("lineadjustment");
                entity.HasOne(e => e.Pool)
                    .WithMany(l => l.OUPicks)
                    .HasForeignKey(d => d.PoolId)
                    .HasConstraintName("FK_overunderpick_pool");
                entity.HasOne(e => e.PoolUser)
                    .WithMany(l => l.OverUnderPicks)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_overunderpick_pooluser");
                entity.HasOne(e => e.WinLine)
                    .WithMany(l => l.OverUnderPicks)
                    .HasForeignKey(d => d.LineId)
                    .HasConstraintName("FK_overunderpick_seasonwins");
                entity.HasKey(e => e.Id);
            });
            modelBuilder.Entity<Pool>(entity =>
            {
                entity.ToTable("pool");

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");
                entity.Property(e => e.Type)
                    .HasColumnName("type");
                entity.Property(e => e.League)
                    .HasColumnName("league");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.OpenDate)
                    .HasColumnName("opendate");
                entity.Property(e => e.StartDate)
                    .HasColumnName("startdate");
                entity.Property(e => e.Name)
                    .HasColumnName("name");
                entity.HasKey(e => e.Id);
            });
            modelBuilder.Entity<Proposal>(entity =>
            {
                entity.ToTable("proposal");

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");
                entity.Property(e => e.LeagueId)
                    .HasColumnName("leagueId");
                entity.Property(e => e.SenderId)
                    .HasColumnName("senderId");
                entity.Property(e => e.ReceiverId)
                    .HasColumnName("receiverId");
                entity.Property(e => e.Accepted)
                    .HasColumnName("accepted");
                entity.Property(e => e.Expires)
                    .HasColumnName("expires");
                entity.Property(e => e.MflTradeId)
                    .HasColumnName("mflTradeId");
                entity.Property(e => e.CommentGUID).HasColumnName("commentGUID");
                entity.HasKey(e => e.Id);
            });
            modelBuilder.Entity<CapEatCandidate>(entity =>
            {
                entity.ToTable("capeatcandidate");

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");
                entity.Property(e => e.LeagueId)
                    .HasColumnName("leagueId");
                entity.Property(e => e.EaterId)
                    .HasColumnName("eaterid");
                entity.Property(e => e.ReceiverId)
                    .HasColumnName("receiverId");
                entity.Property(e => e.Year)
                    .HasColumnName("year");
                entity.Property(e => e.MflPlayerId)
                    .HasColumnName("mflPlayerId");
                entity.Property(e => e.CapAdjustment).HasColumnName("capAdjustment");
                entity.Property(e => e.ProposalGUID).HasColumnName("proposalGUID");
                entity.HasKey(e => e.Id);
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
        public virtual ICollection<WaiverExtension> WaiverExtensions { get; } = new List<WaiverExtension>();
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
        public virtual ICollection<WaiverExtension> WaiverExtensions { get; } = new List<WaiverExtension>();
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
        public virtual LeagueOwnerEntity? LotOwner { get; set; }
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
        public string StreamToken { get; set; }
        public bool ConfidencePaid { get; set; }
/*        public virtual ICollection<Pool> Pools { get; } = new List<Pool>();*/
        public virtual ICollection<LeagueOwnerEntity> Leagueowners { get; } = new List<LeagueOwnerEntity>();
        public virtual ICollection<PoolUser> PoolUsers { get; } = new List<PoolUser>();
        public virtual ICollection<Pick> ConfidencePicks { get; } = new List<Pick>();
        public virtual ICollection<ExtraPick> ExtraPicks { get; } = new List<ExtraPick>();
        
    }

    public partial class LeagueEntity
    {
        [Key]
        public int Mflid { get; set; }
        public string Name { get; set; } = null!;
        public string? Mflhash { get; set; }
        public string? Commishcookie { get; set; }
        public bool Isauctioning { get; set; }
        public bool IsTaxiSzn { get; set; }
        public bool IsFranchiseTagSzn { get; set; } 
        public bool IsBuyoutSzn { get; set; }
        public bool Istest { get; set; }
        public int FirstYear { get; set; }
        public virtual ICollection<Buyout> Buyouts { get; } = new List<Buyout>();
        public virtual ICollection<BidEntity> Bids { get; } = new List<BidEntity>();
        public virtual ICollection<WaiverExtension> WaiverExtensions { get; } = new List<WaiverExtension>();
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
        public decimal BuyoutPenalty { get; set; }
        public virtual LeagueEntity League { get; set; }
        public virtual LeagueOwnerEntity LeagueOwner { get; set; }
        public virtual PlayerEntity Player { get; set; }
    }
    public partial class WaiverExtension
    {
        [Key]
        public int Id { get; set; }
        public int LeagueId { get; set; }
        public int LeagueOwnerId { get; set; }
        public int Year { get; set; }
        public int PlayerId { get; set; }
        public virtual LeagueEntity League { get; set; }
        public virtual LeagueOwnerEntity LeagueOwner { get; set; }
        public virtual PlayerEntity Player { get; set; }
    }

    public partial class NflTeam
    {
        [Key]
        public int Id { get; set; }
        public string Tricode { get; set; }
        public string City { get; set; }
        public string Name { get; set; }
        public string Primary { get; set; }
        public string Secondary { get; set; }
        public string Tertiary { get; set; }
        public string Logo { get; set; }
        public string SecondaryLogo { get; set; }
        public string League { get; set; }
        public int SportsDataId { get; set; }
        public virtual ICollection<NflTeamMatchup> LeftMatchups { get; } = new List<NflTeamMatchup>();
        public virtual ICollection<NflTeamMatchup> RightMatchups { get; } = new List<NflTeamMatchup>();
        public virtual ICollection<NflTeamMatchup> WinMatchups { get; } = new List<NflTeamMatchup>();
        public virtual ICollection<SeasonWins> SeasonWins { get; } = new List<SeasonWins>();
        public virtual ICollection<Pick> ChosenPicks { get; } = new List<Pick>();
    }

    public partial class NflTeamMatchup
    {
        [Key]
        public int Id { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }
        public int Year { get; set; }
        public int Week { get; set; }
        public int? Winner { get; set; }
        public bool Pickable { get; set; }
        public virtual NflTeam LeftTeam { get; set; }
        public virtual NflTeam RightTeam { get; set; }
        public virtual NflTeam? WinningTeam { get; set; }
        public virtual ICollection<Pick> ChosenPicks { get; } = new List<Pick>();
    }

    public partial class Pick
    {
        [Key]
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int MatchupId { get; set; }
        public int? Choice { get; set; }
        public int Points { get; set; }
        public virtual NflTeam ChosenTeam { get; set; }
        public virtual OwnerEntity Owner { get; set; }
        public virtual NflTeamMatchup NflTeamMatchup { get; set; }

    }

    public partial class ExtraPick
    {
        [Key]
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int PropId { get; set; }
        public string Choice { get; set; }
        public virtual OwnerEntity Owner { get; set; }
        public virtual Prop Prop { get; set; }
    }

    public partial class Prop
    {
        [Key]
        public int Id { get; set; }
        public string Prompt { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public int Year { get; set; }
        public int Week { get; set; }
        public string Winner { get; set; }
        public bool Pickable { get; set; }
        public virtual ICollection<ExtraPick> ChosenProps { get; } = new List<ExtraPick>();
    }

    public partial class SeasonWins
    {
        [Key]
        public int Id { get; set; }
        public int FranchiseId { get; set; }
        public int Year { get; set; }
        public decimal BaseOverUnder { get; set; }
        public int RealWins { get; set; }
        public int GamesRemaining { get; set; }
        public virtual ICollection<OverUnderPick> OverUnderPicks { get; } = new List<OverUnderPick>();
        public virtual NflTeam Franchise { get; set; } = null!;
    }
    public partial class OverUnderPick
    {
        [Key]
        public int? Id { get; set; }
        public int LineId { get; set; }
        public int UserId { get; set; }
        public bool? IsOver { get; set; }
        public int LineAdjustment { get; set; }
        public int PoolId { get; set; }
        public virtual SeasonWins WinLine { get; set; } = null!;
        public virtual Pool Pool { get; set; } = null!;
        public virtual PoolUser PoolUser { get; set; } = null!;
    }

    public partial class Pool
    {
        [Key]
        public int Id { get; set; }
        public int Year { get; set; }
        public string Type { get; set; }
        public string League { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime OpenDate { get; set; }
        public virtual ICollection<OverUnderPick> OUPicks { get; } = new List<OverUnderPick>();
        public virtual ICollection<PoolUser> PoolUsers { get; } = new List<PoolUser>();
    }
    public partial class PoolUser
    {
        [Key]
        public int Id { get; set; }
        public int PoolId { get; set; }
        public int OwnerId { get; set; }
        public bool IsPaid { get; set; }
        public virtual Pool Pool { get; set; } = null!;
        public virtual OwnerEntity Owner { get; set; } = null!;
        public virtual ICollection<OverUnderPick> OverUnderPicks { get; } = new List<OverUnderPick>();
    }
    public class CapEatCandidate
    {
        [Key]
        public int Id { get; set; }
        public int EaterId { get; set; }
        public int ReceiverId { get; set; }
        public int LeagueId { get; set; }
        public string ProposalGUID { get; set; }
        public int Year { get; set; }
        public int MflPlayerId { get; set; }
        public int CapAdjustment { get; set; }
    }
    public class Proposal
    {
        [Key]
        public int Id { get; set; }
        public int LeagueId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public bool Accepted { get; set; }
        public string Expires { get; set; }
        public int MflTradeId { get; set; }
        public string CommentGUID { get; set; }
    }
}