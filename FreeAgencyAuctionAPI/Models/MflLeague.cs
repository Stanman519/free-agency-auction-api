using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class Franchise
    {
        public string icon { get; set; }
        public string abbrev { get; set; }
        public string name { get; set; }
        public string waiverSortOrder { get; set; }
        public string logo { get; set; }
        public string salaryCapAmount { get; set; }
        public string id { get; set; }
        public string stadium { get; set; }
        public string email { get; set; }
        public string username { get; set; }
        public string owner_name { get; set; }
        public int leagueId { get; set; } // doesn't come from API, my use only
    }

    public class Franchises
    {
        public string count { get; set; }
        public List<Franchise> franchise { get; set; }
    }

    public class League2
    {
        public string url { get; set; }
        public string year { get; set; }
        public string victoryPointsEndWeek { get; set; }
        public string currentWaiverType { get; set; }
        public string playerLimitUnit { get; set; }
        public string taxiSquad { get; set; }
        public string endWeek { get; set; }
        public string maxWaiverRounds { get; set; }
        public string draft_kind { get; set; }
        public string lockout { get; set; }
        public string defaultTradeExpirationDays { get; set; }
        public string nflPoolStartWeek { get; set; }
        public string victoryPointsTie { get; set; }
        public Franchises franchises { get; set; }
        public string includeTaxiWithContractYear { get; set; }
        public string standingsSort { get; set; }
        public string draftPlayerPool { get; set; }
        public string id { get; set; }
        public string nflPoolType { get; set; }
        public string includeIRWithContractYear { get; set; }
        public History history { get; set; }
        public string rosterSize { get; set; }
        public string name { get; set; }
        public string draftTimer { get; set; }
        public string fantasyPoolType { get; set; }
        public RosterLimits rosterLimits { get; set; }
        public string includeIRWithSalary { get; set; }
        public string mobileAlerts { get; set; }
        public string draftLimitHours { get; set; }
        public string victoryPointsBuckets { get; set; }
        public Starters starters { get; set; }
        public string includeTaxiWithSalary { get; set; }
        public string fantasyPoolEndWeek { get; set; }
        public string nflPoolEndWeek { get; set; }
        public string bestLineup { get; set; }
        public string precision { get; set; }
        public string victoryPointsStartWeek { get; set; }
        public string survivorPool { get; set; }
        public string lastRegularSeasonWeek { get; set; }
        public string usesContractYear { get; set; }
        public string injuredReserve { get; set; }
        public string salaryCapAmount { get; set; }
        public string startWeek { get; set; }
        public string victoryPointsLoss { get; set; }
        public string survivorPoolStartWeek { get; set; }
        public string fantasyPoolStartWeek { get; set; }
        public string survivorPoolEndWeek { get; set; }
        public string rostersPerPlayer { get; set; }
        public string h2h { get; set; }
        public string usesSalaries { get; set; }
        public string victoryPointsWin { get; set; }
        public string baseURL { get; set; }
        public string loadRosters { get; set; }
    }

    public class History
    {
        public List<HistoryLeague> league { get; set; }
    }

    public class HistoryLeague
    {
        public string url { get; set; }
        public string year { get; set; }
    }

    public class Position
    {
        public string name { get; set; }
        public string limit { get; set; }
    }

    public class RosterLimits
    {
        public List<Position> position { get; set; }
    }

    public class Starters
    {
        public string count { get; set; }
        public List<Position> position { get; set; }
        public string iop_starters { get; set; }
    }

    public class LeagueRoot
    {
        public string version { get; set; }
        public League2 league { get; set; }
        public string encoding { get; set; }
    }
}