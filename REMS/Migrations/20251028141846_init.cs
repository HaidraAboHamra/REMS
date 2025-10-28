using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REMS.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllTasksDone",
                table: "FollowUpReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractDate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractFileName",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractFilePath",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Coordinator",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Governorate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductsCount",
                table: "FollowUpReports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreType",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkDate",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "AllTasksDone",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "ContractDate",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "ContractFileName",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "ContractFilePath",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "Coordinator",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "Governorate",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "ProductsCount",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "StoreName",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "StoreType",
                table: "FollowUpReports");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "FollowUpReports");
        }
    }
}
