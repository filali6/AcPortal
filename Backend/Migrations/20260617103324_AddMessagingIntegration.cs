using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamsSetupFailed",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TeamsTeamId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TeamsTeamUrl",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "TeamsMessageId",
                table: "TaskComments",
                newName: "MessagingMessageId");

            migrationBuilder.RenameColumn(
                name: "FromTeams",
                table: "TaskComments",
                newName: "FromMessaging");

            migrationBuilder.RenameColumn(
                name: "TeamsChannelUrl",
                table: "Streams",
                newName: "MessagingChannelUrl");

            migrationBuilder.RenameColumn(
                name: "TeamsChannelId",
                table: "Streams",
                newName: "MessagingChannelId");

            migrationBuilder.RenameColumn(
                name: "TeamsThreadUrl",
                table: "AcpTasks",
                newName: "MessagingThreadUrl");

            migrationBuilder.RenameColumn(
                name: "TeamsThreadId",
                table: "AcpTasks",
                newName: "MessagingThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MessagingMessageId",
                table: "TaskComments",
                newName: "TeamsMessageId");

            migrationBuilder.RenameColumn(
                name: "FromMessaging",
                table: "TaskComments",
                newName: "FromTeams");

            migrationBuilder.RenameColumn(
                name: "MessagingChannelUrl",
                table: "Streams",
                newName: "TeamsChannelUrl");

            migrationBuilder.RenameColumn(
                name: "MessagingChannelId",
                table: "Streams",
                newName: "TeamsChannelId");

            migrationBuilder.RenameColumn(
                name: "MessagingThreadUrl",
                table: "AcpTasks",
                newName: "TeamsThreadUrl");

            migrationBuilder.RenameColumn(
                name: "MessagingThreadId",
                table: "AcpTasks",
                newName: "TeamsThreadId");

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
        }
    }
}
