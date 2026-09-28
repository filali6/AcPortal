using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReportStructuredData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataJson",
                table: "SlaWeeklyReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "SlaWeeklyReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeekStart",
                table: "SlaWeeklyReports",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FunctionalDomain",
                table: "SlaRules",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataJson",
                table: "SlaWeeklyReports");

            migrationBuilder.DropColumn(
                name: "IsManual",
                table: "SlaWeeklyReports");

            migrationBuilder.DropColumn(
                name: "WeekStart",
                table: "SlaWeeklyReports");

            migrationBuilder.DropColumn(
                name: "FunctionalDomain",
                table: "SlaRules");
        }
    }
}
