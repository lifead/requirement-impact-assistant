using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequirementImpactAssistant.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRetrievedContextItemsPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetrievedContextItems",
                columns: table => new
                {
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ExternalReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FragmentId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    Excerpt = table.Column<string>(type: "TEXT", nullable: true),
                    UrlOrReference = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Rank = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<double>(type: "REAL", nullable: true),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AdapterName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Completeness = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    WarningOrLimitationNote = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetrievedContextItems", x => new { x.AiAnalysisResultId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RetrievedContextItems_AiAnalysisResults_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "AiAnalysisResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetrievedContextItems");
        }
    }
}
