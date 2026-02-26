// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using ARCompletions.Dto;
namespace ARCompletions.Controllers
{
    [ApiController]
    [Route("api/venues")]
    public class VenuesController : ControllerBase
    {
        private readonly Data.ARCompletionsContext _context;

        public VenuesController(Data.ARCompletionsContext context)
        {
            _context = context;
        }

        // POST /venues/complete
        [HttpPost("complete")]
        public IActionResult Complete([FromBody] CompleteRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.venuesid) || string.IsNullOrEmpty(req.userid))
                return BadRequest("Invalid request");

            var completion = new Data.Completion
            {
                VenuesId = req.venuesid,
                UserId = req.userid,
                Complate = req.complate == 1,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            try
            {
                _context.Completions.Add(completion);
                _context.SaveChanges();

                return Ok(new {
                    venuesid = completion.VenuesId,
                    userid = completion.UserId,
                    complate = completion.Complate ? 1 : 0,
                    createdAt = completion.CreatedAt
                });
            }
            catch
            {
                return StatusCode(500, "Database error");
            }
        }

        // GET /venues/{userId}
        [HttpGet("{userId}")]
        public IActionResult GetUserVenues(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("Invalid userId");

            // 固定 requiredVenues 清單（改為 v001..v015）
            var requiredVenues = new[] { "v001", "v002", "v003", "v004", "v005", "v006", "v007", "v008", "v009", "v010", "v011", "v012", "v013", "v014", "v015", "v016" };
            var completedVenues = _context.Completions
                .Where(c => c.UserId == userId && c.Complate)
                .Select(c => c.VenuesId)
                .Distinct()
                .ToList();
            var completedVenuesResult = completedVenues.ToList();

            // 只回傳已完成的場地 coupon
            var couponList = requiredVenues
                .Select((venue, idx) => new {
                    venue = venue,
                    vendorid = $"vendor{(idx + 1).ToString("D2")}",
                    imgurl = idx < 5
                        ? "/Image/01-05.jpg"
                        : (idx == 6 ? "/Image/07.png" : $"/Image/{(idx + 1).ToString("D2")}.jpg")
                })
                .Where(x => completedVenuesResult.Contains(x.venue))
                .Select(x => new { x.vendorid, x.imgurl })
                .ToList();
            return Ok(new
            {
                completedVenues = completedVenuesResult,
                coupon = couponList,
                requiredVenues,
                doneCount = completedVenuesResult.Count,
                totalRequired = requiredVenues.Length
            });
        }
    }
}
