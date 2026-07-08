using System.Collections.Generic;
using System.Threading.Tasks;
using FreeAgencyAuctionAPI.Filters;
using FreeAgencyAuctionAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Filters
{
    public class DemoWriteGuardFilterTests
    {
        private const int DemoLeague = -32;
        private const int RealLeague = 13894;

        private static ActionExecutingContext BuildContext(
            string method,
            string path,
            Dictionary<string, object> routeValues = null,
            Dictionary<string, object> args = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = method;
            httpContext.Request.Path = path;

            var routeData = new RouteData();
            foreach (var kv in routeValues ?? new Dictionary<string, object>())
                routeData.Values[kv.Key] = kv.Value;

            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                args ?? new Dictionary<string, object>(),
                controller: new object());
        }

        private static async Task<(bool nextCalled, ActionExecutingContext ctx)> Run(ActionExecutingContext ctx)
        {
            var filter = new DemoWriteGuardFilter();
            var nextCalled = false;
            ActionExecutionDelegate next = () =>
            {
                nextCalled = true;
                return Task.FromResult<ActionExecutedContext>(null);
            };
            await filter.OnActionExecutionAsync(ctx, next);
            return (nextCalled, ctx);
        }

        private static void AssertForbidden(ActionExecutingContext ctx)
        {
            var result = Assert.IsType<ObjectResult>(ctx.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        }

        [Fact]
        public async Task Post_WithDemoLeagueIdInBody_IsBlocked()
        {
            var ctx = BuildContext("POST", "/free-agency/bid",
                args: new Dictionary<string, object> { { "bid", new BidDTO { LeagueId = DemoLeague } } });

            var (nextCalled, _) = await Run(ctx);

            Assert.False(nextCalled);
            AssertForbidden(ctx);
        }

        [Fact]
        public async Task Post_WithDemoLeagueIdInRoute_IsBlocked()
        {
            var ctx = BuildContext("PUT", "/free-agency/win",
                routeValues: new Dictionary<string, object> { { "leagueId", DemoLeague } });

            var (nextCalled, _) = await Run(ctx);

            Assert.False(nextCalled);
            AssertForbidden(ctx);
        }

        [Fact]
        public async Task GetTradeMutation_WithDemoLeagueId_IsBlocked()
        {
            // Trade mutations are GETs; league id arrives as the {id} route param.
            var ctx = BuildContext("GET", "/dashboard/league/-32/trades/5/9/mfl/1/accept-trade",
                routeValues: new Dictionary<string, object> { { "id", DemoLeague } });

            var (nextCalled, _) = await Run(ctx);

            Assert.False(nextCalled);
            AssertForbidden(ctx);
        }

        [Fact]
        public async Task Post_WithRealLeagueId_PassesThrough()
        {
            var ctx = BuildContext("POST", "/free-agency/bid",
                args: new Dictionary<string, object> { { "bid", new BidDTO { LeagueId = RealLeague } } });

            var (nextCalled, _) = await Run(ctx);

            Assert.True(nextCalled);
            Assert.Null(ctx.Result);
        }

        [Fact]
        public async Task Get_ReadOfDemoLeague_IsNotBlocked()
        {
            // Plain reads of the demo league must be allowed (that's the whole demo).
            var ctx = BuildContext("GET", "/free-agency/leagues/-32/lots",
                routeValues: new Dictionary<string, object> { { "leagueId", DemoLeague } });

            var (nextCalled, _) = await Run(ctx);

            Assert.True(nextCalled);
            Assert.Null(ctx.Result);
        }
    }
}
