using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DUANCHAMCONG.Migrations
{
    public partial class AddEarlyLeaveReasonToAttendance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EarlyLeaveReason",
                table: "Attendances",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyLeaveReason",
                table: "Attendances");
        }
    }
}
