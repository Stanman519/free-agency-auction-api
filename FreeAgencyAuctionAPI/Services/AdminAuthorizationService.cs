using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IAdminAuthorizationService
    {
        Task<AdminAuthResult> AuthorizeAdminAsync(string authId);
    }

    public class AdminAuthorizationService : IAdminAuthorizationService
    {
        private readonly AuctionContext _db;
        private readonly ILogger<AdminAuthorizationService> _logger;

        public AdminAuthorizationService(AuctionContext db, ILogger<AdminAuthorizationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<AdminAuthResult> AuthorizeAdminAsync(string authId)
        {
            if (string.IsNullOrEmpty(authId))
            {
                return AdminAuthResult.Unauthenticated();
            }

            var owner = await _db.Owners.FirstOrDefaultAsync(o => o.authid == authId);

            if (owner == null)
            {
                _logger.LogWarning("Admin action attempted with invalid authId: {AuthId}", authId);
                return AdminAuthResult.Unauthenticated();
            }

            if (owner.Premium != true)
            {
                _logger.LogWarning("Non-admin user {OwnerId} ({AuthId}) attempted admin action", owner.Ownerid, authId);
                return AdminAuthResult.Unauthorized(owner);
            }

            _logger.LogInformation("Admin user {OwnerId} ({AuthId}) authenticated for admin action", owner.Ownerid, authId);
            return AdminAuthResult.Authorized(owner);
        }
    }

    public class AdminAuthResult
    {
        public bool IsAuthenticated { get; private set; }
        public bool IsAuthorized { get; private set; }
        public OwnerEntity Owner { get; private set; }
        public string FailureReason { get; private set; }

        private AdminAuthResult() { }

        public static AdminAuthResult Authorized(OwnerEntity owner)
        {
            return new AdminAuthResult
            {
                IsAuthenticated = true,
                IsAuthorized = true,
                Owner = owner
            };
        }

        public static AdminAuthResult Unauthorized(OwnerEntity owner)
        {
            return new AdminAuthResult
            {
                IsAuthenticated = true,
                IsAuthorized = false,
                Owner = owner,
                FailureReason = "User does not have admin privileges."
            };
        }

        public static AdminAuthResult Unauthenticated()
        {
            return new AdminAuthResult
            {
                IsAuthenticated = false,
                IsAuthorized = false,
                FailureReason = "Invalid or missing authentication credentials."
            };
        }
    }
}
