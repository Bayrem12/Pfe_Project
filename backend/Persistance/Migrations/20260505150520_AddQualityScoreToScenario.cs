using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityScoreToScenario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastAnalyzedAt",
                table: "Scenarios",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityLabel",
                table: "Scenarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualityScore",
                table: "Scenarios",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAnalyzedAt",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "QualityLabel",
                table: "Scenarios");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "Scenarios");
        }
    }
}
