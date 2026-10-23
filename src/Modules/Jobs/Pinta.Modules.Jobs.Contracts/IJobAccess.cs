namespace Pinta.Modules.Jobs.Contracts;

/// <summary>Who may see a job's resources, for modules that store them by job id (S1: others get 404).</summary>
public interface IJobAccess
{
    Task<bool> IsClientOfAsync(Guid jobId, Guid userId, CancellationToken cancellationToken = default);
}
