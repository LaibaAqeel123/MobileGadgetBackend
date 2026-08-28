using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TuneDefaultScenePose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LeanDegrees", "YawDegrees" },
                values: new object[] { 15.0, -30.0 });

            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "LeanDegrees", "YawDegrees" },
                values: new object[] { 15.0, -30.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LeanDegrees", "YawDegrees" },
                values: new object[] { 10.0, -22.0 });

            migrationBuilder.UpdateData(
                table: "Scenes",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "LeanDegrees", "YawDegrees" },
                values: new object[] { 10.0, -22.0 });
        }
    }
}
