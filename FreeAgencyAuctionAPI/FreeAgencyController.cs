using System.Collections.Generic;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace FreeAgencyAuctionAPI
{
    [ApiController]
    [Route("[controller]")]
    public class FreeAgencyController : ControllerBase
    {
        private readonly IPlayerServiceLayer _pService;

        public FreeAgencyController(IPlayerServiceLayer pService)
        {
            _pService = pService;
        }

        /// <summary>
        /// get player by id
        /// </summary>
        /// <returns></returns>
        [HttpGet("players/{playerId}")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(PlayerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlayerById(int playerId)
        {
            var ret = await _pService.GetPlayerById(playerId);
            if (ret != null) return Ok(ret);
            return BadRequest();
        }

        /// <summary>
        /// get all players who have owners - for rosters pages
        /// </summary>
        /// <returns></returns>
        [HttpGet("players/rostered")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetRosteredPlayers()
        {
            var ret = await _pService.GetRosteredPlayers();
            if (ret != null) return Ok(ret);
            return BadRequest();
        }

        /// <summary>
        /// give placeholder owner to player after nomination
        /// </summary>
        /// <returns></returns>
        [HttpGet("players/rostered")]
        [Produces("application/json", Type = typeof(PlayerDTO))]
        [ProducesResponseType(typeof(List<PlayerDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> NominatePlayer([FromBody] PlayerDTO player)

    {
            var ret = await _pService.NominatePlayer(player);
            if (ret != null) return Ok(ret);
            return BadRequest();
        }
    }
}