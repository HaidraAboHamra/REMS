using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REMS.Migrations
{
    /// <inheritdoc />
    public partial class init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedEmployee",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientOrProject",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedDate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedDetails",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedItems",
                table: "FollowUpReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedDurationDays",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastUpdatedBy",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedDate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskDetails",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalItems",
                table: "FollowUpReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FollowUpReportUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FollowUpReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedItems = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedDetails = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    ProgressPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUpReportUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowUpReportUpdates_FollowUpReports_FollowUpReportId",
                        column: x => x.FollowUpReportId,
                        principalTable: "FollowUpReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpReportUpdates_FollowUpReportId",
                table: "FollowUpReportUpdates",
                column: "FollowUpReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowUpReportUpdates");

            migrationBuilder.DropColumn(
                name: "AssignedEmployee",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "ClientOrProject",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "CompletedDetails",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "CompletedItems",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "ExpectedDurationDays",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "LastUpdatedBy",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "LastUpdatedDate",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "TaskDetails",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "TotalItems",
                table: "FollowUpReports");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
