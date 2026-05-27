using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class PlayerDTO
    {
        public string FirstName { set; get; }
        public string LastName { set; get; }
        public string? FullName { get; set; }
        public string? Team { get; set; }
        public int? Age { get; set; }
        public string? Headshot { get; set; }
        public string? ActionShot { get; set; }
        public int? Salary { set; get; }
        public int? Length { set; get; }
        public string? Position { set; get; }
        public int MflId { set; get; }
        public int? ContractValue { set; get; }
        public int? MflFranchiseId { set; get; }
        public decimal? Adp { get; set; }
        public decimal? LastSeasonPts { get; set; }
        public string? RosterStatus { get; set; }
    }
    
    public class PlayerBioDTO 
    {
        public string FirstName { set; get; }
        public string LastName { set; get; }
        public string MflId { set; get; }
        public string? Team { get; set; }
        public int? Age { get; set; }
        public int? Height { get; set; }
        public int? Weight { get; set; }
        public string College { get; set; }
        public string DraftYear { get; set; }
        public string DraftRound { get; set; }
        public string DraftPick { get; set; }
        public string? Position { set; get; }
        public string? ActionShot { get; set; } //action?
        public int LastSeasonSalary { get; set; }
        public string PrevOwner { set; get; }
        public List<MflBioPositionRank> PositionRanks { get; set; }
        
    }

    public class MflBioPositionRank
    {
        public int Year { get; set; }
        public decimal Points { get; set; }
        public int? Rank { get; set; }
    }
}