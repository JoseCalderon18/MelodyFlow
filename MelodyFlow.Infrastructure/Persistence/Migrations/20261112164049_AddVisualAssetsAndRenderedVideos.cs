using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelodyFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisualAssetsAndRenderedVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "visual_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    music_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generation_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    width_px = table.Column<int>(type: "integer", nullable: true),
                    height_px = table.Column<int>(type: "integer", nullable: true),
                    provider_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_manual_upload = table.Column<bool>(type: "boolean", nullable: false),
                    rights_evidence_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visual_assets", x => x.id);
                    table.CheckConstraint("ck_visual_assets_dimensions_positive", "(width_px IS NULL AND height_px IS NULL) OR (width_px > 0 AND height_px > 0)");
                    table.CheckConstraint("ck_visual_assets_duration_positive", "duration_ms IS NULL OR duration_ms > 0");
                    table.CheckConstraint("ck_visual_assets_file_size_positive", "file_size_bytes > 0");
                    table.ForeignKey(
                        name: "FK_visual_assets_generation_jobs_generation_job_id",
                        column: x => x.generation_job_id,
                        principalTable: "generation_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visual_assets_music_projects_music_project_id",
                        column: x => x.music_project_id,
                        principalTable: "music_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rendered_videos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    music_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    render_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visual_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    music_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    width_px = table.Column<int>(type: "integer", nullable: false),
                    height_px = table.Column<int>(type: "integer", nullable: false),
                    codec = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    container_format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rendered_videos", x => x.id);
                    table.CheckConstraint("ck_rendered_videos_media_positive", "duration_ms > 0 AND width_px > 0 AND height_px > 0 AND file_size_bytes > 0");
                    table.ForeignKey(
                        name: "FK_rendered_videos_generation_jobs_render_job_id",
                        column: x => x.render_job_id,
                        principalTable: "generation_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rendered_videos_music_projects_music_project_id",
                        column: x => x.music_project_id,
                        principalTable: "music_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rendered_videos_music_tracks_music_track_id",
                        column: x => x.music_track_id,
                        principalTable: "music_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rendered_videos_visual_assets_visual_asset_id",
                        column: x => x.visual_asset_id,
                        principalTable: "visual_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rendered_videos_music_track_id",
                table: "rendered_videos",
                column: "music_track_id");

            migrationBuilder.CreateIndex(
                name: "ix_rendered_videos_project_created_at",
                table: "rendered_videos",
                columns: new[] { "music_project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_rendered_videos_visual_asset_id",
                table: "rendered_videos",
                column: "visual_asset_id");

            migrationBuilder.CreateIndex(
                name: "ux_rendered_videos_render_job_id",
                table: "rendered_videos",
                column: "render_job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visual_assets_generation_job_id",
                table: "visual_assets",
                column: "generation_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_visual_assets_project_created_at",
                table: "visual_assets",
                columns: new[] { "music_project_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rendered_videos");

            migrationBuilder.DropTable(
                name: "visual_assets");
        }
    }
}
