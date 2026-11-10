using Microsoft.AspNetCore.Identity;

namespace Kalories.Server.Data;

public class ApplicationUser : IdentityUser
{
    public DateTime? ConsentAcceptedAt { get; set; }
    public string? ConsentVersion { get; set; }
}