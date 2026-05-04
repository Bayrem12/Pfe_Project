using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFailureAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisJson",
                table: "TestResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisJson",
                table: "StepResults",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAnalysisJson",
                table: "TestResults");

            migrationBuilder.DropColumn(
                name: "AiAnalysisJson",
                table: "StepResults");
        }
    }
}
