using System.Security.Claims;
using Kalories.Server.Data;
using Kalories.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace Kalories.Server.Controllers;

public record MealItemDto(int Id, string Name, decimal Grams, decimal Kcal, decimal ProteinG, decimal CarbsG, decimal FatG);

public record MealDto(
    int Id, DateTime CreatedAt, string Title, string Confidence, string Notes, bool HasPhoto,
    decimal TotalKcal, decimal TotalProteinG, decimal TotalCarbsG, decimal TotalFatG,
    List<MealItemDto> Items);

[ApiController]
[Route("api/meals")]
[Authorize]
[RequireConsent]
public class MealsController(
    ApplicationDbContext db,
    IMealAnalyzer analyzer,
    IOptions<AiOptions> ai,
    IWebHostEnvironment env) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB de subida
    private const int AnalysisMaxSide = 1024;       // lado máximo de la foto, en píxeles

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Carpeta fuera de wwwroot: las fotos solo se entregan a través de este controlador, a su dueño.
    private string Folder => Path.Combine(env.ContentRootPath, "uploads", "meals");

    [HttpPost("analyze")]
    [RequestSizeLimit(MaxBytes + 8192)]
    public async Task<ActionResult<MealDto>> Analyze(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No se recibió ninguna imagen.");
        if (file.Length > MaxBytes)
            return BadRequest("La imagen no puede superar 10 MB.");
        if (!await ImageTools.IsJpegOrPngAsync(file))
            return BadRequest("Solo se admiten imágenes JPG o PNG.");

        // Límite diario por usuario: cada análisis cuesta dinero.
        var dayStart = DateTime.UtcNow.Date;
        var today = await db.Meals.CountAsync(m => m.UserId == UserId && m.CreatedAt >= dayStart, ct);
        if (today >= ai.Value.DailyLimit)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                $"Has alcanzado el límite de {ai.Value.DailyLimit} análisis al día. Vuelve a intentarlo mañana.");

        byte[]? jpeg;
        try
        {
            jpeg = await ImageTools.ToCleanJpegAsync(file, AnalysisMaxSide);
        }
        catch (ImageFormatException)
        {
            return BadRequest("La imagen no es válida.");
        }
        if (jpeg is null)
            return BadRequest("La imagen es demasiado grande.");

        MealAnalysis analysis;
        try
        {
            analysis = await analyzer.AnalyzeAsync(jpeg, ct);
        }
        catch (MealAnalysisException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }

        if (!analysis.IsFood || analysis.Items.Count == 0)
            return UnprocessableEntity("No parece que en la foto haya comida. Prueba con otra foto del plato.");

        Directory.CreateDirectory(Folder);
        var photoName = $"{Guid.NewGuid():N}.jpg";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(Folder, photoName), jpeg, ct);

        var meal = new Meal
        {
            UserId = UserId,
            Title = analysis.DishName,
            Confidence = analysis.Confidence,
            Notes = analysis.Notes,
            PhotoPath = photoName,
            Items = analysis.Items.Select(i => new MealItem
            {
                Name = i.Name,
                Grams = i.Grams,
                Kcal = i.Kcal,
                ProteinG = i.ProteinG,
                CarbsG = i.CarbsG,
                FatG = i.FatG
            }).ToList()
        };

        db.Meals.Add(meal);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            DeleteFile(photoName);
            throw;
        }

        return ToDto(meal);
    }

    [HttpGet]
    public async Task<ActionResult<List<MealDto>>> List([FromQuery] int take = 30)
    {
        take = Math.Clamp(take, 1, 100);

        var meals = await db.Meals.AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.UserId == UserId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();

        return meals.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MealDto>> Get(int id)
    {
        var meal = await db.Meals.AsNoTracking()
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == UserId);

        return meal is null ? NotFound() : ToDto(meal);
    }

    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id)
    {
        var name = await db.Meals
            .Where(m => m.Id == id && m.UserId == UserId)
            .Select(m => m.PhotoPath)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(name))
            return NotFound();

        var path = Path.Combine(Folder, Path.GetFileName(name));
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/jpeg") : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == id && m.UserId == UserId);
        if (meal is null)
            return NotFound();

        var photo = meal.PhotoPath;
        db.Meals.Remove(meal); // los alimentos se borran en cascada
        await db.SaveChangesAsync();

        DeleteFile(photo);
        return NoContent();
    }

    private void DeleteFile(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var path = Path.Combine(Folder, Path.GetFileName(name)); // GetFileName evita rutas maliciosas
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private static MealDto ToDto(Meal m) => new(
        m.Id,
        DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc),
        m.Title,
        m.Confidence,
        m.Notes,
        m.PhotoPath is not null,
        Math.Round(m.Items.Sum(i => i.Kcal), 1),
        Math.Round(m.Items.Sum(i => i.ProteinG), 1),
        Math.Round(m.Items.Sum(i => i.CarbsG), 1),
        Math.Round(m.Items.Sum(i => i.FatG), 1),
        m.Items.OrderBy(i => i.Id)
            .Select(i => new MealItemDto(i.Id, i.Name, i.Grams, i.Kcal, i.ProteinG, i.CarbsG, i.FatG))
            .ToList());
}