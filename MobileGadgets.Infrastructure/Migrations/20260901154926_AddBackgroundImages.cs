using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundImageUrl",
                table: "Scenes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomBackgroundImageUrl",
                table: "HeroGenerations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 1,
                column: "BackgroundImageUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 2,
                column: "BackgroundImageUrl",
                value: null);

            migrationBuilder.InsertData(
                table: "Scenes",
                columns: new[] { "Id", "BackgroundBottomColor", "BackgroundImageUrl", "BackgroundTopColor", "CamY", "CamZ", "FloorBottomColor", "FloorTopColor", "Focal", "IsDefault", "LeanDegrees", "Name", "PitchDegrees", "WallBottomColor", "WallTopColor", "YawDegrees" },
                values: new object[,]
                {
                    { 3, "#141416", "/uploads/seed-bg-warm-beige-studio.png", "#2c2c2f", 1.3500000000000001, -2.1000000000000001, "#111113", "#333336", 1500.0, false, 5.0, "Warm Beige Backdrop", 9.0, "#28282b", "#3d3d40", 0.0 },
                    { 4, "#141416", "/uploads/seed-bg-cool-slate-studio.png", "#2c2c2f", 1.3500000000000001, -2.1000000000000001, "#111113", "#333336", 1500.0, false, 5.0, "Cool Slate Backdrop", 9.0, "#28282b", "#3d3d40", 0.0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DropColumn(
                name: "BackgroundImageUrl",
                table: "Scenes");

            migrationBuilder.DropColumn(
                name: "CustomBackgroundImageUrl",
                table: "HeroGenerations");
        }
    }
}
