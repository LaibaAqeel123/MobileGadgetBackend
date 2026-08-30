using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSceneToApprovedPose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BackgroundBottomColor", "BackgroundTopColor", "CamY", "CamZ", "FloorBottomColor", "FloorTopColor", "Focal", "LeanDegrees", "PitchDegrees", "WallBottomColor", "WallTopColor", "YawDegrees" },
                values: new object[] { "#0c0b0b", "#5a5654", 1.3500000000000001, -2.1000000000000001, "#080707", "#463e3a", 1500.0, 5.0, 9.0, "#242221", "#686360", 0.0 });

            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CamY", "CamZ", "Focal", "LeanDegrees", "PitchDegrees", "YawDegrees" },
                values: new object[] { 1.3500000000000001, -2.1000000000000001, 1500.0, 5.0, 9.0, 0.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BackgroundBottomColor", "BackgroundTopColor", "CamY", "CamZ", "FloorBottomColor", "FloorTopColor", "Focal", "LeanDegrees", "PitchDegrees", "WallBottomColor", "WallTopColor", "YawDegrees" },
                values: new object[] { "#141416", "#2c2c2f", 1.1499999999999999, -2.6000000000000001, "#111113", "#333336", 1650.0, 15.0, 13.0, "#28282b", "#3d3d40", -30.0 });

            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CamY", "CamZ", "Focal", "LeanDegrees", "PitchDegrees", "YawDegrees" },
                values: new object[] { 1.1499999999999999, -2.6000000000000001, 1650.0, 15.0, 13.0, -30.0 });
        }
    }
}
