using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyOutboxMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "OutboxMessages");

            migrationBuilder.RenameColumn(
                name: "ToolName",
                table: "OutboxMessages",
                newName: "Topic");

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "OutboxMessages",
                newName: "Payload");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Topic",
                table: "OutboxMessages",
                newName: "ToolName");

            migrationBuilder.RenameColumn(
                name: "Payload",
                table: "OutboxMessages",
                newName: "EventType");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);
        }
    }
}
