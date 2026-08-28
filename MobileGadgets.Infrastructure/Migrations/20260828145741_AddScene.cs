using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScene : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CamY = table.Column<double>(type: "float", nullable: false),
                    CamZ = table.Column<double>(type: "float", nullable: false),
                    PitchDegrees = table.Column<double>(type: "float", nullable: false),
                    Focal = table.Column<double>(type: "float", nullable: false),
                    LeanDegrees = table.Column<double>(type: "float", nullable: false),
                    YawDegrees = table.Column<double>(type: "float", nullable: false),
                    BackgroundTopColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BackgroundBottomColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FloorTopColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FloorBottomColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WallTopColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WallBottomColor = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Scenes",
                columns: new[] { "Id", "BackgroundBottomColor", "BackgroundTopColor", "CamY", "CamZ", "FloorBottomColor", "FloorTopColor", "Focal", "IsDefault", "LeanDegrees", "Name", "PitchDegrees", "WallBottomColor", "WallTopColor", "YawDegrees" },
                values: new object[,]
                {
                    { 1, "#141416", "#2c2c2f", 1.1499999999999999, -2.6000000000000001, "#111113", "#333336", 1650.0, true, 10.0, "Dark Studio", 13.0, "#28282b", "#3d3d40", -22.0 },
                    { 2, "#e2e2de", "#f4f4f2", 1.1499999999999999, -2.6000000000000001, "#d6d6d2", "#ffffff", 1650.0, false, 10.0, "Light Studio", 13.0, "#ebebe8", "#faf9f7", -22.0 }
                });

            // Defaults to Scene Id 1 ("Dark Studio", the seeded default) — safe for any
            // pre-existing HeroGenerations rows, since that Scene always exists by this point.
            migrationBuilder.AddColumn<int>(
                name: "SceneId",
                table: "HeroGenerations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_HeroGenerations_SceneId",
                table: "HeroGenerations",
                column: "SceneId");

            migrationBuilder.AddForeignKey(
                name: "FK_HeroGenerations_Scenes_SceneId",
                table: "HeroGenerations",
                column: "SceneId",
                principalTable: "Scenes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HeroGenerations_Scenes_SceneId",
                table: "HeroGenerations");

            migrationBuilder.DropTable(
                name: "Scenes");

            migrationBuilder.DropIndex(
                name: "IX_HeroGenerations_SceneId",
                table: "HeroGenerations");

            migrationBuilder.DropColumn(
                name: "SceneId",
                table: "HeroGenerations");
        }
    }
}
