using System.Security.Claims;
using Kalories.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalories.Server.Controllers;

[ApiController]
[Route("api/profile/avatar")]
[Authorize]
public class AvatarController(ApplicationDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Carpeta fuera de wwwroot: los archivos no se sirven como estáticos,
    // solo se entregan a través de este controlador, a su dueño.
    private string Folder => Path.Combine(env.ContentRootPath, "uploads", "avatars");

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 8192)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile is null)
            return BadRequest("Completa primero el perfil.");

        if (file is null || file.Length == 0)
            return BadRequest("No se recibió ninguna imagen.");
        if (file.Length > MaxBytes)
            return BadRequest("La imagen no puede superar 10 MB.");

        // Se comprueba el contenido real del archivo, no su nombre ni su Content-Type.
        var header = new byte[8];
        int read;
        await using (var input = file.OpenReadStream())
        {
            read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        }

        string? ext = null;
        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            ext = ".jpg";
        else if (read >= 8 && header.AsSpan().SequenceEqual(PngSignature))
            ext = ".png";

        if (ext is null)
            return BadRequest("Solo se admiten imágenes JPG o PNG.");

        Directory.CreateDirectory(Folder);
        var newName = $"{Guid.NewGuid():N}{ext}";
        var newPath = Path.Combine(Folder, newName);

        await using (var fs = System.IO.File.Create(newPath))
        {
            await file.CopyToAsync(fs);
        }

        DeleteFile(profile.AvatarPath); // borra la foto anterior, si había
        profile.AvatarPath = newName;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var name = await db.Profiles
            .Where(p => p.UserId == UserId)
            .Select(p => p.AvatarPath)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(name))
            return NotFound();

        var path = Path.Combine(Folder, Path.GetFileName(name));
        if (!System.IO.File.Exists(path))
            return NotFound();

        var contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
        return PhysicalFile(path, contentType);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile?.AvatarPath is null)
            return NoContent();

        DeleteFile(profile.AvatarPath);
        profile.AvatarPath = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private void DeleteFile(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var path = Path.Combine(Folder, Path.GetFileName(name)); // GetFileName evita rutas maliciosas
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }
}