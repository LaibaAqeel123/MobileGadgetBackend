using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroModelPoseType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DesignQuadJson",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoseType",
                table: "HeroModels",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesignQuadJson",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "PoseType",
                table: "HeroModels");
        }
    }
}
