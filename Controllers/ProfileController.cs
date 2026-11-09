using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Kalories.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalories.Server.Controllers;

public class ProfileDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public Sex Sex { get; set; }
    [Range(50, 260)]
    public decimal? HeightCm { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public Goal Goal { get; set; }
    public string? AvatarPath { get; set; }
}

[ApiController]
[Route("api/profile")]
[Authorize]
[RequireConsent]
public class ProfileController(ApplicationDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get()
    {
        var p = await db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == UserId);
        return p is null ? NotFound() : ToDto(p);
    }

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Put(ProfileDto dto)
    {
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.UserId == UserId);
        if (p is null)
        {
            p = new UserProfile { UserId = UserId };
            db.Profiles.Add(p);
        }

        p.Name = dto.Name.Trim();
        p.BirthDate = dto.BirthDate;
        p.Sex = dto.Sex;
        p.HeightCm = dto.HeightCm;
        p.ActivityLevel = dto.ActivityLevel;
        p.Goal = dto.Goal;
        // AvatarPath no se acepta aquí: lo gestionará el endpoint de subida de foto.

        await db.SaveChangesAsync();
        return ToDto(p);
    }

    private static ProfileDto ToDto(UserProfile p) => new()
    {
        Name = p.Name,
        BirthDate = p.BirthDate,
        Sex = p.Sex,
        HeightCm = p.HeightCm,
        ActivityLevel = p.ActivityLevel,
        Goal = p.Goal,
        AvatarPath = string.IsNullOrEmpty(p.AvatarPath) ? null : "/api/profile/avatar"
    };
}