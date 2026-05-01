using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class RemoveComputerVisionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetectionResults");

            migrationBuilder.DropTable(
                name: "UIElementcaches");

            migrationBuilder.DropTable(
                name: "UIPatterns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DetectionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundingBox = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ElementType = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedById = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ScreenshotPath = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetectionResults_StepResults_StepResultId",
                        column: x => x.StepResultId,
                        principalTable: "StepResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UIElementcaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DetectionCount = table.Column<int>(type: "integer", nullable: false),
                    ElementIdentifier = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastDetectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedById = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PageUrl = table.Column<string>(type: "text", nullable: false),
                    SelectorType = table.Column<string>(type: "text", nullable: false),
                    SelectorValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UIElementcaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UIElementcaches_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UIPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedById = table.Column<string>(type: "text", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PatternName = table.Column<string>(type: "text", nullable: false),
                    PatternType = table.Column<string>(type: "text", nullable: false),
                    TemplateData = table.Column<string>(type: "text", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UIPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UIPatterns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetectionResults_StepResultId",
                table: "DetectionResults",
                column: "StepResultId");

            migrationBuilder.CreateIndex(
                name: "IX_UIElementcaches_ProjectId",
                table: "UIElementcaches",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UIPatterns_ProjectId",
                table: "UIPatterns",
                column: "ProjectId");
        }
    }
}
