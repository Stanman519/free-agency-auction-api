using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace FreeAgencyAuctionAPI.Models
{
    public class MflPlayerDetails
    {
        public string draft_year { get; set; }
        public string draft_round { get; set; }
        public string position { get; set; }
        public string weight { get; set; }
        public string id { get; set; }
        public string draft_team { get; set; }
        public string birthdate { get; set; }
        public string name { get; set; }
        public string draft_pick { get; set; }
        public string college { get; set; }
        public string height { get; set; }
        public string jersey { get; set; }
        public string team { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
    }

    public class MflPlayerDetailsParent
    {
        public string timestamp { get; set; }
        public string since { get; set; }
        [JsonConverter(typeof(SingleOrArrayConverter<MflPlayerDetails>))]
        public List<MflPlayerDetails> player { get; set; }
    }

    public class MflPlayerDetailsRoot
    {
        public string version { get; set; }
        public MflPlayerDetailsParent players { get; set; }
        public string encoding { get; set; }
    }

    class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>();
            }


            return new List<T> { token.ToObject<T>() };
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}