using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);


    public class Thumbnail
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }

    public class Value
    {
        [JsonProperty("webSearchUrl")]
        public string WebSearchUrl { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("datePublished")]
        public DateTime DatePublished { get; set; }

        [JsonProperty("contentUrl")]
        public string ContentUrl { get; set; }

        [JsonProperty("hostPageUrl")]
        public string HostPageUrl { get; set; }

        [JsonProperty("contentSize")]
        public string ContentSize { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("thumbnail")]
        public Thumbnail Thumbnail { get; set; }
        
        [JsonProperty("accentColor")]
        public string AccentColor { get; set; }
    }


    public class ActionShotQueryResponse
    {
        [JsonProperty("_type")]
        public string Type { get; set; }

        [JsonProperty("readLink")]
        public string ReadLink { get; set; }

        [JsonProperty("webSearchUrl")]
        public string WebSearchUrl { get; set; }

        [JsonProperty("totalEstimatedMatches")]
        public int TotalEstimatedMatches { get; set; }

        [JsonProperty("nextOffset")]
        public int NextOffset { get; set; }
        
        [JsonProperty("currentOffset")]
        public int CurrentOffset { get; set; }

        [JsonProperty("value")]
        public List<Value> Value { get; set; }
        
    }


}