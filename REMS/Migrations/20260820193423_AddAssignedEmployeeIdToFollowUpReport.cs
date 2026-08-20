using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedEmployeeIdToFollowUpReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedEmployeeId",
                table: "FollowUpReports",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedEmployeeId",
                table: "FollowUpReports");
        }
    }
}
