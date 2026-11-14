using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelodyFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRenderedVideoTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rendered_video_tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rendered_video_id = table.Column<Guid>(type: "uuid", nullable: false),
                    music_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rendered_video_tracks", x => x.id);
                    table.ForeignKey(
                        name: "FK_rendered_video_tracks_music_tracks_music_track_id",
                        column: x => x.music_track_id,
                        principalTable: "music_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rendered_video_tracks_rendered_videos_rendered_video_id",
                        column: x => x.rendered_video_id,
                        principalTable: "rendered_videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Keep every existing single-song render visible in the new ordered playlist model.
            migrationBuilder.Sql(
                "INSERT INTO rendered_video_tracks (id, rendered_video_id, music_track_id, sequence_number) " +
                "SELECT gen_random_uuid(), id, music_track_id, 0 FROM rendered_videos;");

            migrationBuilder.CreateIndex(
                name: "ix_rendered_video_tracks_music_track_id",
                table: "rendered_video_tracks",
                column: "music_track_id");

            migrationBuilder.CreateIndex(
                name: "ux_rendered_video_tracks_video_sequence",
                table: "rendered_video_tracks",
                columns: new[] { "rendered_video_id", "sequence_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rendered_video_tracks");
        }
    }
}
