using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nettest.Migrations
{
    /// <inheritdoc />
    public partial class AddUrgencyLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Urgency",
                table: "MaintenanceRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "MaintenanceRequests");
        }
    }
}
