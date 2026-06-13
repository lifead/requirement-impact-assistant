using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequirementImpactAssistant.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMvpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalDescription = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectRequest = table.Column<string>(type: "TEXT", nullable: false),
                    SituationDescription = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeSource = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FixedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EngineName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PromptVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InputSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    RawResponse = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAnalysisResults_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContextFragments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextFragments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContextFragments_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertConclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConclusionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", nullable: false),
                    FixedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertConclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertConclusions_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContextSufficiency = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ResultUsefulness = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GeneralComment = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertEvaluations_Analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "Analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMaps",
                columns: table => new
                {
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangeSummaryId = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ChangeSummaryItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ChangeSummaryTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ChangeSummaryDescription = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeSummarySeverity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChangeSummaryRelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeSummaryNotes = table.Column<string>(type: "TEXT", nullable: false),
                    PreliminaryAssessmentId = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    PreliminaryAssessmentItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PreliminaryAssessmentTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    PreliminaryAssessmentDescription = table.Column<string>(type: "TEXT", nullable: false),
                    PreliminaryAssessmentSeverity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PreliminaryAssessmentRelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    PreliminaryAssessmentNotes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMaps", x => x.AiAnalysisResultId);
                    table.ForeignKey(
                        name: "FK_ImpactMaps_AiAnalysisResults_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "AiAnalysisResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    ExpertEvaluationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertCorrections_ExpertEvaluations_ExpertEvaluationId",
                        column: x => x.ExpertEvaluationId,
                        principalTable: "ExpertEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertEvaluatedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Mark = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    CorrectionText = table.Column<string>(type: "TEXT", nullable: false),
                    ExpertEvaluationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertEvaluatedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertEvaluatedItems_ExpertEvaluations_ExpertEvaluationId",
                        column: x => x.ExpertEvaluationId,
                        principalTable: "ExpertEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertMissedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    ExpertEvaluationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertMissedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertMissedItems_ExpertEvaluations_ExpertEvaluationId",
                        column: x => x.ExpertEvaluationId,
                        principalTable: "ExpertEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapAffectedApiInterfacesDocumentsTests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapAffectedApiInterfacesDocumentsTests", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapAffectedApiInterfacesDocumentsTests_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapAffectedArchitecturalConstraints",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapAffectedArchitecturalConstraints", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapAffectedArchitecturalConstraints_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapAffectedOrganizationalContextItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapAffectedOrganizationalContextItems", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapAffectedOrganizationalContextItems_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapAffectedProjectDecisions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapAffectedProjectDecisions", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapAffectedProjectDecisions_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapAffectedRequirements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapAffectedRequirements", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapAffectedRequirements_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapAffectedTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapAffectedTasks", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapAffectedTasks_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapClarificationQuestions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapClarificationQuestions", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapClarificationQuestions_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapContradictions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapContradictions", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapContradictions_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapMissingInformation",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapMissingInformation", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapMissingInformation_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapOptionsForExpertReview",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapOptionsForExpertReview", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapOptionsForExpertReview_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpactMapRisks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    AiAnalysisResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RelatedContextFragmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpactMapRisks", x => new { x.AiAnalysisResultId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImpactMapRisks_ImpactMaps_AiAnalysisResultId",
                        column: x => x.AiAnalysisResultId,
                        principalTable: "ImpactMaps",
                        principalColumn: "AiAnalysisResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalysisResults_AnalysisId",
                table: "AiAnalysisResults",
                column: "AnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContextFragments_AnalysisId",
                table: "ContextFragments",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertConclusions_AnalysisId",
                table: "ExpertConclusions",
                column: "AnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertCorrections_ExpertEvaluationId",
                table: "ExpertCorrections",
                column: "ExpertEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertEvaluatedItems_ExpertEvaluationId",
                table: "ExpertEvaluatedItems",
                column: "ExpertEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertEvaluations_AnalysisId",
                table: "ExpertEvaluations",
                column: "AnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertMissedItems_ExpertEvaluationId",
                table: "ExpertMissedItems",
                column: "ExpertEvaluationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContextFragments");

            migrationBuilder.DropTable(
                name: "ExpertConclusions");

            migrationBuilder.DropTable(
                name: "ExpertCorrections");

            migrationBuilder.DropTable(
                name: "ExpertEvaluatedItems");

            migrationBuilder.DropTable(
                name: "ExpertMissedItems");

            migrationBuilder.DropTable(
                name: "ImpactMapAffectedApiInterfacesDocumentsTests");

            migrationBuilder.DropTable(
                name: "ImpactMapAffectedArchitecturalConstraints");

            migrationBuilder.DropTable(
                name: "ImpactMapAffectedOrganizationalContextItems");

            migrationBuilder.DropTable(
                name: "ImpactMapAffectedProjectDecisions");

            migrationBuilder.DropTable(
                name: "ImpactMapAffectedRequirements");

            migrationBuilder.DropTable(
                name: "ImpactMapAffectedTasks");

            migrationBuilder.DropTable(
                name: "ImpactMapClarificationQuestions");

            migrationBuilder.DropTable(
                name: "ImpactMapContradictions");

            migrationBuilder.DropTable(
                name: "ImpactMapMissingInformation");

            migrationBuilder.DropTable(
                name: "ImpactMapOptionsForExpertReview");

            migrationBuilder.DropTable(
                name: "ImpactMapRisks");

            migrationBuilder.DropTable(
                name: "ExpertEvaluations");

            migrationBuilder.DropTable(
                name: "ImpactMaps");

            migrationBuilder.DropTable(
                name: "AiAnalysisResults");

            migrationBuilder.DropTable(
                name: "Analyses");
        }
    }
}
