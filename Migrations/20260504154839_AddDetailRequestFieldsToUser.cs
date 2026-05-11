using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DUANCHAMCONG.Migrations
{
    public partial class AddDetailRequestFieldsToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanViewDetails",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequestViewDetails",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanViewDetails",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RequestViewDetails",
                table: "Users");
        }
    }
}
