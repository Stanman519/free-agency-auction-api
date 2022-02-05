using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using Microsoft.AspNetCore.SignalR;

namespace FreeAgencyAuctionAPI.Hub
{
    public interface IAuctionHub
    {
        Task SendFreshBid(int questionId, int score);
    }
    public class AuctionHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public AuctionHub()
        {
            
        }
        // update bid for client
        public async Task SendFreshBid(BidDTO freshBid)
        {
            Console.WriteLine(freshBid.Ownername);
            Console.WriteLine(freshBid.Player.FirstName);
            await Clients.All.SendAsync("FreshBid", freshBid);
        }
        
        
        
        /* What do we need signalR for:
         * -telling logged on users when a new bid comes in
         * -managing wins? or is the current setup fine? (probably not - two logged in users could force two wins?  do we check for that?)
         *
         *
         *
         *
         * 
         */
        // update win? for client
    }
}