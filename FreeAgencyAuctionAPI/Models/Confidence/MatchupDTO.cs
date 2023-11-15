using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models.Confidence
{
    public class NflMatchupDTO
    {
        public int Id { get; set; }
        public NflTeamDTO Left { get; set; }
        public NflTeamDTO Right { get; set; }
        public int Year { get; set; }
        public int Week { get; set; }
        public NflTeamDTO? Winner { get; set; }
        public bool Pickable { get; set; }
        public NflPicksDTO Pick { get; set; }
    }
    public class NflTeamDTO
    {
        public string Tricode { get; set; }
        public string City { get; set; }
        public string Name { get; set; }
        public string Primary { get; set; }
        public string Secondary { get; set; }
        public string Logo { get; set; }
        public string SecondaryLogo { get; set; }
    }

    public class NflPicksDTO
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public int MatchupId { get; set; }
        public string Choice { get; set; }
        public int Points { get; set; }
    }

    public class ConfidencePoolResultsResponse
    {
        public IEnumerable<ConfidencePlayerResult> PoolResults { get; set; }
    }
    public class ConfidencePlayerResult
    {
        public bool PickSubmitted { get; set; }
        public int Rank { get; set; }
        public string Avatar { get; set; }
        public string DisplayName { get; set; }
        public int OwnerId { get; set; }
        public int TotalPoints { get; set; } = 0;
        public IEnumerable<WeeklyConfidenceResult> WeeklyResults { get; set; }
    }
    public class WeeklyConfidenceResult
    {
        public int Week { get; set; }
        public int TotalPoints { get; set; } = 0;
        public IEnumerable<PickResult> Results { get; set; }
    }
    public class PickResult : NflPicksDTO
    {
        public bool Correct { get; set; }
        public NflTeamDTO PickTeam { get; set; }

    }
    public class ConfidenceHomeResponse
    {
        public List<NflMatchupDTO> Matchups { get; set; }
        public IEnumerable<ConfidencePlayerResult> Results { get; set; }
    }
}
