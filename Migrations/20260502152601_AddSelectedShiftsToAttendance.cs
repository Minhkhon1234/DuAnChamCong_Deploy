using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DUANCHAMCONG.Migrations
{
    public partial class AddSelectedShiftsToAttendance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedShifts",
                table: "Attendances",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedShifts",
                table: "Attendances");
        }
    }
}
