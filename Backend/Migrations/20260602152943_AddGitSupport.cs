using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddGitSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitRepoUrl",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCommitAt",
                table: "ProjectSteps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastCommitHash",
                table: "ProjectSteps",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StepConfigFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    CommitHash = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepConfigFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepConfigFiles_ProjectSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "ProjectSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StepConfigFiles_StepId",
                table: "StepConfigFiles",
                column: "StepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StepConfigFiles");

            migrationBuilder.DropColumn(
                name: "GitRepoUrl",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "LastCommitAt",
                table: "ProjectSteps");

            migrationBuilder.DropColumn(
                name: "LastCommitHash",
                table: "ProjectSteps");
        }
    }
}
