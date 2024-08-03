using System;
using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class OwnerDTO
    {
        public int OwnerId { get; set; }
        public string Ownername { get; set; }
        public string Password { get; set; }
        public bool Premium { get; set; }
        public string DisplayName { get; set; }
        public string StreamToken { get; set; }
        public string Avatar { get; set; }
        public bool ConfidencePaid { get; set; }
        public IEnumerable<PoolDTO> Pools {  get; set; }
        public IEnumerable<LeagueOwnerDTO> Leagues { get; set; }

    }

    public class LeagueOwnerDTO 
    {
        public List<TagCandidate> TagCandidates { get; set; }
        public List<PlayerDTO> TaxiPlayers { get; set; }
        public List<PlayerDTO> CutCandidates { get; set; }
        public int CapRoom { get; set; }
        public int YearsLeft { get; set; }
        public int Mflfranchiseid { get; set; }
        public int Leagueownerid { get; set; }
        public string TeamName { get; set; }
        public string Ownername { get; set; }
        public LeagueDTO League { get; set; }
    }
    public class PoolDTO
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string League { get; set; }
        public int Year { get; set; }
        public DateTime OpenDate { get; set; }
        public DateTime StartDate { get; set; }
        public string Name { get; set; }
        public int PoolOwnerId { get; set; }
    }
    public class OpposingFranchiseDTO
    {
        public int CapRoom { get; set; }
        public int YearsLeft { get; set; }
        public int Mflfranchiseid { get; set; }
        public int Leagueownerid { get; set; }
        public string TeamName { get; set; }
        public string OwnerName { get; set; }
        public string Avatar { get; set; }
    }
    public class OpposingFranchiseWithRoster : OpposingFranchiseDTO
    {
        public List<PlayerDTO> Players { get; set; }
    }

    public class TagCandidate
    {
        public PlayerDTO Player { get; set; }
        public int LastSeasonSalary { get; set; }
        public int TagAmount { get; set; }

    }
    public class FranchiseTagRequestBody
    {
        public int leagueId { get; set; }
        public int mflPlayerId { get; set; }
        public int mflFranchiseId { get; set; }
        public int tagSalary { get; set; }
        public int leagueOwnerId { get; set; }
    }
    public class CutRequestBody
    {
        public int leagueId { get; set; }
        public PlayerDTO player { get; set; }
        public int mflFranchiseId { get; set; }
        public double rebate { get; set; }
    }
}