using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DUANCHAMCONG.Migrations
{
    public partial class AddSpoofingDetection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Accuracy",
                table: "Attendances",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "Attendances",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accuracy",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "Attendances");
        }
    }
}
