using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreeAgencyAuctionAPI.Filters
{
    /// <summary>
    /// Hard safety boundary for the public read-only demo: no mutation may ever
    /// target a demo league. Registered globally.
    ///
    /// A request is treated as a mutation when the HTTP method is POST/PUT/DELETE/PATCH,
    /// or when the path is one of the GET-based trade mutations (accept/reject/revoke-trade).
    /// It is blocked (403) when any resolved league id is a demo league (negative).
    ///
    /// This works because a demo league is the only negative id in the system, so a
    /// negative id appearing anywhere in the route or a bound body/query unambiguously
    /// means the request targets the demo league. Reads (plain GETs) are never blocked.
    /// The frontend simulates demo writes locally and never sends them, so this filter
    /// is defense-in-depth — it exists so a regression cannot silently mutate production.
    /// </summary>
    public class DemoWriteGuardFilter : IAsyncActionFilter
    {
        private static readonly string[] TradeMutationMarkers =
            { "accept-trade", "reject-trade", "revoke-trade" };

        private static readonly string[] MutatingMethods =
            { "POST", "PUT", "DELETE", "PATCH" };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (IsMutation(context) && TargetsDemoLeague(context))
            {
                context.Result = new ObjectResult(new ErrorResponse("This action is disabled in the read-only demo."))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }

        private static bool IsMutation(ActionExecutingContext context)
        {
            var method = context.HttpContext.Request.Method;
            if (MutatingMethods.Contains(method, StringComparer.OrdinalIgnoreCase)) return true;

            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            return TradeMutationMarkers.Any(m => path.Contains(m, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TargetsDemoLeague(ActionExecutingContext context)
        {
            return CandidateIds(context).Any(Utils.IsDemoLeague);
        }

        /// <summary>
        /// Every integer we can find in the route values and bound action arguments.
        /// We only care whether any is negative (a demo league id), so casting a wide
        /// net is safe: no legitimate id in the system is negative.
        /// </summary>
        private static IEnumerable<int> CandidateIds(ActionExecutingContext context)
        {
            foreach (var rv in context.RouteData.Values.Values)
            {
                if (rv != null && int.TryParse(rv.ToString(), out var routeInt))
                    yield return routeInt;
            }

            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg == null) continue;

                if (arg is int i)
                {
                    yield return i;
                    continue;
                }

                // Bound body/query DTOs: read any int property named "leagueId".
                var leagueProp = arg.GetType()
                    .GetProperties()
                    .FirstOrDefault(p =>
                        string.Equals(p.Name, "leagueId", StringComparison.OrdinalIgnoreCase) &&
                        p.PropertyType == typeof(int));

                if (leagueProp?.GetValue(arg) is int leagueId)
                    yield return leagueId;
            }
        }
    }
}
