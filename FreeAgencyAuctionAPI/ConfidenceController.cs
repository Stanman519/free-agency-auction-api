namespace FreeAgencyAuctionAPI
{
    using AutoMapper;
    using FreeAgencyAuctionAPI.Models;
    using global::FreeAgencyAuctionAPI.Models;
    using global::FreeAgencyAuctionAPI.Models.Confidence;
    using global::FreeAgencyAuctionAPI.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using RestEase;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    namespace FreeAgencyAuctionAPI
    {
        [ApiController]
        [Route("confidence")]
        public class ConfidenceController : ControllerBase
        {

            private readonly ILogger<ConfidenceController> _logger;
            private AuctionContext _db;
            private readonly IMapper _mapper;

            public ConfidenceController(ILogger<ConfidenceController> logger, AuctionContext db, IMapper mapper)
            {

                _logger = logger;
                _db = db;
                _mapper = mapper;
            }

            [HttpGet("nfl-teams")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetAllNflTeams()
            {
                var teams = _mapper.Map<NflTeamDTO>(_db.NflTeams.ToList());
                return Ok(teams);
            }

            [HttpGet("home")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetPageLoadResponse([Query] int year = Utils.ThisYear)
            {
                //switch for demo to take negative pool?
                var resp = new ConfidenceHomeResponse();
                // look for matchups in db with the latest week but not decided  (need some sort of LOCKED) mechanism
                // if the matchups are ready for picking, send them back.  If they're live, send them back and let the client handle non form mode

                var dbMatchups = _db.NflTeamMatchups.Where(_ => _.Year == year).ToList();
                resp.Matchups = dbMatchups.Where(m => m.Pickable).Select(_ => _mapper.Map<NflMatchupDTO>(_)).ToList();
                resp.Results = _db.NflPicks.Where(_ => _.NflTeamMatchup.Year == year)
                    .GroupBy(_ => _.OwnerId)
                    .Select(_ => new ConfidencePlayerResult
                    {
                        DisplayName = _.FirstOrDefault().Owner.Displayname ?? "",
                        OwnerId = _.Key,
                        TotalPoints = _.Sum(pk => pk.Choice == pk.NflTeamMatchup.Winner ? pk.Points : 0),
                        WeeklyResults = _.GroupBy(pk => pk.NflTeamMatchup.Week).Select(wk => new WeeklyConfidenceResult
                        {
                            Week = wk.Key,
                            TotalPoints = wk.Sum(r => r.Choice == r.NflTeamMatchup.Winner ? r.Points : 0),
                            Results = wk.Select(wRes => new PickResult
                            {
                                Id = wRes.Id,
                                OwnerId = wRes.OwnerId,
                                MatchupId = wRes.MatchupId,
                                Choice = wRes.Choice,
                                Points = wRes.Points,
                                Correct = wRes.NflTeamMatchup.Winner == wRes.Choice,
                                PickTeam = _mapper.Map<NflTeamDTO>(wRes.ChosenTeam)
                            })
                        })
                    }).ToList();
                return Ok(resp);
            }

            [HttpGet("matchups")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetCurrentMatchupsForm([Query] int year = Utils.ThisYear, [Query] string user = "")
            {
                //switch for demo to take negative pool?

                // look for matchups in db with the latest week but not decided  (need some sort of LOCKED) mechanism
                // if the matchups are ready for picking, send them back.  If they're live, send them back and let the client handle non form mode

                var dbMatchups = _db.NflTeamMatchups.Where(_ => _.Year == year).ToList();
                var thisWeek = dbMatchups.GroupBy(m => m.Week).OrderByDescending(m => m.Key).First().Select(_ => _mapper.Map<NflMatchupDTO>(_)).ToList(); // can't do this serverside because groupby => orderby doesnt work on EFCore?
                if (!string.IsNullOrEmpty(user) && thisWeek.Any(m => m.Pickable))
                {
                    var userPicks = _db.NflPicks.Where(p => p.Owner.authid == user && thisWeek.Select(w => w.Id).Contains(p.NflTeamMatchup.Id)).OrderByDescending(p => p.Points).ToList();
                    thisWeek.ForEach(mat =>
                    {
                        var dbPick = userPicks.FirstOrDefault(p => p.MatchupId == mat.Id);
                        if (dbPick != null) mat.Pick = _mapper.Map<NflPicksDTO>(dbPick);
                    });
                }
                return Ok(thisWeek);

            }
            [HttpPost("admin/new-matchups")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> PostPickableMatchups([Body] List<NflMatchupDTO> matchups)
            {
                var dbMatchups = _mapper.Map<List<NflTeamMatchup>>(matchups);
                dbMatchups.ForEach(matchup => matchup.Pickable = true);
                try
                {
                    _db.NflTeamMatchups.AddRange(dbMatchups);
                    _db.SaveChanges();
                }
                catch (System.Exception e)
                {
                    return BadRequest(new ErrorResponse(e.Message));
                }

                return Ok(dbMatchups);
            }
            [HttpPost("admin/matchups/{matchupId}/results/{winningTricode}")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> PostRealMatchupWinner([Path] int matchupId, [Path] string winningTricode)
            {
                var dbMatchupToUpdate = _db.NflTeamMatchups.FirstOrDefault(m => m.Id == matchupId);
                if (dbMatchupToUpdate != null) 
                {
                    try
                    {
                        dbMatchupToUpdate.Winner = winningTricode;
                        _db.SaveChanges();
                    }
                    catch (System.Exception e)
                    {
                        return BadRequest(e.Message);
                    }    
                }
                return Ok();
            }

            [HttpPost("lock-matchups")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> MakeAllMatchupsUnpickable([Query] int year = Utils.ThisYear)
            {
                //Figure out a way to make this only doable by commish
                var matchups = _db.NflTeamMatchups.Where(m => m.Year == year).ToList();
                matchups.ForEach(m => m.Pickable = false);
                _db.SaveChanges();
                return Ok(matchups);
            }

            [HttpPost("picks")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> SaveOrOverwriteMyPicks([Body] List<NflPicksDTO> picks)
            {
                // need to know who sent it - part of the body
                // get picks that match user, week, year
                // if there's none, save these new ones

                // or does it not matter if there are extra in the db? (could just get latest picks because you ahve to submit all at the same timee)
                if (!picks.Any() || picks[0] == null || picks[0]?.OwnerId == null || picks[0]?.MatchupId == null) return BadRequest("invalid matchup or profile.");
                var existingPicks = _db.NflPicks.Where(p => picks.Select(_ => _.MatchupId).Contains(p.MatchupId) && picks.First().OwnerId == p.OwnerId).ToList();
                if (existingPicks.Any())
                {
                    //overwrite
                    if (existingPicks.Count != picks.Count) { }// i dont know but should do something like add the ones that dont match. but why would that happen
                    else
                    {
                        existingPicks.ForEach(p =>
                        {
                            p.Choice = picks.FirstOrDefault(pick => pick.MatchupId == p.MatchupId).Choice;
                        });
                        _db.SaveChanges();
                    }

                }
                else
                {
                    //insert
                    var entities = _mapper.Map<List<Pick>>(picks);
                    _db.NflPicks.AddRange(entities);
                    _db.SaveChanges();
                }
                return Ok();
            }

            [HttpGet("results")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetCurrentPoolResults([Query] int year = 2023)
            {
                //var rawPoolMatchups = _db.NflTeamMatchups.Where(_ => _.Year == year).ToList();
                var results = _db.NflPicks.Where(_ => _.NflTeamMatchup.Year == year)
                    .GroupBy(_ => _.OwnerId)
                    .Select(_ =>  new ConfidencePlayerResult
                        {
                            PickSubmitted = _.Any(p => (p.NflTeamMatchup.Pickable && !string.IsNullOrEmpty(p.Choice)) ? true : 
                                (_.All(p => !p.NflTeamMatchup.Pickable) && _.Any(p => string.IsNullOrEmpty(_.OrderByDescending(p => p.NflTeamMatchup.Week).FirstOrDefault().Choice)))),
                            Avatar = _.FirstOrDefault().Owner.Avatar,
                            DisplayName = _.FirstOrDefault().Owner.Displayname ?? "",
                            OwnerId = _.Key,
                            TotalPoints = _.Sum(pk => pk.Choice == pk.NflTeamMatchup.Winner ? pk.Points : 0),
                            WeeklyResults = _.GroupBy(pk => pk.NflTeamMatchup.Week).Select(wk => new WeeklyConfidenceResult
                            {
                                Week = wk.Key,
                                TotalPoints = wk.Sum(r => r.Choice == r.NflTeamMatchup.Winner ? r.Points : 0),
                                Results = wk.Select(wRes => new PickResult
                                {
                                    Id = wRes.Id,
                                    OwnerId = wRes.OwnerId,
                                    MatchupId = wRes.MatchupId,
                                    Choice = wRes.Choice,
                                    Points = wRes.Points,
                                    Correct = wRes.NflTeamMatchup.Winner == wRes.Choice,
                                    PickTeam = _mapper.Map<NflTeamDTO>(wRes.ChosenTeam)
                                })
                            })
                    }
                    )
                    .ToList(); //rawPoolMatchups.Select(m => m.Id).Contains(_.MatchupId)).ToList();
                var scores = results.Select(r => r.TotalPoints).ToList();
                results.ForEach(res =>
                {
                    res.Rank = (from s in scores where s > res.TotalPoints select s).Count() + 1;
                });
                var ret = new ConfidencePoolResultsResponse
                {
                    PoolResults = results.OrderByDescending(r => r.TotalPoints)
                };
                return Ok(results);

            }
        }
    }

}
