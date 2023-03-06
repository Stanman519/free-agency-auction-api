using System.Text.Json.Serialization;

namespace FreeAgencyAuctionAPI.Models
{
    public class AuthUser
    {
        public string Name { get; set; }
        [JsonPropertyName("given_name")]
        public string GivenName { get; set; }
        [JsonPropertyName("family_name")]
        public string FamilyName { get; set; }
        [JsonPropertyName("middle_name")]
        public string MiddleName { get; set; }
        public string Nickname { get; set; }
        [JsonPropertyName("preferred_username")]
        public string PreferredUsername { get; set; }
        public string Profile { get; set; }
        public string Picture { get; set; }
        public string Website { get; set; }
        public string Email { get; set; }
        public string Birthdate { get; set; }
        public string ZoneInfo { get; set; }
        public string Locale { get; set; }
        public string Gender { get; set; }
        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }
        public string Sub { get; set; }
    }
}
