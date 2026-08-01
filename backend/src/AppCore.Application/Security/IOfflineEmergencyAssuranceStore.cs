namespace AppCore.Application.Security;

public interface IOfflineEmergencyAssuranceStore
{
    Task<bool> ConsumeTwoCustodianAssuranceAsync(
        string firstShare,
        string secondShare,
        string correlationId,
        CancellationToken cancellationToken = default);
}
