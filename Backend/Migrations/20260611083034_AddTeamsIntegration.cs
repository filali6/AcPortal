using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FromTeams",
                table: "TaskComments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TeamsMessageId",
                table: "TaskComments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsChannelId",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsChannelUrl",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TeamsSetupFailed",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TeamsTeamId",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsTeamUrl",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsThreadId",
                table: "AcpTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsThreadUrl",
                table: "AcpTasks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromTeams",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "TeamsMessageId",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "TeamsChannelId",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "TeamsChannelUrl",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "TeamsSetupFailed",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TeamsTeamId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TeamsTeamUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TeamsThreadId",
                table: "AcpTasks");

            migrationBuilder.DropColumn(
                name: "TeamsThreadUrl",
                table: "AcpTasks");
        }
    }
}
