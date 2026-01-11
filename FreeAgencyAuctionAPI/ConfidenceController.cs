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
            private readonly IConfidencePickValidationService _validationService;
            private readonly IAdminAuthorizationService _adminAuthService;

            public ConfidenceController(
                ILogger<ConfidenceController> logger,
                AuctionContext db,
                IMapper mapper,
                IGMBot gm,
                IConfidencePickValidationService validationService,
                IAdminAuthorizationService adminAuthService)
            {
                _logger = logger;
                _db = db;
                _mapper = mapper;
                _gm = gm;
                _validationService = validationService;
                _adminAuthService = adminAuthService;
            }

            // Helper method to decode user parameter
            private string DecodeUserParam(string user)
            {
                if (string.IsNullOrEmpty(user))
                {
                    return user;
                }

                // Decode URL-encoded characters like %7C back to |
                return System.Net.WebUtility.UrlDecode(user);
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
                var dbTeams = await _db.NflTeams.ToListAsync();
                var teams = _mapper.Map<List<NflTeamDTO>>(dbTeams);
                return Ok(teams);
            }


            [HttpGet("matchups")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetCurrentMatchupsForm([FromQuery] int year = Utils.ThisYear, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                var dbMatchups = _db.NflTeamMatchups.Where(_ => _.Year == year).ToList();
                if (!dbMatchups.Any())
                {
                    return Ok(new MatchupForm
                    {
                        Matchups = new List<NflMatchupDTO>(),
                        Props = new List<PropDTO>()
                    });
                }
                var dbProps = _db.Props.Where(_ => _.Year == year).ToList();
                var thisWeek = dbMatchups.GroupBy(m => m.Week).OrderByDescending(m => m.Key).FirstOrDefault()?.Select(_ => _mapper.Map<NflMatchupDTO>(_)).ToList();
                var propsThisWeek = dbProps.GroupBy(p => p.Week).OrderByDescending(p => p.Key).FirstOrDefault()?.Select(p => _mapper.Map<PropDTO>(p)).ToList();
                var props = dbProps.GroupBy(m => m.Week).OrderByDescending(m => m.Key).FirstOrDefault()?.Select(_ => _mapper.Map<PropDTO>(_)).ToList() ?? new List<PropDTO>();

                if (!string.IsNullOrEmpty(user))
                {
                    var userPicks = _db.NflPicks.Where(p => p.Owner.authid == user && thisWeek.Select(w => w.Id).Contains(p.NflTeamMatchup.Id)).OrderByDescending(p => p.Points).ToList();

                    // Only query user props if there are props this week
                    var userProps = propsThisWeek != null && propsThisWeek.Any()
                        ? _db.ExtraPicks.Where(p => p.Owner.authid == user && propsThisWeek.Select(w => w.Id).Contains(p.PropId)).ToList()
                        : new List<ExtraPick>();

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
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> PostPickableMatchups([FromBody] List<NflMatchupDTO> matchups, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} creating {Count} new matchups",
                    authResult.Owner.Ownerid, matchups.Count);

                var dbMatchups = _mapper.Map<List<NflTeamMatchup>>(matchups);
                dbMatchups.ForEach(matchup => matchup.Pickable = true);

                try
                {
                    await _db.NflTeamMatchups.AddRangeAsync(dbMatchups);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Successfully created {Count} matchups", dbMatchups.Count);
                    return Ok(dbMatchups);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error creating matchups");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("Unable to create matchups. Please check the data and try again."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error creating matchups");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("An unexpected error occurred."));
                }
            }

            [HttpPost("admin/new-props")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> PostPickableProps([FromBody] List<PropDTO> props, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} creating {Count} new props",
                    authResult.Owner.Ownerid, props.Count);

                var dbProps = _mapper.Map<List<Prop>>(props);
                dbProps.ForEach(prop => prop.Pickable = true);

                try
                {
                    await _db.Props.AddRangeAsync(dbProps);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Successfully created {Count} props", dbProps.Count);
                    return Ok(dbProps);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error creating props");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("Unable to create props. Please check the data and try again."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error creating props");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("An unexpected error occurred."));
                }
            }

            [HttpGet("admin/unpaid")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetUnpaidOwners()
            {
                var owners = await _db.Owners.Where(o => !o.istest && !o.ConfidencePaid).ToListAsync();
                var ret = _mapper.Map<List<OwnerDTO>>(owners);
                return Ok(ret);
            }

            [HttpPost("admin/mark-paid")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> MarkOwnerAsPaid([FromBody] List<int> ownerIds, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} marking {Count} owners as paid",
                    authResult.Owner.Ownerid, ownerIds.Count);

                var editOwners = await _db.Owners.Where(o => ownerIds.Contains(o.Ownerid)).ToListAsync();

                if (!editOwners.Any())
                {
                    return BadRequest(new ErrorResponse("No owners found with the provided IDs."));
                }

                editOwners.ForEach(o =>
                {
                    o.ConfidencePaid = true;
                });

                try
                {
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Successfully marked {Count} owners as paid", editOwners.Count);
                    return Ok(new { markedPaid = editOwners.Count });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error marking owners as paid");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("Unable to update payment status. Please try again."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error marking owners as paid");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("An unexpected error occurred."));
                }
            }

            [HttpPost("admin/props/{propId}/results/{winningOption}")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status404NotFound)]
            public async Task<IActionResult> PostPropAnswer([FromRoute] int propId, [FromRoute] string winningOption, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} setting winner for prop {PropId}",
                    authResult.Owner.Ownerid, propId);

                var dbPropToUpdate = await _db.Props.FirstOrDefaultAsync(m => m.Id == propId);

                if (dbPropToUpdate == null)
                {
                    _logger.LogWarning("Prop {PropId} not found", propId);
                    return NotFound(new ErrorResponse("Prop not found."));
                }

                if (winningOption != "A" && winningOption != "B")
                {
                    return BadRequest(new ErrorResponse("Winning option must be 'A' or 'B'."));
                }

                try
                {
                    dbPropToUpdate.Winner = winningOption;
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Successfully set winner {Option} for prop {PropId}", winningOption, propId);
                    return Ok(new { propId, winningOption });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error setting prop winner");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("Unable to update prop result. Please try again."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error setting prop winner");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("An unexpected error occurred."));
                }
            }

            [HttpGet("year/{year}/week/{week}/coummunity-stats")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> GetCommunityStats(int year, int week)
            {
                var allPicksThisWeek = _db.NflPicks.Where(p => p.NflTeamMatchup.Week == week && p.NflTeamMatchup.Year == year).ToList().GroupBy(p => p.MatchupId);
                var isStillPickable = false;

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
                        LPct = Math.Round(leftPicks / count, 2),
                        RPct = Math.Round(rightPicks / count, 2),
                        LAvg = Math.Round(leftPicks == 0 ? 0 : leftTotalPts / leftPicks, 1),
                        RAvg = Math.Round(rightPicks == 0 ? 0 : rightTotalPts / rightPicks, 1)
                    };
                });
                if (isStillPickable)
                {
                    await _gm.NotifyMflError(new BotMessage($"Someone is tryna cheat", string.Empty));
                    return BadRequest(new ErrorResponse("you are trying to access stats for matchups that are not yet locked."));
                }
                return Ok(matchupStats);
            }

            [HttpPost("admin/matchups/{matchupId}/results/{winningTeamId}")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status404NotFound)]
            public async Task<IActionResult> PostRealMatchupWinner([FromRoute] int matchupId, [FromRoute] int winningTeamId, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} setting winner for matchup {MatchupId}",
                    authResult.Owner.Ownerid, matchupId);

                var dbMatchupToUpdate = await _db.NflTeamMatchups.FirstOrDefaultAsync(m => m.Id == matchupId);

                if (dbMatchupToUpdate == null)
                {
                    _logger.LogWarning("Matchup {MatchupId} not found", matchupId);
                    return NotFound(new ErrorResponse("Matchup not found."));
                }

                // Validate that the winning team is one of the teams in the matchup
                if (winningTeamId != dbMatchupToUpdate.Left && winningTeamId != dbMatchupToUpdate.Right)
                {
                    _logger.LogWarning("Invalid winning team {TeamId} for matchup {MatchupId}", winningTeamId, matchupId);
                    return BadRequest(new ErrorResponse("Winning team must be one of the teams in the matchup."));
                }

                try
                {
                    dbMatchupToUpdate.Winner = winningTeamId;
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Successfully set winner {TeamId} for matchup {MatchupId}", winningTeamId, matchupId);
                    return Ok(new { matchupId, winningTeamId });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error setting matchup winner");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("Unable to update matchup result. Please try again."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error setting matchup winner");
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new ErrorResponse("An unexpected error occurred."));
                }
            }

            [HttpPost("lock-matchups")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> MakeAllMatchupsUnpickable([FromQuery] int year = Utils.ThisYear, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} locking matchups for year {Year}",
                    authResult.Owner.Ownerid, year);

                using (var transaction = await _db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var matchups = await _db.NflTeamMatchups.Where(m => m.Year == year).ToListAsync();
                        var props = await _db.Props.Where(p => p.Year == year).ToListAsync();

                        props.ForEach(p => p.Pickable = false);
                        matchups.ForEach(m => m.Pickable = false);

                        await _db.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Successfully locked {MatchupCount} matchups and {PropCount} props for year {Year}",
                            matchups.Count, props.Count, year);

                        return Ok(new { matchupsLocked = matchups.Count, propsLocked = props.Count });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error locking matchups for year {Year}", year);
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new ErrorResponse("Unable to lock matchups. Please try again."));
                    }
                }
            }

            [HttpPost("picks")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            public async Task<IActionResult> SaveOrOverwriteMyPicks([FromBody] NflPickSubmission pickSubmission, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // AUTHENTICATION: Verify user is authenticated
                if (string.IsNullOrEmpty(user))
                {
                    _logger.LogWarning("Pick submission attempted without user authentication");
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                // Verify the user exists in the database
                var authenticatedOwner = await _db.Owners.FirstOrDefaultAsync(o => o.authid == user);
                if (authenticatedOwner == null)
                {
                    _logger.LogWarning("Pick submission attempted with invalid user: {User}", user);
                    return Unauthorized(new ErrorResponse("Invalid user credentials."));
                }

                // Basic validation
                if (pickSubmission?.Picks == null || !pickSubmission.Picks.Any())
                {
                    return BadRequest(new ErrorResponse("Invalid pick submission - no picks provided."));
                }

                var picks = pickSubmission.Picks;
                var props = pickSubmission.Props ?? new List<PropPickDTO>();

                // Get the matchup IDs to determine expected counts
                var matchupIds = picks.Select(p => p.MatchupId).Distinct().ToList();
                var pickableMatchups = await _db.NflTeamMatchups
                    .Where(m => matchupIds.Contains(m.Id) && m.Pickable)
                    .ToListAsync();

                if (!pickableMatchups.Any())
                {
                    return BadRequest(new ErrorResponse("No pickable matchups found."));
                }

                // Get pickable props for this week/year
                var year = pickableMatchups.First().Year;
                var week = pickableMatchups.First().Week;
                var pickableProps = await _db.Props
                    .Where(p => p.Year == year && p.Week == week && p.Pickable)
                    .ToListAsync();

                // Validate the submission structure
                var validationResult = _validationService.ValidatePickSubmission(
                    pickSubmission,
                    pickableMatchups.Count,
                    pickableProps.Count
                );

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Pick validation failed for user {User}: {Error}", user, validationResult.ErrorMessage);
                    return BadRequest(new ErrorResponse(validationResult.ErrorMessage));
                }

                // AUTHORIZATION: Verify the authenticated user matches the submission owner
                if (validationResult.OwnerId != authenticatedOwner.Ownerid)
                {
                    _logger.LogWarning("User {User} attempted to submit picks for owner {OwnerId}", user, validationResult.OwnerId);
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("You cannot submit picks for another user."));
                }

                // Validate that all choices are valid (Left or Right team in matchup)
                foreach (var pick in picks)
                {
                    var matchup = pickableMatchups.FirstOrDefault(m => m.Id == pick.MatchupId);
                    if (matchup == null)
                    {
                        return BadRequest(new ErrorResponse($"Matchup {pick.MatchupId} is not pickable."));
                    }

                    if (pick.Choice != matchup.Left && pick.Choice != matchup.Right)
                    {
                        return BadRequest(new ErrorResponse($"Invalid team choice for matchup {pick.MatchupId}."));
                    }
                }

                // USE TRANSACTION to prevent race conditions
                using (var transaction = await _db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Re-check that matchups are still pickable within the transaction
                        var stillPickable = await _db.NflTeamMatchups
                            .Where(m => matchupIds.Contains(m.Id))
                            .Select(m => new { m.Id, m.Pickable })
                            .ToListAsync();

                        if (stillPickable.Any(m => !m.Pickable))
                        {
                            _logger.LogWarning("Matchups locked during pick submission for user {User}", user);
                            return BadRequest(new ErrorResponse("One or more matchups were locked while you were submitting. Please refresh and try again."));
                        }

                        // Get existing picks for this owner and these matchups
                        var existingPicks = await _db.NflPicks
                            .Where(p => matchupIds.Contains(p.MatchupId) && p.OwnerId == authenticatedOwner.Ownerid)
                            .ToListAsync();

                        var propIds = props.Select(p => p.PropId).ToList();
                        var existingProps = await _db.ExtraPicks
                            .Where(p => propIds.Contains(p.PropId) && p.OwnerId == authenticatedOwner.Ownerid)
                            .ToListAsync();

                        if (existingPicks.Any())
                        {
                            // UPDATE existing picks
                            foreach (var existingPick in existingPicks)
                            {
                                var submittedPick = picks.FirstOrDefault(p => p.MatchupId == existingPick.MatchupId);
                                if (submittedPick != null)
                                {
                                    existingPick.Choice = submittedPick.Choice;
                                    existingPick.Points = submittedPick.Points;
                                }
                            }

                            // ADD any new picks (if new matchups were added to the week)
                            var newPickMatchupIds = matchupIds.Except(existingPicks.Select(ep => ep.MatchupId)).ToList();
                            if (newPickMatchupIds.Any())
                            {
                                var newPickDtos = picks.Where(p => newPickMatchupIds.Contains(p.MatchupId)).ToList();
                                var newPicks = _mapper.Map<List<Pick>>(newPickDtos);
                                await _db.NflPicks.AddRangeAsync(newPicks);
                            }

                            // UPDATE existing props
                            foreach (var existingProp in existingProps)
                            {
                                var submittedProp = props.FirstOrDefault(p => p.PropId == existingProp.PropId);
                                if (submittedProp != null)
                                {
                                    existingProp.Choice = submittedProp.Choice;
                                }
                            }

                            // ADD any new props
                            var newPropIds = propIds.Except(existingProps.Select(ep => ep.PropId)).ToList();
                            if (newPropIds.Any())
                            {
                                var newPropDtos = props.Where(p => newPropIds.Contains(p.PropId)).ToList();
                                var newProps = _mapper.Map<List<ExtraPick>>(newPropDtos);
                                await _db.ExtraPicks.AddRangeAsync(newProps);
                            }
                        }
                        else
                        {
                            // INSERT new picks
                            var pickEntities = _mapper.Map<List<Pick>>(picks);
                            await _db.NflPicks.AddRangeAsync(pickEntities);

                            if (props.Any())
                            {
                                var propEntities = _mapper.Map<List<ExtraPick>>(props);
                                await _db.ExtraPicks.AddRangeAsync(propEntities);
                            }
                            try
                            {
                                await _gm.NotifyMflError(new BotMessage($"User {authenticatedOwner.Displayname} ({authenticatedOwner.Ownerid}) submitted their confidence picks.", String.Empty));
                            } catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending notification for pick submission by user {User}", user);
                            }
                        }

                        // Save all changes
                        await _db.SaveChangesAsync();

                        // Commit transaction
                        await transaction.CommitAsync();

                        _logger.LogInformation("Successfully saved picks for user {User}, owner {OwnerId}", user, authenticatedOwner.Ownerid);
                        return Ok(new { message = "Picks saved successfully" });
                    }
                    catch (DbUpdateException ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Database error saving picks for user {User}", user);
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new ErrorResponse("Unable to save picks. Please try again."));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Unexpected error saving picks for user {User}", user);
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new ErrorResponse("An unexpected error occurred. Please try again."));
                    }
                }
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
                        PickSubmitted = _.Any(p => p.NflTeamMatchup.Pickable),
                        Avatar = _.FirstOrDefault().Owner.Avatar,
                        DisplayName = _.FirstOrDefault().Owner.Displayname ?? "",
                        IsPaid = _.FirstOrDefault().Owner.ConfidencePaid,
                        OwnerId = _.Key,
                        ConfidenceTitles = _.FirstOrDefault().Owner.ConfidenceTitleList,
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
                                Choice = wRes.NflTeamMatchup.Pickable ? null : wRes.Choice,
                                Points = wRes.Points,
                                Correct = wRes.NflTeamMatchup.Winner == null ? null : wRes.NflTeamMatchup.Winner == wRes.Choice,
                                PickTeam = (wRes.NflTeamMatchup.Pickable && year != -1) ? null : _mapper.Map<NflTeamBaseDTO>(wRes.ChosenTeam)
                            }).OrderByDescending(r => r.Points)

                        }).OrderBy(wr => wr.Week)
                    }

                    ).OrderByDescending(r => r.TotalPoints + (r.ExtraPoints * 0.1)).ToList();

                var scores = results.Select(r => r.TotalPoints + (r.ExtraPoints * 0.1)).ToList();
                results.ForEach(res =>
                {
                    var x = from s in scores where s > (res.TotalPoints + (res.ExtraPoints * 0.1)) select s;
                    res.Rank = x.Count() + 1;
                });
                return Ok(results);
            }

            /*[HttpGet("demo/generate")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            public async Task<IActionResult> CreateDemoData([FromQuery] int year = -1)
            {
                // Check if there are already too many test owners
                if (_db.Owners.Where(o => o.istest).ToList().Count > 50)
                    return BadRequest(new ErrorResponse("Too many test owners already exist."));

                // Get existing week 1 matchups for the specified year
                var week1Matchups = await _db.NflTeamMatchups
                    .Where(m => m.Year == year && m.Week == 1)
                    .ToListAsync();

                if (!week1Matchups.Any())
                {
                    return BadRequest(new ErrorResponse($"No matchups found for year {year}, week 1. Please create matchups first."));
                }

                // Generate fake demo users
                var users = new Faker<OwnerEntity>()
                    .RuleFor(o => o.Ownername, f => f.Internet.UserName())
                    .RuleFor(o => o.PasswordHash, f => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(f.Internet.Password())))
                    .RuleFor(o => o.Displayname, f => f.Name.FullName())
                    .RuleFor(o => o.Avatar, f => f.Internet.Avatar())
                    .RuleFor(o => o.istest, f => true)
                    .RuleFor(o => o.authid, f => f.Internet.Password())
                    .Generate(20);

                _db.Owners.AddRange(users);
                await _db.SaveChangesAsync();

                // Generate picks for all demo users for week 1
                var allThePicks = new List<Pick>();
                var points = Enumerable.Range(1, week1Matchups.Count).ToArray();
                var rng = new Random();

                users.ForEach(u =>
                {
                    rng.Shuffle(points);
                    var picks = week1Matchups.Select((m, i) => new Pick
                    {
                        Choice = rng.Next(2) == 0 ? m.Left : m.Right,
                        MatchupId = m.Id,
                        OwnerId = u.Ownerid,
                        Points = points[i]
                    }).ToList();
                    allThePicks.AddRange(picks);
                });

                _db.NflPicks.AddRange(allThePicks);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Generated {UserCount} demo users with {PickCount} picks for year {Year}, week 1",
                    users.Count, allThePicks.Count, year);

                return Ok(new
                {
                    message = "Demo data created successfully",
                    usersCreated = users.Count,
                    picksCreated = allThePicks.Count,
                    matchupsUsed = week1Matchups.Count
                });
            }*/
            [HttpPost("admin/matchups/{matchupId}/set-current")]
            [Produces("application/json")]
            [ProducesResponseType(StatusCodes.Status200OK)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status401Unauthorized)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status404NotFound)]
            public async Task<IActionResult> SetCurrentGame([FromRoute] int matchupId, [FromQuery] string user = "")
            {
                // Decode user parameter
                user = DecodeUserParam(user);

                // ADMIN AUTHENTICATION & AUTHORIZATION
                var authResult = await _adminAuthService.AuthorizeAdminAsync(user);

                if (!authResult.IsAuthenticated)
                {
                    return Unauthorized(new ErrorResponse("Authentication required."));
                }

                if (!authResult.IsAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        new ErrorResponse("Admin privileges required for this action."));
                }

                _logger.LogInformation("Admin user {OwnerId} setting matchup {MatchupId} as current game",
                    authResult.Owner.Ownerid, matchupId);

                using (var transaction = await _db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Find the matchup to set as current
                        var matchupToSetCurrent = await _db.NflTeamMatchups.FirstOrDefaultAsync(m => m.Id == matchupId);

                        if (matchupToSetCurrent == null)
                        {
                            _logger.LogWarning("Matchup {MatchupId} not found", matchupId);
                            return NotFound(new ErrorResponse("Matchup not found."));
                        }

                        // Set all other matchups to false
                        var allMatchups = await _db.NflTeamMatchups.ToListAsync();
                        allMatchups.ForEach(m => m.IsCurrentGame = false);

                        // Set the selected matchup as the current game
                        matchupToSetCurrent.IsCurrentGame = true;

                        await _db.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Successfully set matchup {MatchupId} as current game", matchupId);

                        return Ok(new
                        {
                            matchupId,
                            currentGame = true,
                            message = "Matchup set as current game successfully"
                        });
                    }
                    catch (DbUpdateException ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Database error setting current game");
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new ErrorResponse("Unable to set current game. Please try again."));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Unexpected error setting current game");
                        return StatusCode(StatusCodes.Status500InternalServerError,
                            new ErrorResponse("An unexpected error occurred."));
                    }
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
}
