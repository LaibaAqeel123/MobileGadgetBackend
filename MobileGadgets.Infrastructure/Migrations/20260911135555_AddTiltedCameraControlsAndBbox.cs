using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTiltedCameraControlsAndBbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "CaseBboxJson",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraPadPx",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "CameraRoundR",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "CaseBboxJson",
                table: "HeroModels");
        }
    }
}
