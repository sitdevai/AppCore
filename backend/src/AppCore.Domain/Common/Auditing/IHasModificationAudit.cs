namespace AppCore.Domain.Common.Auditing;

public interface IHasModificationAudit
{
    DateTime? LastModifiedAtUtc { get; set; }

    string? LastModifiedByActorId { get; set; }
}
