using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Kalories.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalories.Server.Controllers;

public class MeasurementDto
{
    public DateTime? MeasuredAt { get; set; }
    [Range(20, 500)]
    public decimal WeightKg { get; set; }
    [Range(20, 300)]
    public decimal? WaistCm { get; set; }
    [Range(20, 300)]
    public decimal? HipCm { get; set; }
    [Range(20, 300)]
    public decimal? ChestCm { get; set; }
}

[ApiController]
[Route("api/measurements")]
[Authorize]
[RequireConsent]
public class MeasurementsController(ApplicationDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<List<MeasurementDto>>> List()
    {
        return await db.Measurements.AsNoTracking()
            .Where(m => m.UserId == UserId)
            .OrderByDescending(m => m.MeasuredAt)
            .Select(m => new MeasurementDto
            {
                MeasuredAt = m.MeasuredAt,
                WeightKg = m.WeightKg,
                WaistCm = m.WaistCm,
                HipCm = m.HipCm,
                ChestCm = m.ChestCm
            })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Add(MeasurementDto dto)
    {
        db.Measurements.Add(new BodyMeasurement
        {
            UserId = UserId,
            MeasuredAt = dto.MeasuredAt?.ToUniversalTime() ?? DateTime.UtcNow,
            WeightKg = dto.WeightKg,
            WaistCm = dto.WaistCm,
            HipCm = dto.HipCm,
            ChestCm = dto.ChestCm
        });
        await db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created);
    }
}