using Microsoft.AspNetCore.Identity;

namespace AppCore.Infrastructure.Security;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public bool IsBuiltIn { get; set; }
    public bool IsProtected { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class RolePermissionAssignment
{
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
    public string PermissionId { get; set; } = null!;
}

public sealed class PermissionRecord
{
    public string Id { get; set; } = null!;
    public string Assurance { get; set; } = null!;
    public string Scope { get; set; } = null!;
}
