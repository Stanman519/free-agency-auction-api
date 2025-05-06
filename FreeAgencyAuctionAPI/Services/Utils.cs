using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Services
{
    public static class Utils
    {
        public const int ThisYear = 2025;
        public static Dictionary<int, string> Owners = new()
        {
            {1, "Ryan"},
            {2, "tylerwelsh"},
            {3, "Leb"},
            {4, "caboroberts"},
            {5,"turley69"},
            {6,"CrappieDuster"},
            {7,"cory"},
            {8,"jeremimattern"},
            {9,"Not a noob"},
            {10,"Flapjackcarl"},
            {11,"Juanard"},
            {12,"dkirsch16"}
        };
        public static Dictionary<int, string> leagueBotDict = new Dictionary<int, string>{
            { 13894, "REDACTED_GROUPME_BOT" },
            { 26548, "REDACTED_GROUPME_BOT"}
        };

        public static Dictionary<string, PositionAgeCliff> AgeCliffs = new()
        {
            {
                "QB", new()
                {
                    BonusThreshhold = 2,
                    Low = 25,
                    High = 30
                }
            },
            {
                "RB", new()
                {
                    BonusThreshhold = 1,
                    Low = 25,
                    High = 26
                }
            },
            {
                "WR", new()
                {
                    BonusThreshhold = 1,
                    Low = 26,
                    High = 27
                }

            },
            {
                "TE", new()
                {
                    BonusThreshhold = 2,
                    Low = 25,
                    High = 27
                }
            }
        };
        public static List<PositionProjectionRange> PositionRanges = new()
        {
            new()
            {
                Position = "QB",
                RankMax = 12,
                RankMin = 1,
                SalaryMed = 60,
                SalaryUpper = 80
            },
            new()
            {
                Position = "QB",
                RankMax = 24,
                RankMin = 13,
                SalaryMed = 25,
                SalaryUpper = 32
            },
            new()
            {
                Position = "QB",
                RankMax = 36,
                RankMin = 25,
                SalaryMed = 19,
                SalaryUpper = 30
            },            
            new()
            {
                Position = "QB",
                RankMax = 37,
                RankMin = 48,
                SalaryMed = 6,
                SalaryUpper = 10
            },
            new()
            {
                Position = "QB",
                RankMax = 49,
                RankMin = int.MaxValue,
                SalaryMed = 1,
                SalaryUpper = 1
            },
            new()
            {
                Position = "RB",
                RankMax = 12,
                RankMin = 1,
                SalaryMed = 68,
                SalaryUpper = 80
            },
            new()
            {
                Position = "RB",
                RankMax = 24,
                RankMin = 13,
                SalaryMed = 31,
                SalaryUpper = 38
            },
            new()
            {
                Position = "RB",
                RankMax = 36,
                RankMin = 25,
                SalaryMed = 14,
                SalaryUpper = 22
            },
            new()
            {
                Position = "RB",
                RankMax = 48,
                RankMin = 37,
                SalaryMed = 10,
                SalaryUpper = 24
            },
            new()
            {
                Position = "RB",
                RankMax = 60,
                RankMin = 49,
                SalaryMed = 7,
                SalaryUpper = 12
            },
            new()
            {
                Position = "RB",
                RankMax = int.MaxValue,
                RankMin = 61,
                SalaryMed = 1,
                SalaryUpper = 1
            },
            new()
            {
                Position = "WR",
                RankMax = 12,
                RankMin = 1,
                SalaryMed = 35,
                SalaryUpper = 66
            },
            new()
            {
                Position = "WR",
                RankMax = 24,
                RankMin = 13,
                SalaryMed = 26,
                SalaryUpper = 56
            },
            new()
            {
                Position = "WR",
                RankMax = 36,
                RankMin = 25,
                SalaryMed = 17,
                SalaryUpper = 35
            },
            new()
            {
                Position = "WR",
                RankMax = 48,
                RankMin = 37,
                SalaryMed = 20,
                SalaryUpper = 32
            },
            new()
            {
                Position = "WR",
                RankMax = 60,
                RankMin = 49,
                SalaryMed = 6,
                SalaryUpper = 23
            },
            new()
            {
                Position = "WR",
                RankMax = 72,
                RankMin = 61,
                SalaryMed = 6,
                SalaryUpper = 14
            },
            new()
            {
                Position = "WR",
                RankMax = 84,
                RankMin = 73,
                SalaryMed = 2,
                SalaryUpper = 10
            },
            new()
            {
                Position = "WR",
                RankMax = int.MaxValue,
                RankMin = 85,
                SalaryMed = 1,
                SalaryUpper = 1
            },
            new()
            {
                Position = "TE",
                RankMax = 12,
                RankMin = 1,
                SalaryMed = 22,
                SalaryUpper = 50
            },
            new()
            {
                Position = "TE",
                RankMax = 24,
                RankMin = 13,
                SalaryMed = 14,
                SalaryUpper = 22
            },
            new()
            {
                Position = "TE",
                RankMax = 36,
                RankMin = 25,
                SalaryMed = 1,
                SalaryUpper = 5
            },
            new()
            {
                Position = "TE",
                RankMax = int.MaxValue,
                RankMin = 37,
                SalaryMed = 1,
                SalaryUpper = 1
            },
        };

    }

    public class PositionAgeCliff
    {
        public int BonusThreshhold { get; set; }
        public int Low { get; set; }
        public int High { get; set; }
    }
    public class PositionProjectionRange
    {
        public string Position { get; set; }
        public int RankMin { get; set; }
        public int RankMax { get; set; }
        public int SalaryMed { get; set; }
        public int SalaryUpper { get; set; }
        
    }
    
    
}