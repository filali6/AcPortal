using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaRules_TaskType",
                table: "SlaRules");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "AcpTasks");

            migrationBuilder.RenameColumn(
                name: "WarningThresholdPercent",
                table: "SlaRules",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "TaskType",
                table: "SlaRules",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "MaxDurationDays",
                table: "SlaRules",
                newName: "SlaDays");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SlaRules",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "SlaRules");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "SlaRules",
                newName: "WarningThresholdPercent");

            migrationBuilder.RenameColumn(
                name: "SlaDays",
                table: "SlaRules",
                newName: "MaxDurationDays");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "SlaRules",
                newName: "TaskType");

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "Streams",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "AcpTasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SlaRules_TaskType",
                table: "SlaRules",
                column: "TaskType",
                unique: true);
        }
    }
}
