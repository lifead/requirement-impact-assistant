using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequirementImpactAssistant.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnalysisResultMetadataPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisMode",
                table: "AiAnalysisResults",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "DirectLlm");

            migrationBuilder.AddColumn<bool>(
                name: "ManualContextForwardedToExternalAiOrRag",
                table: "AiAnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MetadataAdapterName",
                table: "AiAnalysisResults",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataEngineName",
                table: "AiAnalysisResults",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataModelWorkflowProfileName",
                table: "AiAnalysisResults",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataProviderName",
                table: "AiAnalysisResults",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetrievedContextState",
                table: "AiAnalysisResults",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unavailable");

            migrationBuilder.AddColumn<string>(
                name: "Warnings",
                table: "AiAnalysisResults",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisMode",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "ManualContextForwardedToExternalAiOrRag",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "MetadataAdapterName",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "MetadataEngineName",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "MetadataModelWorkflowProfileName",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "MetadataProviderName",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "RetrievedContextState",
                table: "AiAnalysisResults");

            migrationBuilder.DropColumn(
                name: "Warnings",
                table: "AiAnalysisResults");
        }
    }
}
