using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class CleanRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropColumn(
                name: "PortfolioDirectorId",
                table: "Projects");

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioDirectorId",
                table: "Portfolios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_KeycloakId",
                table: "Users",
                column: "KeycloakId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolRoles_ToolId",
                table: "ToolRoles",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_Streams_BusinessTeamLeadId",
                table: "Streams",
                column: "BusinessTeamLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Streams_ProjectId",
                table: "Streams",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Streams_TechnicalTeamLeadId",
                table: "Streams",
                column: "TechnicalTeamLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamMembers_ConsultantId",
                table: "StreamMembers",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamMembers_StreamId_ConsultantId",
                table: "StreamMembers",
                columns: new[] { "StreamId", "ConsultantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSteps_ProjectId",
                table: "ProjectSteps",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSteps_StreamId",
                table: "ProjectSteps",
                column: "StreamId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PortfolioId",
                table: "Projects",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectManagerId",
                table: "Projects",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_PortfolioDirectorId",
                table: "Portfolios",
                column: "PortfolioDirectorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantToolRoles_ConsultantId",
                table: "ConsultantToolRoles",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantToolRoles_ToolId",
                table: "ConsultantToolRoles",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantToolRoles_ToolRoleId",
                table: "ConsultantToolRoles",
                column: "ToolRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultantToolRoles_AcpTools_ToolId",
                table: "ConsultantToolRoles",
                column: "ToolId",
                principalTable: "AcpTools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultantToolRoles_ToolRoles_ToolRoleId",
                table: "ConsultantToolRoles",
                column: "ToolRoleId",
                principalTable: "ToolRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultantToolRoles_Users_ConsultantId",
                table: "ConsultantToolRoles",
                column: "ConsultantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Portfolios_Users_PortfolioDirectorId",
                table: "Portfolios",
                column: "PortfolioDirectorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Portfolios_PortfolioId",
                table: "Projects",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_ProjectManagerId",
                table: "Projects",
                column: "ProjectManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectSteps_Projects_ProjectId",
                table: "ProjectSteps",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectSteps_Streams_StreamId",
                table: "ProjectSteps",
                column: "StreamId",
                principalTable: "Streams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StreamMembers_Streams_StreamId",
                table: "StreamMembers",
                column: "StreamId",
                principalTable: "Streams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StreamMembers_Users_ConsultantId",
                table: "StreamMembers",
                column: "ConsultantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Streams_Projects_ProjectId",
                table: "Streams",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Streams_Users_BusinessTeamLeadId",
                table: "Streams",
                column: "BusinessTeamLeadId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Streams_Users_TechnicalTeamLeadId",
                table: "Streams",
                column: "TechnicalTeamLeadId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ToolRoles_AcpTools_ToolId",
                table: "ToolRoles",
                column: "ToolId",
                principalTable: "AcpTools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultantToolRoles_AcpTools_ToolId",
                table: "ConsultantToolRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultantToolRoles_ToolRoles_ToolRoleId",
                table: "ConsultantToolRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsultantToolRoles_Users_ConsultantId",
                table: "ConsultantToolRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Portfolios_Users_PortfolioDirectorId",
                table: "Portfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Portfolios_PortfolioId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_ProjectManagerId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectSteps_Projects_ProjectId",
                table: "ProjectSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectSteps_Streams_StreamId",
                table: "ProjectSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_StreamMembers_Streams_StreamId",
                table: "StreamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_StreamMembers_Users_ConsultantId",
                table: "StreamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Streams_Projects_ProjectId",
                table: "Streams");

            migrationBuilder.DropForeignKey(
                name: "FK_Streams_Users_BusinessTeamLeadId",
                table: "Streams");

            migrationBuilder.DropForeignKey(
                name: "FK_Streams_Users_TechnicalTeamLeadId",
                table: "Streams");

            migrationBuilder.DropForeignKey(
                name: "FK_ToolRoles_AcpTools_ToolId",
                table: "ToolRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_KeycloakId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ToolRoles_ToolId",
                table: "ToolRoles");

            migrationBuilder.DropIndex(
                name: "IX_Streams_BusinessTeamLeadId",
                table: "Streams");

            migrationBuilder.DropIndex(
                name: "IX_Streams_ProjectId",
                table: "Streams");

            migrationBuilder.DropIndex(
                name: "IX_Streams_TechnicalTeamLeadId",
                table: "Streams");

            migrationBuilder.DropIndex(
                name: "IX_StreamMembers_ConsultantId",
                table: "StreamMembers");

            migrationBuilder.DropIndex(
                name: "IX_StreamMembers_StreamId_ConsultantId",
                table: "StreamMembers");

            migrationBuilder.DropIndex(
                name: "IX_ProjectSteps_ProjectId",
                table: "ProjectSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProjectSteps_StreamId",
                table: "ProjectSteps");

            migrationBuilder.DropIndex(
                name: "IX_Projects_PortfolioId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectManagerId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Portfolios_PortfolioDirectorId",
                table: "Portfolios");

            migrationBuilder.DropIndex(
                name: "IX_ConsultantToolRoles_ConsultantId",
                table: "ConsultantToolRoles");

            migrationBuilder.DropIndex(
                name: "IX_ConsultantToolRoles_ToolId",
                table: "ConsultantToolRoles");

            migrationBuilder.DropIndex(
                name: "IX_ConsultantToolRoles_ToolRoleId",
                table: "ConsultantToolRoles");

            migrationBuilder.DropColumn(
                name: "PortfolioDirectorId",
                table: "Portfolios");

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioDirectorId",
                table: "Projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChefEquipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId_ConsultantId",
                table: "TeamMembers",
                columns: new[] { "TeamId", "ConsultantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ProjectId",
                table: "Teams",
                column: "ProjectId",
                unique: true);
        }
    }
}
