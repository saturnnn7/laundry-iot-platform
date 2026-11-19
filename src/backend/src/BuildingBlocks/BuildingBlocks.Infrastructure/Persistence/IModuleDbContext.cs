namespace Laundry.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Marks the DbContext that owns a module schema. Implemented by <see cref="ModuleDbContext"/>
/// and by contexts that derive from another DbContext and compose <see cref="ModuleDbContextCore"/>.
/// </summary>
public interface IModuleDbContext
{
    string SchemaName { get; }
}
