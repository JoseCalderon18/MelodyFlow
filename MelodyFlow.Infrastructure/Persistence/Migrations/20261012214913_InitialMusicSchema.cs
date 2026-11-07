using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelodyFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>Crea las tablas iniciales de proyectos musicales y trabajos de generación.</summary>
    public partial class InitialMusicSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los proyectos guardan el brief; los trabajos guardan solicitudes y estados, no archivos multimedia.
            migrationBuilder.CreateTable(
                name: "music_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    genre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    mood = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    target_duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_music_projects", x => x.id);
                    table.CheckConstraint("ck_music_projects_target_duration_positive", "target_duration_seconds > 0");
                });

            migrationBuilder.CreateTable(
                name: "generation_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    music_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    requested_parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    provider_name = table.Column<string>(type: "text", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    progress_percent = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_jobs", x => x.id);
                    table.CheckConstraint("ck_generation_jobs_attempt_count", "attempt_count >= 0 AND attempt_count <= max_attempts");
                    table.CheckConstraint("ck_generation_jobs_max_attempts", "max_attempts BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_generation_jobs_progress", "progress_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_generation_jobs_music_projects_music_project_id",
                        column: x => x.music_project_id,
                        principalTable: "music_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generation_jobs_music_project_id",
                table: "generation_jobs",
                column: "music_project_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_jobs_status_created_at",
                table: "generation_jobs",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generation_jobs");

            migrationBuilder.DropTable(
                name: "music_projects");
        }
    }
}
