namespace AppCore.Contracts.System;

public sealed record SystemInfoResponse(
    string Service,
    string ApiVersion,
    DateTimeOffset ServerTimeUtc);
