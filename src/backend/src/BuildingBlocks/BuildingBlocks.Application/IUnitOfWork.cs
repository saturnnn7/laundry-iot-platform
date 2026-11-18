namespace Laundry.BuildingBlocks.Application;

public interface IUnitOfWork
{
    void AddIntegrationEvent(IIntegrationEvent integrationEvent);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
