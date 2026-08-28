using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileGadgets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeroGenerations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeroModelId = table.Column<int>(type: "int", nullable: false),
                    DesignImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroGenerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeroGenerations_HeroModels_HeroModelId",
                        column: x => x.HeroModelId,
                        principalTable: "HeroModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeroGenerations_HeroModelId",
                table: "HeroGenerations",
                column: "HeroModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroGenerations");
        }
    }
}
