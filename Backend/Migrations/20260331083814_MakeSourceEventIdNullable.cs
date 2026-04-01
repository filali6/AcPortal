using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class MakeSourceEventIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcpTasks_AcpEvents_SourceEventId",
                table: "AcpTasks");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceEventId",
                table: "AcpTasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_AcpTasks_AcpEvents_SourceEventId",
                table: "AcpTasks",
                column: "SourceEventId",
                principalTable: "AcpEvents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcpTasks_AcpEvents_SourceEventId",
                table: "AcpTasks");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceEventId",
                table: "AcpTasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AcpTasks_AcpEvents_SourceEventId",
                table: "AcpTasks",
                column: "SourceEventId",
                principalTable: "AcpEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
