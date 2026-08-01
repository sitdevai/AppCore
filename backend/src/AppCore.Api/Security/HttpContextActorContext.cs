using System.Security.Claims;
using AppCore.Application.Common.Abstractions;

namespace AppCore.Api.Security;

public sealed class HttpContextActorContext(
    IHttpContextAccessor httpContextAccessor)
    : IActorContext
{
    public string? ActorId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(
            ClaimTypes.NameIdentifier);
}
