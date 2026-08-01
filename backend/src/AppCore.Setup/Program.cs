using AppCore.Api;
using AppCore.Application.Security;
using AppCore.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length < 2)
{
    Console.Error.WriteLine(
        "Usage: create-owner <username> [email] | enable-owner <user-id> | mark-ready <user-id> | complete-owner <user-id>");
    return 2;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Services.AddSetupServices(builder.Configuration);
using IHost host = builder.Build();
await host.StartAsync();
await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
BootstrapIdentityPreparationService bootstrap =
    scope.ServiceProvider.GetRequiredService<BootstrapIdentityPreparationService>();

switch (args[0])
{
    case "create-owner":
        OneTimeChallengeResult challenge = await bootstrap.CreateOwnerAsync(
            args[1],
            args.Length > 2 ? args[2] : null);
        Console.WriteLine($"Owner user ID: {challenge.UserId}");
        Console.WriteLine($"One-time activation code: {challenge.Code}");
        Console.WriteLine($"Expires at UTC: {challenge.ExpiresAtUtc:O}");
        return 0;
    case "enable-owner" when Guid.TryParse(args[1], out Guid enableUserId):
        return await bootstrap.EnablePreparedOwnerAsync(enableUserId) ? 0 : 1;
    case "mark-ready" when Guid.TryParse(args[1], out Guid readyUserId):
        return await bootstrap.MarkReadyForPrivilegeGrantAsync(readyUserId)
            ? 0
            : 1;
    case "complete-owner" when Guid.TryParse(args[1], out Guid completeUserId):
        return await bootstrap.CompletePrivilegeGrantAsync(completeUserId)
            ? 0
            : 1;
    default:
        Console.Error.WriteLine("Invalid setup command or user ID.");
        return 2;
}
