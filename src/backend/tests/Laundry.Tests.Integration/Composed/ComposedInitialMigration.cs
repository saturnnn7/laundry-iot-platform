using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Laundry.Tests.Integration.Composed;

[DbContext(typeof(ComposedDbContext))]
[Migration("20260101000000_ComposedInitial")]
public sealed class ComposedInitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "composed");

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "composed",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "text", nullable: true),
                dead_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_outbox_messages", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_pending",
            schema: "composed",
            table: "outbox_messages",
            column: "next_attempt_at_utc",
            filter: "processed_at_utc IS NULL AND dead_at_utc IS NULL");

        migrationBuilder.CreateTable(
            name: "probe_items",
            schema: "composed",
            columns: table => new { Id = table.Column<int>(type: "integer", nullable: false) },
            constraints: table => table.PrimaryKey("PK_probe_items", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "probe_items", schema: "composed");
        migrationBuilder.DropTable(name: "outbox_messages", schema: "composed");
    }
}
