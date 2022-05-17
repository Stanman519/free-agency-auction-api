using System.Threading.Tasks;
using RestEase;

namespace FreeAgencyAuctionAPI.Repos
{
    public interface ISharkApi
    {
        [Get("SeasonProjections.php?pos={pos}&format=json")]
        Task<SharkPlayerProjection[]> GetSharkProjectionsByPosition([Path] string pos);
    }
    public class SharkPlayerProjection
    {
        public int Rank { get; set; }
        public int? ADP { get; set; }
        public string ID { get; set; }
        public string Name { get; set; }
        public string Pos { get; set; }
        public string Team { get; set; }
        public string Bye { get; set; }
        public string Comp { get; set; }
        public string PassYards { get; set; }
        public int PassTD { get; set; }
        public string Int { get; set; }
        public string Att { get; set; }
        public string RushYards { get; set; }
        public int RushTD { get; set; }
        public string Fum { get; set; }
        public string Rec { get; set; }
        public string RecYards { get; set; }
        public int RecTD { get; set; }
        public int FantasyPoints { get; set; }
    }
}

