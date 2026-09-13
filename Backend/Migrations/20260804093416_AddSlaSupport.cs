using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Streams",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "Streams",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "AcpTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SlaRuleId",
                table: "AcpTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "AcpTasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SlaRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<string>(type: "text", nullable: false),
                    MaxDurationDays = table.Column<int>(type: "integer", nullable: false),
                    WarningThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaRules_TaskType",
                table: "SlaRules",
                column: "TaskType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaRules");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "AcpTasks");

            migrationBuilder.DropColumn(
                name: "SlaRuleId",
                table: "AcpTasks");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "AcpTasks");
        }
    }
}
