using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REMS.Migrations
{
    /// <inheritdoc />
    public partial class AddLateTaskPenalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeductions",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LatePenaltyAmount",
                table: "Settings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LatePenaltyAmount",
                table: "FollowUpReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "LatePenaltyApplied",
                table: "FollowUpReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatePenaltyAppliedAt",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "LatePenaltyAmount",
                value: 0m);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "TotalDeductions",
                value: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalDeductions",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LatePenaltyAmount",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LatePenaltyAmount",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "LatePenaltyApplied",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "LatePenaltyAppliedAt",
                table: "FollowUpReports");
        }
    }
}
