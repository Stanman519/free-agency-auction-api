namespace FreeAgencyAuctionAPI
{
    public class AppConfig
    {
        public AzureMessageQueue AzureMessageQueue { get; set; }
        public string SqlServerConnectionString { get; set; }
        public BingImageApi BingImageApi { get; set; }
        public MflKeys Mfl { get; set; }
        public StreamClient StreamClient { get; set; }
    }
    public class AzureMessageQueue
    {
        public string AzureStorageConnectionString { get; set; }
    }
    public class BingImageApi
    {
        public string BingSubscriptionKey { get; set; }
    }
    public class MflKeys
    {
        public string CommishCookie { get; set; }
        public string MflApiKey { get; set; }
    }
    public class StreamClient
    {
        public string StreamKey { get; set; }
        public string StreamPassword { get; set; } 
    }
}
