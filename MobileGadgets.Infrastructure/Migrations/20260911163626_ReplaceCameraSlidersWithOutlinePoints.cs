using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCameraSlidersWithOutlinePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraPadPx",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "CameraRoundR",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "CoverCameraPlate",
                table: "HeroModels");

            migrationBuilder.AddColumn<string>(
                name: "CameraOutlinePointsJson",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraSpotsJson",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraOutlinePointsJson",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "CameraSpotsJson",
                table: "HeroModels");

            migrationBuilder.AddColumn<double>(
                name: "CameraPadPx",
                table: "HeroModels",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CameraRoundR",
                table: "HeroModels",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CoverCameraPlate",
                table: "HeroModels",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
