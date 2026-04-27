using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace FreeAgencyAuctionAPI.Filters
{
    // Protects admin endpoints callable by cron (GitHub Actions) via a shared secret in X-Api-Key.
    public class AdminApiKeyAttribute : TypeFilterAttribute
    {
        public AdminApiKeyAttribute() : base(typeof(AdminApiKeyFilter)) { }
    }

    public class AdminApiKeyFilter : IAuthorizationFilter
    {
        private readonly IConfiguration _config;
        public AdminApiKeyFilter(IConfiguration config) => _config = config;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var expected = _config["AdminApiKey"];
            if (string.IsNullOrEmpty(expected))
            {
                context.Result = new StatusCodeResult(500);
                return;
            }
            if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != expected)
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }
}
