using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverCameraPlate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CoverCameraPlate",
                table: "HeroModels",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverCameraPlate",
                table: "HeroModels");
        }
    }
}
