using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class DeadCapData
    {
        public int StartingYear = 2020;
        public string Team { get; set; }
        public Dictionary<string, decimal> Amount { get; set; }
        public int FranchiseId { get; set; }

        public DeadCapData(int id, string name)
        {
            Amount = new Dictionary<string, decimal> { {(StartingYear).ToString(), 0} };
            FranchiseId = id;
            Team = name;
        }

        public void AddPenalties(int yearOfTransaction, decimal amount, int numOfYears)
        {
            var indicesRequired = yearOfTransaction /*2022*/ + numOfYears /*3*/ - StartingYear;  // 2025 - 2020 = 5
            //if (Amount.Count < indicesRequired)

            // var addsNeeded = indicesRequired - Amount.Count;
            for (int x = 0; x < indicesRequired; x++)
            {
                //for each addNeeded, add a year to starting year with a 0 amount
                if(!Amount.ContainsKey((StartingYear + x).ToString()))
                    Amount.Add((StartingYear + x).ToString(), 0);
            }

            
            for (int x = 0; x < numOfYears; x++)
            {
                Amount[(yearOfTransaction + x).ToString()] += amount;
            }
        }
    }
    
}