using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace FreeAgencyAuctionAPI.Services
{
    public interface ILeagueService
    {
        Task<List<DeadCapData>> GetDeadCapData(int leagueId);
        List<TransactionDTO> GetAllTransactions(int leagueId);

    }

    public class LeagueService : ILeagueService
    {
        private readonly IMapper _mapper;
        private AuctionContext _context;
        private readonly ILogger<LeagueService> _logger;



        public LeagueService(IMapper mapper, AuctionContext context, ILogger<LeagueService> logger)
        {
            _mapper = mapper;
            _context = context;
            _logger = logger;
        }

        public async Task<List<DeadCapData>> GetDeadCapData(int leagueId)
        {
            var returnData = new List<DeadCapData>();
            //get all transactions from table and join with franchise to have team names
            var transactions = new List<Transaction>();
            var franchises = new List<LeagueOwnerEntity>();

            try
            {
                transactions = await _context.Transactions.ToListAsync();
                franchises = await _context.LeagueOwners.ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError("entity framework error", e);
                return new List<DeadCapData>();
            }

            var allTransactions = (
                from t in transactions
                join f in franchises on t.Franchiseid equals f.Mflfranchiseid
                select new
                {
                    FranchiseId = t.Franchiseid,
                    TeamName = f.Owner.Displayname,
                    DeadAmount = t.Amount,
                    PlayerName = t.Playername,
                    TransactionYear = t.Yearoftransaction,
                    NumOfYears = t.Years
                }).ToList();

            // go through each transaction - add up amount for each year
            var distinct = allTransactions.GroupBy(t => t.FranchiseId)
                .Select(grp => grp.First())
                .Select(t => new DeadCapData(t.FranchiseId, t.TeamName))
                .ToList();

            distinct.ForEach(t =>
            {
                returnData.Add(new DeadCapData(t.FranchiseId, t.Team));
            });

            allTransactions.ForEach(t =>
            {
                //get year, then get length.  add ammount to list for each year in that span. 0 = 2020
                returnData.FirstOrDefault(_ => _.FranchiseId == t.FranchiseId)?.AddPenalties((int)t.TransactionYear, t.DeadAmount, t.NumOfYears);
            });
            return returnData;
        }

        public List<TransactionDTO> GetAllTransactions(int leagueId)
        {
            try
            {
                var res = _context.Transactions.Where(t => t.Leagueid == leagueId).ToList();
                return _mapper.Map<List<Transaction>, List<TransactionDTO>>(res);
            }
            catch (Exception e)
            {
                _logger.LogError("entity framework error", e);
                return new List<TransactionDTO>();
            }

        }




    }
}