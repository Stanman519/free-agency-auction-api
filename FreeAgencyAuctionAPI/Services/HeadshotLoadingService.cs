using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Newtonsoft.Json;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IHeadshotLoadingService
    {
        Task<List<HeadshotPlayer>> ParseHeadshots();
        int? GetAgeInt(string birthdate);
    }

    public class HeadshotLoadingService : IHeadshotLoadingService
    {

        public async Task<List<HeadshotPlayer>> ParseHeadshots()
        {
            using (StreamReader file = File.OpenText(@"playerheadshots.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                var rawHeadshots = (List<HeadshotRoot>)serializer.Deserialize(file, typeof(List<HeadshotRoot>));
                var cleanHeadshots = rawHeadshots.Select(h => new HeadshotPlayer
                {
                    FirstName = h.firstName,
                    FullName = h.fullName,
                    Headshot = h.headshot?.href ?? "",
                    LastName = h.lastName,
                    Position = h.position?.abbreviation ?? ""
                }).ToList();
                return cleanHeadshots;
            }

        }

        public int? GetAgeInt(string birthdate)
        {
            return Convert.ToInt32(Math.Floor(
                (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(Int32.Parse(birthdate))).TotalDays / 365));
        }
    }
}