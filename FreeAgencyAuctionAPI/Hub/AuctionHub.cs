using System;
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
            await Clients.All.SendAsync("FreshBid", freshBid);
        }

        public async Task SendNewHeadline(HeadlineDTO headline)
        {
            await Clients.All.SendAsync("NewHeadline", headline);
        }

        public async Task SendNewQuote(OwnerQuoteDTO quote)
        {
            await Clients.All.SendAsync("NewQuote", quote);
        }

        public async Task SendQuoteRemoved(int leagueId, int quoteId)
        {
            await Clients.All.SendAsync("QuoteRemoved", new { leagueId, quoteId });
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