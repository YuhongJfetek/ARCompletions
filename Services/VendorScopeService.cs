using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    public class VendorScopeService
    {
        private readonly ARCompletionsContext _db;

        public VendorScopeService(ARCompletionsContext db)
        {
            _db = db;
        }

        // Returns null when the user has access to all vendors
        public async Task<HashSet<string>?> GetAllowedVendorIdsAsync(ClaimsPrincipal user)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            var ids = await _db.PlatformUserVendorScopes
                .Where(s => s.PlatformUserId == userId)
                .Select(s => s.VendorId)
                .ToListAsync();

            if (ids == null || ids.Count == 0) return null; // null = unrestricted
            return ids.ToHashSet();
        }
    }
}
