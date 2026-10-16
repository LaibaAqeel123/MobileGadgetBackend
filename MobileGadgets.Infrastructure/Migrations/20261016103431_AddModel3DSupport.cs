using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModel3DSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GlbUrl",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintLayoutJson",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintMaterialName",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationReportJson",
                table: "HeroModels",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlbUrl",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "PrintLayoutJson",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "PrintMaterialName",
                table: "HeroModels");

            migrationBuilder.DropColumn(
                name: "ValidationReportJson",
                table: "HeroModels");
        }
    }
}
