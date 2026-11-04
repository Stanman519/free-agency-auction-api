using System;
using System.ComponentModel.DataAnnotations;

namespace FreeAgencyAuctionAPI.Models
{
    // One row per Auth0 sub (google-oauth2|…, auth0|…) that belongs to an owner. A person who logs in
    // with Google one year and email/password the next has two subs but one owner.
    public partial class OwnerIdentityEntity
    {
        [Key]
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string AuthId { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual OwnerEntity Owner { get; set; } = null!;
    }
}
