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

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        // Pide la contraseña para evitar borrados accidentales o con una sesión ajena.
        // lockoutOnFailure cuenta los fallos y bloquea la cuenta si se insiste.
        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return BadRequest("Contraseña incorrecta.");

        var avatar = await db.Profiles
            .Where(p => p.UserId == userId)
            .Select(p => p.AvatarPath)
            .FirstOrDefaultAsync();

        // El borrado en cascada elimina también el perfil y las medidas.
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
            return StatusCode(StatusCodes.Status500InternalServerError, "No se pudo borrar la cuenta.");

        DeleteAvatarFile(avatar);
        return NoContent();
    }

    private void DeleteAvatarFile(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            var path = Path.Combine(env.ContentRootPath, "uploads", "avatars", Path.GetFileName(name));
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch (Exception)
        {
            // La cuenta ya está borrada; si el archivo no se puede eliminar, quedará huérfano.
            // Más adelante conviene una tarea de limpieza que lo detecte.
        }
    }
}