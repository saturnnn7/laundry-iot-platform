using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Laundry.Booking.Application;
using Microsoft.EntityFrameworkCore;

namespace Laundry.Booking.Infrastructure;

public sealed class BookingDbContext(
    DbContextOptions<BookingDbContext> options,
    ModuleContextServices services) : ModuleDbContext(options, services), IBookingUnitOfWork
{
    public const string SchemaName = "booking";

    protected override string Schema => SchemaName;
}
