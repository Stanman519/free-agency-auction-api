namespace FreeAgencyAuctionAPI.Models
{
    public class ErrorResponse
    {
        public string FriendlyMessage { get; set; }

        public ErrorResponse()
        {
        }

        public ErrorResponse(string friendlyMessage)
        {
            FriendlyMessage = friendlyMessage;
        }
    }
}