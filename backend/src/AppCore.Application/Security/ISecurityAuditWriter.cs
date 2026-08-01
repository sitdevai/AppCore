namespace AppCore.Application.Security;

public interface ISecurityAuditWriter
{
    Task WriteAsync(
        SecurityAuditEntry entry,
        CancellationToken cancellationToken = default);
}
