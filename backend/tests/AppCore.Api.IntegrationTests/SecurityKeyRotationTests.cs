using AppCore.Api.Configuration;
using AppCore.Api.Security;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AppCore.Api.IntegrationTests;

public sealed class SecurityKeyRotationTests
{
    [Fact]
    public async Task CurrentAndRetainedChallengeKeysRemainResolvable()
    {
        var settings = new SecurityKeySettings
        {
            CurrentVersion = 2,
            Keys = new Dictionary<int, string>
            {
                [1] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
                [2] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()),
            },
        };
        var provider = new ConfigurationSecurityKeyProvider(
            Options.Create(settings),
            new StubHostEnvironment());

        Assert.Equal(
            2,
            (await provider.GetCurrentKeyAsync("challenge-hmac")).Version);
        Assert.Equal(
            1,
            (await provider.GetKeyAsync("challenge-hmac", 1))?.Version);
        Assert.Null(await provider.GetKeyAsync("challenge-hmac", 3));
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
