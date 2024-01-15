namespace FreeAgencyAuctionAPI
{
    using AutoMapper;
    using Bogus;
    using global::FreeAgencyAuctionAPI.Models;
    using global::FreeAgencyAuctionAPI.Models.Confidence;
    using global::FreeAgencyAuctionAPI.Repos;
    using global::FreeAgencyAuctionAPI.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Identity.Client;
    using RestEase;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Utils = Services.Utils;

    namespace FreeAgencyAuctionAPI
    {
        [ApiController]
        [Route("confidence")]
        public class ConfidenceController : ControllerBase
        {

            private readonly ILogger<ConfidenceController> _logger;
            private AuctionContext _db;
            private readonly IMapper _mapper;
            private readonly IGMBot _gm;

            public ConfidenceController(ILogger<ConfidenceController> logger, AuctionContext db, IMapper mapper, IGMBot gm)
            {

                _logger = logger;
                _db = db;
                _mapper = mapper;
                _gm = gm;
            }

            [HttpGet("ping")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> KeepTheLightsOn()
            {
                return Ok("Hello");
            }

            [HttpGet("nfl-teams")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetAllNflTeams()
            {
                var teams = _mapper.Map<List<NflTeamDTO>>(_db.NflTeams.ToList());
                return Ok(teams);
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
                var dbProps = _db.Props.Where(_ => _.Year == year).ToList();
                var thisWeek = dbMatchups.GroupBy(m => m.Week).OrderByDescending(m => m.Key).FirstOrDefault()?.Select(_ => _mapper.Map<NflMatchupDTO>(_)).ToList(); // can't do this serverside because groupby => orderby doesnt work on EFCore?
                var propsThisWeek = dbProps.GroupBy(p => p.Week).OrderByDescending(p => p.Key).FirstOrDefault()?.Select(p => _mapper.Map<PropDTO>(p)).ToList();
                var props = dbProps.GroupBy(m => m.Week).OrderByDescending(m => m.Key).FirstOrDefault()?.Select(_ => _mapper.Map<PropDTO>(_)).ToList() ?? new List<PropDTO>();
                if (!string.IsNullOrEmpty(user))
                {
                    var userPicks = _db.NflPicks.Where(p => p.Owner.authid == user && thisWeek.Select(w => w.Id).Contains(p.NflTeamMatchup.Id)).OrderByDescending(p => p.Points).ToList();
                    var userProps = _db.ExtraPicks.Where(p => p.Owner.authid == user && propsThisWeek.Select(w => w.Id).Contains(p.PropId)).ToList();

                    thisWeek.ForEach(mat =>
                    {
                        var dbPick = userPicks.FirstOrDefault(p => p.MatchupId == mat.Id);
                        if (dbPick != null) mat.Pick = _mapper.Map<NflPicksDTO>(dbPick);
                    });
                    thisWeek = thisWeek.OrderByDescending(mat => mat.Pick?.Points).ToList();

                    props.ForEach(p =>
                    {
                        var dbProp = userProps.FirstOrDefault(db => db.PropId == p.Id);
                        if (dbProp != null) p.Pick = _mapper.Map<PropPickDTO>(dbProp);
                    });
                    }
                return Ok(new MatchupForm
                {
                    Matchups = thisWeek,
                    Props = props,
                });

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
                catch (Exception e)
                {
                    return BadRequest(new ErrorResponse(e.Message));
                }

                return Ok(dbMatchups);
            }
            [HttpPost("admin/new-props")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> PostPickableProps([Body] List<PropDTO> props)
            {

                var dbProps = _mapper.Map<List<Prop>>(props);
                dbProps.ForEach(matchup => matchup.Pickable = true);
                try
                {
                    _db.Props.AddRange(dbProps);
                    _db.SaveChanges();
                }
                catch (Exception e)
                {
                    return BadRequest(new ErrorResponse(e.Message));
                }
                return Ok(dbProps);
            }

            [HttpGet("admin/unpaid")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetUnpaidOwners()
            {

                var owners = _db.Owners.Where(o => !o.istest && !o.ConfidencePaid).ToList();
                var ret = _mapper.Map<List<OwnerDTO>>(owners);
                return Ok(ret);
            }

            [HttpPost("admin/mark-paid")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> MarkOwnerAsPaid([Body] List<int> ownerIds)
            {
                var editOwners = _db.Owners.Where(o => ownerIds.Contains(o.Ownerid)).ToList();
                editOwners.ForEach(o =>
                {
                    o.ConfidencePaid = true;
                });

                try
                {
                    _db.SaveChanges();
                }
                catch (Exception e)
                {
                    return BadRequest(new ErrorResponse(e.Message));
                }
                return Ok();
            }

            [HttpPost("admin/props/{propId}/results/{winningOption}")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> PostPropAnswer([Path] int propId, [Path] string winningOption)
            {
                var dbMatchupToUpdate = _db.Props.FirstOrDefault(m => m.Id == propId);
                if (dbMatchupToUpdate == null) return BadRequest(new ErrorResponse("Wrong id"));
                if (winningOption != "A" && winningOption != "B") return BadRequest(new ErrorResponse("wrong winning option input method"));
                try
                {
                    dbMatchupToUpdate.Winner = winningOption;
                    _db.SaveChanges();
                }
                catch (Exception e)
                {
                    return BadRequest(e.Message);
                }
              
                return Ok();
            }

            [HttpGet("year/{year}/week/{week}/coummunity-stats")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetCommunityStats(int year, int week)
            {


                var allPicksThisWeek = _db.NflPicks.Where(p => p.NflTeamMatchup.Week ==  week && p.NflTeamMatchup.Year == year).ToList().GroupBy(p => p.MatchupId);
                var isStillPickable = false;
                //var matchupStats = new List<MatchupCommunityStats>();
                
                var matchupStats = allPicksThisWeek.Select(mup =>
                {
                    decimal count = 0;
                    decimal leftPicks = 0;
                    decimal rightPicks = 0;
                    decimal leftTotalPts = 0;
                    decimal rightTotalPts = 0;
                    mup.ToList().ForEach(pick =>
                    {
                        if (pick.NflTeamMatchup.Pickable) isStillPickable = true;
                        count++;
                        if (pick.Choice == pick.NflTeamMatchup.Left)
                        {
                            leftPicks++;
                            leftTotalPts += pick.Points;
                        }
                        if (pick.Choice == pick.NflTeamMatchup.Right)
                        {
                            rightPicks++;
                            rightTotalPts += pick.Points;
                        }
                    });
                    return new MatchupCommunityStats
                    {
                        MatchupId = mup.Key,
                        LPct = Math.Round(leftPicks / count,1),
                        RPct = Math.Round(rightPicks / count,1),
                        LAvg = Math.Round(leftPicks == 0 ? 0 : leftTotalPts / leftPicks, 1),
                        RAvg = Math.Round(rightPicks == 0 ? 0 : rightTotalPts / rightPicks, 1)
                    };
                });
                if (isStillPickable)
                {
                    await _gm.NotifyMflError(new ErrorMessage($"Someone is tryna cheat"));
                    return BadRequest(new ErrorResponse("you are trying to access stats for matchups that are not yet locked."));
                }
                return Ok(matchupStats);
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
                    catch (Exception e)
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
                var props = _db.Props.Where(p => p.Year == year).ToList();
                props.ForEach(p => p.Pickable = false);
                matchups.ForEach(m => m.Pickable = false);
                _db.SaveChanges();
                return Ok();
            }

            [HttpPost("picks")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> SaveOrOverwriteMyPicks([Body] NflPickSubmission pickSubmission)
            {

                // need to know who sent it - part of the body
                // get picks that match user, week, year
                // if there's none, save these new ones
                var picks = pickSubmission.Picks;
                var props = pickSubmission.Props;
                // or does it not matter if there are extra in the db? (could just get latest picks because you ahve to submit all at the same timee)
                if (!picks.Any() || picks[0] == null || picks[0]?.OwnerId == null || picks[0]?.MatchupId == null) return BadRequest("invalid matchup or profile.");
                var dbPicks = _db.NflPicks.Where(p => picks.Select(_ => _.MatchupId).Contains(p.MatchupId) && p.OwnerId == picks[0].OwnerId);
               
                if (dbPicks.Any(_ => !_.NflTeamMatchup.Pickable)) return BadRequest(new ErrorResponse("You have submitted picks for matchups that are locked."));
                var existingPicks = dbPicks.Where(p => picks.First().OwnerId == p.OwnerId).ToList();
                if (existingPicks.Any())
                {
                    //overwrite
                    var existingProps = _db.ExtraPicks.Where(p => props.Select(_ => _.PropId).Contains(p.PropId) && p.OwnerId == props[0].OwnerId).ToList();
                    existingPicks.ForEach(p =>
                    {
                        p.Choice = picks.FirstOrDefault(pick => pick.MatchupId == p.MatchupId).Choice;
                    });
                    if (picks.Count > existingPicks.Count)
                    {
                        var dtosToAdd = picks.Where(p => !existingPicks.Select(ep => ep.Id).Contains(p.Id)).ToList();
                        var picksToAdd = _mapper.Map<List<Pick>>(dtosToAdd);
                        _db.NflPicks.AddRange(picksToAdd);
                    }
                    existingProps.ForEach(p =>
                    {
                        p.Choice = props.FirstOrDefault(prop => prop.PropId == p.PropId).Choice;
                    });
                    if (props.Count > existingProps.Count)
                    {
                        var dtosToAdd = props.Where(p => !existingProps.Select(ep => ep.Id).Contains(p.Id)).ToList();
                        var propsToAdd = _mapper.Map<List<ExtraPick>>(dtosToAdd);
                        _db.ExtraPicks.AddRange(propsToAdd);
                    }



                    _db.SaveChanges();
                }
                else
                {
                    //insert
                    var entities = _mapper.Map<List<Pick>>(picks);
                    var extraEntities = _mapper.Map<List<ExtraPick>>(props);
                    _db.NflPicks.AddRange(entities);
                    _db.ExtraPicks.AddRange(extraEntities);
                    _db.SaveChanges();
                }
                return Ok();
            }

            [HttpGet("error")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> ErrorTest()
            {
                return BadRequest(new ErrorResponse("beep boop test.")); 
            }

            [HttpGet("results")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetCurrentPoolResults([Query] int year = 2023)
            {
                var extraPts = _db.ExtraPicks.Where(_ => _.Prop.Year == year).GroupBy(_ => _.OwnerId).ToList();
                var results = _db.NflPicks.Where(_ => _.NflTeamMatchup.Year == year)
                    .GroupBy(_ => _.OwnerId)
                    .ToList()
                    .Select(_ => new ConfidencePlayerResult
                        {
                            PickSubmitted = _.Any(p => (p.NflTeamMatchup.Pickable && !string.IsNullOrEmpty(p.Choice)) ? true :
                                (_.All(p => !p.NflTeamMatchup.Pickable) && _.Any(p => string.IsNullOrEmpty(_.OrderByDescending(p => p.NflTeamMatchup.Week)
                                .FirstOrDefault().Choice)))),
                            Avatar = _.FirstOrDefault().Owner.Avatar,
                            DisplayName = _.FirstOrDefault().Owner.Displayname ?? "",
                            IsPaid = _.FirstOrDefault().Owner.ConfidencePaid,
                            OwnerId = _.Key,
                            TotalPoints = _.Sum(pk => pk.Choice == pk.NflTeamMatchup.Winner ? pk.Points : 0),
                            ExtraPoints = (extraPts.FirstOrDefault(ep => ep.Key == _.Key) == null || extraPts.Count == 0) ? 0 : extraPts.FirstOrDefault(ep => ep.Key == _.Key).Sum(pick => pick.Choice == pick.Prop?.Winner ? 1 : 0),
                            WeeklyResults = _.GroupBy(pk => pk.NflTeamMatchup.Week).Select(wk => new WeeklyConfidenceResult
                                {
                                    Week = wk.Key,
                                    TotalPoints = wk.Sum(r => r.Choice == r.NflTeamMatchup.Winner ? r.Points : 0),
                                    Results = wk.Select(wRes => new PickResult
                                    {
                                        Id = wRes.Id,
                                        OwnerId = wRes.OwnerId,
                                        MatchupId = wRes.MatchupId,
                                        Choice = wRes.NflTeamMatchup.Pickable ? string.Empty : wRes.Choice,
                                        Points = wRes.Points,
                                        Correct = string.IsNullOrEmpty(wRes.NflTeamMatchup.Winner) ? null : wRes.NflTeamMatchup.Winner == wRes.Choice,
                                        PickTeam = (wRes.NflTeamMatchup.Pickable && year != -1) ? null : _mapper.Map<NflTeamDTO>(wRes.ChosenTeam)
                                    }).OrderByDescending(r => r.Points)
                                
                            }).OrderBy(wr => wr.Week)
                    }

                    ).OrderByDescending(r => r.TotalPoints).ToList();
                    
                var scores = results.Select(r => r.TotalPoints + (r.ExtraPoints * 0.1)).ToList();
                results.ForEach(res =>
                {
                    var x = from s in scores where s > (res.TotalPoints + (res.ExtraPoints * 0.1)) select s;
                    res.Rank = x.Count() + 1;
                });
                return Ok(results);
            }

            [HttpGet("demo/generate")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> CreateDemoData([Query] int year = -1)
            {
                // if there's less than 50 owners create 50 fake owners
                if (_db.Owners.Where(o => o.istest).ToList().Count > 50) return BadRequest();
                var users = new Faker<OwnerEntity>()
                    .RuleFor(o => o.Ownername, f => f.Internet.UserName())
                    .RuleFor(o => o.PasswordHash, f => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(f.Internet.Password())))
                    .RuleFor(o => o.Displayname, f => f.Name.FullName())
                    .RuleFor(o => o.Avatar, f => f.Internet.Avatar())
                    .RuleFor(o => o.istest, f => true)
                    .RuleFor(o => o.authid, f => f.Internet.Password())
                    .Generate(20);

                // create 4 matchups for week 1 of this year, 4 matchups for week 2, USE ANY TEAM EXCEPT tricode == FA
                var leftTms = _db.NflTeams.Where(t => t.Tricode != "FA").OrderBy(t => t.Secondary).ToList();
                var rightTms = leftTms.TakeLast(15).ToList();
                leftTms.RemoveRange(15, 15);

                var week1 = leftTms.Take(6).Select((lt, index) => new NflTeamMatchup
                {
                    Left = lt.Tricode,
                    Right = rightTms[index].Tricode,
                    Week = 1,
                    Year = year,
                    Winner = new Random().Next(2) > 0 ? lt.Tricode : rightTms[index].Tricode,
                    Pickable = false
                }).ToList();


                var week2 = week1.Take(4).Select((w, index) => new NflTeamMatchup
                {
                    Week = 2,
                    Year = year,
                    Winner = null,
                    Pickable = true,
                    Left = index < 2 ? leftTms[8 + index].Tricode : w.Winner,
                    Right = index < 2 ? w.Winner : week1[2 + index].Winner
                }).ToList();

                _db.Owners.AddRange(users);
                _db.SaveChanges();

                _db.NflTeamMatchups.AddRange(week1);
                _db.NflTeamMatchups.AddRange(week2);

                _db.SaveChanges();

                var allThePicks = new List<Pick>();
                var points1 = new int[] { 1, 2, 3, 4, 5, 6 };
                var points2 = new int[] { 2, 3, 4, 5, 6, 7 };
                var rng = new Random();
                users.ForEach(u =>
                {

                    rng.Shuffle(points1);
                    var picks1 = week1.Select((m, i) => new Pick
                    {
                        Choice = new Random().Next(2) == 0 ? m.Left : m.Right,
                        MatchupId = m.Id,
                        OwnerId = u.Ownerid,
                        Points = points1[i]
                    }).ToList();
                    allThePicks.AddRange(picks1);
                    if (new Random().Next(2) == 0)
                    {
                        rng.Shuffle(points2);
                        var picks2 = week2.Select((m, i) => new Pick
                        { 
                            Choice = new Random().Next(2) == 0 ? m.Left : m.Right,
                            MatchupId = m.Id,
                            OwnerId = u.Ownerid,
                            Points = points2[i]
                        }).ToList();
                        allThePicks.AddRange(picks2);
                    }
                });

                _db.NflPicks.AddRange(allThePicks);
                _db.SaveChanges();


                return Ok();



            }



        }
    }
    static class RandomExtensions
    {
        public static void Shuffle<T>(this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }
}
