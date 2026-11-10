using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Kalories.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalories.Server.Controllers;

public class DeleteAccountRequest
{
    [Required]
    public string Password { get; set; } = "";
}

public class ConsentRequest
{
    [Required]
    public string Version { get; set; } = "";
}

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    IWebHostEnvironment env) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("consent")]
    public async Task<IActionResult> GetConsent()
    {
        var user = await users.FindByIdAsync(UserId);
        if (user is null)
            return Unauthorized();

        return Ok(new
        {
            accepted = user.ConsentVersion == ConsentInfo.CurrentVersion,
            version = user.ConsentVersion,
            acceptedAt = user.ConsentAcceptedAt,
            currentVersion = ConsentInfo.CurrentVersion
        });
    }

    [HttpPost("consent")]
    public async Task<IActionResult> AcceptConsent(ConsentRequest request)
    {
        if (request.Version != ConsentInfo.CurrentVersion)
            return BadRequest("Versión del consentimiento no válida.");

        var user = await users.FindByIdAsync(UserId);
        if (user is null)
            return Unauthorized();

        user.ConsentAcceptedAt = DateTime.UtcNow;
        user.ConsentVersion = request.Version;

        var result = await users.UpdateAsync(user);
        return result.Succeeded
            ? NoContent()
            : StatusCode(StatusCodes.Status500InternalServerError, "No se pudo guardar el consentimiento.");
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountRequest request)
    {
        var user = await users.FindByIdAsync(UserId);
        if (user is null)
            return Unauthorized();

        // Pide la contraseña para evitar borrados accidentales o con una sesión ajena.
        // lockoutOnFailure cuenta los fallos y bloquea la cuenta si se insiste.
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return BadRequest("Contraseña incorrecta.");

        // Se apuntan las fotos antes de borrar, porque después ya no se podrán consultar.
        var avatar = await db.Profiles
            .Where(p => p.UserId == UserId)
            .Select(p => p.AvatarPath)
            .FirstOrDefaultAsync();

        var mealPhotos = await db.Meals
            .Where(m => m.UserId == UserId)
            .Select(m => m.PhotoPath)
            .ToListAsync();

        // El borrado en cascada elimina también el perfil, las medidas y las comidas.
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
            return StatusCode(StatusCodes.Status500InternalServerError, "No se pudo borrar la cuenta.");

        DeleteFile("avatars", avatar);
        foreach (var photo in mealPhotos)
            DeleteFile("meals", photo);

        return NoContent();
    }

    private void DeleteFile(string folder, string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            var path = Path.Combine(env.ContentRootPath, "uploads", folder, Path.GetFileName(name));
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch (Exception)
        {
            // La cuenta ya está borrada; si un archivo no se puede eliminar, quedará huérfano.
        }
    }
}