using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using REMS.Data;

#nullable disable

namespace REMS.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827000000_SharedHubAndTelegramLinking")]
public partial class SharedHubAndTelegramLinking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSharedHub",
            table: "StoredFiles",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "TelegramLinkedAt",
            table: "Users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TelegramLinkToken",
            table: "Users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TelegramUsername",
            table: "Users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoredFiles_IsSharedHub_CreatedAt",
            table: "StoredFiles",
            columns: new[] { "IsSharedHub", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_StoredFiles_IsSharedHub_CreatedAt", table: "StoredFiles");
        migrationBuilder.DropColumn(name: "IsSharedHub", table: "StoredFiles");
        migrationBuilder.DropColumn(name: "TelegramLinkedAt", table: "Users");
        migrationBuilder.DropColumn(name: "TelegramLinkToken", table: "Users");
        migrationBuilder.DropColumn(name: "TelegramUsername", table: "Users");
    }
}
