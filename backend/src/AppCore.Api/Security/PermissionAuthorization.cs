using AppCore.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AppCore.Api.Security;

public static class PermissionPolicies
{
    public const string Prefix = "Permission:";

    public static string For(string permissionId) => $"{Prefix}{permissionId}";
}

public sealed record PermissionRequirement(string PermissionId)
    : IAuthorizationRequirement;

public sealed class PermissionPolicyProvider(
    IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicies.Prefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        string permissionId = policyName[PermissionPolicies.Prefix.Length..];
        if (SystemPermissions.Find(permissionId) is null)
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder(AuthenticationSchemes.Session)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionId))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class PermissionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IPermissionAuthorizationService permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items[nameof(ValidatedSession)] is ValidatedSession session
            && await permissions.HasPermissionAsync(
                session,
                requirement.PermissionId,
                httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
