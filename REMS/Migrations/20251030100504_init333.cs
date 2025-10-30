using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REMS.Migrations
{
    /// <inheritdoc />
    public partial class init333 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomFieldsJson",
                table: "FollowUpReports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFieldsJson",
                table: "FollowUpReports",
                type: "TEXT",
                nullable: true);
        }
    }
}
