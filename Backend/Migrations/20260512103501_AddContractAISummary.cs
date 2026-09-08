using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddContractAISummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SummarizedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryStatus",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SummarizedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SummaryStatus",
                table: "Contracts");
        }
    }
}
