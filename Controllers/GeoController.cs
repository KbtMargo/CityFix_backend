using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Controllers
{
    // JSON endpoints for the map picker. Signed-in users only: the server, not the browser, talks to
    // Nominatim, so its usage policy (rate limit, User-Agent, caching) is kept in one place.
    [Authorize]
    public class GeoController : Controller
    {
        private readonly IGeocodingService _geocoding;

        public GeoController(IGeocodingService geocoding)
        {
            _geocoding = geocoding;
        }

        // GET: Geo/Reverse?lat=&lng=  → { address } or 204 when nothing was found / the service is down
        [HttpGet]
        public async Task<IActionResult> Reverse(double lat, double lng, CancellationToken cancellationToken)
        {
            if (lat is < -90 or > 90 || lng is < -180 or > 180) return BadRequest();
            var address = await _geocoding.ReverseAsync(lat, lng, cancellationToken);
            return address == null ? NoContent() : Json(new { address });
        }

        // GET: Geo/Search?q=  → { lat, lng, address } or 204
        [HttpGet]
        public async Task<IActionResult> Search(string? q, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest();
            var place = await _geocoding.SearchAsync(q, cancellationToken);
            return place == null ? NoContent() : Json(new { lat = place.Lat, lng = place.Lng, address = place.Address });
        }
    }
}
