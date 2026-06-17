using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequirementImpactAssistant.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectRequestTypeToAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectRequestType",
                table: "Analyses",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "Other");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectRequestType",
                table: "Analyses");
        }
    }
}
