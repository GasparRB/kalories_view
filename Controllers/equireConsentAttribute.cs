using System.Security.Claims;
using Kalories.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Kalories.Server.Controllers;

// Bloquea el acceso a los datos personales si el usuario no ha aceptado
// la versión actual del consentimiento.
public class RequireConsentAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var version = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ConsentVersion)
            .FirstOrDefaultAsync();

        if (version != ConsentInfo.CurrentVersion)
        {
            context.Result = new ObjectResult(new { error = "consent_required" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}