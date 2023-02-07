namespace FreeAgencyAuctionAPI
{
    public class AppConfig
    {
        public AzureMessageQueue QueueConfig { get; set; }
        public string SqlServerConnectionString { get; set; }
        public BingImageApi Bing { get; set; }
        public MflKeys Mfl { get; set; }
        public StreamClient Stream { get; set; }
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
        public string ApiKey { get; set; }
    }
    public class StreamClient
    {
        public string Key { get; set; }
        public string Password { get; set; } 
    }
}
