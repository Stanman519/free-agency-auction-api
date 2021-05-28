using System.Collections.Generic;

namespace FreeAgencyAuctionAPI.Models
{
    public class SalaryAdjustment
    {
        public string amount { get; set; }
        public string timestamp { get; set; }
        public string franchise_id { get; set; }
        public string id { get; set; }
        public string description { get; set; }
    }

    public class SalaryAdjustments
    {
        public List<SalaryAdjustment> salaryAdjustment { get; set; }
    }

    public class SalaryAdjustmentsRoot
    {
        public string version { get; set; }
        public SalaryAdjustments salaryAdjustments { get; set; }
        public string encoding { get; set; }
    }
}