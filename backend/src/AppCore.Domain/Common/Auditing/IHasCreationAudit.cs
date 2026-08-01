namespace AppCore.Domain.Common.Auditing;

public interface IHasCreationAudit
{
    DateTime CreatedAtUtc { get; set; }

    string? CreatedByActorId { get; set; }
}
