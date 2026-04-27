using System;
using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Services
{
    public static class Utils
    {
        public static int CurrentYear => DateTime.UtcNow.Year;
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
        public static Dictionary<int, int> draftPicks = new Dictionary<int, int>
        {
            {1, 30},
            {2, 28},
            {3, 26},
            {4, 24},
            {5, 22},
            {6, 22},
            {7, 22},
            {8, 22},
            {9, 20},
            {10, 20},
            {11, 20},
            {12, 20},
            {13, 18},
            {14, 18},
            {15, 18},
            {16, 18},
            {17, 16},
            {18, 16},
            {19, 16},
            {20, 16},
            {21, 14},
            {22, 14},
            {23, 14},
            {24, 14},
            {25, 12},
            {26, 12},
            {27, 12},
            {28, 12},
            {29, 10},
            {30, 10},
            {31, 10},
            {32, 10},
            {33, 8},
            {34, 8},
            {35, 8},
            {36, 8},
            {37,  6}
        };
        public static Dictionary<int, int> rbDraftPicks = new Dictionary<int, int>
        {
            {1, 36},
            {2, 34},
            {3, 31},
            {4, 28},
            {5, 26},
            {6, 26},
            {7, 26},
            {8, 26},
            {9, 23},
            {10, 23},
            {11, 23},
            {12, 23},
            {13, 21},
            {14, 21},
            {15, 21},
            {16, 21},
            {17, 19},
            {18, 19},
            {19, 19},
            {20, 19},
            {21, 16},
            {22, 16},
            {23, 16},
            {24, 16},
            {25, 14},
            {26, 14},
            {27, 14},
            {28, 14},
            {29, 12},
            {30, 12},
            {31, 12},
            {32, 12},
            {33, 10},
            {34, 10},
            {35, 10},
            {36, 10},
            {37,  7}
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