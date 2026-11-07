using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelodyFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>Añade metadatos reutilizables de audio; el archivo binario queda en almacenamiento local.</summary>
    public partial class AddGeneratedMusicTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La fila conserva origen, duración, formato, clave del archivo y el plan usado para generarlo.
            migrationBuilder.CreateTable(
                name: "music_tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    music_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generation_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    provider_asset_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    license_evidence_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    arrangement_plan_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_music_tracks", x => x.id);
                    table.CheckConstraint("ck_music_tracks_duration_positive", "duration_ms IS NULL OR duration_ms > 0");
                    table.ForeignKey(
                        name: "FK_music_tracks_generation_jobs_generation_job_id",
                        column: x => x.generation_job_id,
                        principalTable: "generation_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_music_tracks_music_projects_music_project_id",
                        column: x => x.music_project_id,
                        principalTable: "music_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_music_tracks_project_created_at",
                table: "music_tracks",
                columns: new[] { "music_project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_music_tracks_generation_job_id",
                table: "music_tracks",
                column: "generation_job_id",
                unique: true,
                filter: "generation_job_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "music_tracks");
        }
    }
}
