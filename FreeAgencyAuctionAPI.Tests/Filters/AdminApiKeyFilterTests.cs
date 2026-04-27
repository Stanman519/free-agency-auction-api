using FreeAgencyAuctionAPI.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

namespace FreeAgencyAuctionAPI.Tests.Filters
{
    public class AdminApiKeyFilterTests
    {
        private static AuthorizationFilterContext BuildContext(string headerValue)
        {
            var httpContext = new DefaultHttpContext();
            if (headerValue != null) httpContext.Request.Headers["X-Api-Key"] = headerValue;
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        private static IConfiguration BuildConfig(string adminKey)
        {
            var cfg = new ConfigurationManager();
            if (adminKey != null) cfg["AdminApiKey"] = adminKey;
            return cfg;
        }

        [Fact]
        public void ValidKey_Authorizes()
        {
            var filter = new AdminApiKeyFilter(BuildConfig("secret"));
            var ctx = BuildContext("secret");
            filter.OnAuthorization(ctx);
            Assert.Null(ctx.Result);
        }

        [Fact]
        public void WrongKey_ReturnsUnauthorized()
        {
            var filter = new AdminApiKeyFilter(BuildConfig("secret"));
            var ctx = BuildContext("nope");
            filter.OnAuthorization(ctx);
            Assert.IsType<UnauthorizedResult>(ctx.Result);
        }

        [Fact]
        public void MissingHeader_ReturnsUnauthorized()
        {
            var filter = new AdminApiKeyFilter(BuildConfig("secret"));
            var ctx = BuildContext(null);
            filter.OnAuthorization(ctx);
            Assert.IsType<UnauthorizedResult>(ctx.Result);
        }

        [Fact]
        public void UnconfiguredServer_Returns500()
        {
            var filter = new AdminApiKeyFilter(BuildConfig(null));
            var ctx = BuildContext("anything");
            filter.OnAuthorization(ctx);
            var result = Assert.IsType<StatusCodeResult>(ctx.Result);
            Assert.Equal(500, result.StatusCode);
        }
    }
}
