using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace FreeAgencyAuctionAPI.Models
{
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
        public string ConfidenceTitles { get; set; } = "[]";

        [NotMapped]
        public List<int> ConfidenceTitleList =>
            JsonSerializer.Deserialize<List<int>>(ConfidenceTitles) ?? new();
        public bool ConfidencePaid { get; set; }
        public virtual ICollection<LeagueOwnerEntity> Leagueowners { get; } = new List<LeagueOwnerEntity>();
        public virtual ICollection<PoolUser> PoolUsers { get; } = new List<PoolUser>();
        public virtual ICollection<Pick> ConfidencePicks { get; } = new List<Pick>();
        public virtual ICollection<ExtraPick> ExtraPicks { get; } = new List<ExtraPick>();

    }
}
