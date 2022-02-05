namespace FreeAgencyAuctionAPI.Models
{
    public class HeadshotPlayer
    {
        public string Headshot { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
        public string FullName { get; set; }
    }
    
    public class HeadshotPosition
    {
        public string id { get; set; }
        public string name { get; set; }
        public string displayName { get; set; }
        public string abbreviation { get; set; }
    }

    public class Headshot
    {
        public string href { get; set; }
        public string alt { get; set; }
    }
    
    public class HeadshotStatus
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string abbreviation { get; set; }
    }

    public class HeadshotRoot
    {
        public Headshot headshot { get; set; }
        public string jersey { get; set; }
        public HeadshotPosition position { get; set; }
        public HeadshotStatus status { get; set; }
        public bool active { get; set; }
        public string id { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string fullName { get; set; }
    }

}