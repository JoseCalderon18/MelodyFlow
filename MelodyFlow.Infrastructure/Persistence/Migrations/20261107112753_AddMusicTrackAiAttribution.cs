using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelodyFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicTrackAiAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "used_ai",
                table: "music_tracks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Los planes anteriores se generaban siempre con LM Studio; conserva esa procedencia.
            migrationBuilder.Sql(
                """
                UPDATE music_tracks
                SET used_ai = CASE
                    WHEN jsonb_typeof(arrangement_plan_json -> 'usedAI') = 'boolean'
                        THEN (arrangement_plan_json ->> 'usedAI')::boolean
                    ELSE TRUE
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "used_ai",
                table: "music_tracks");
        }
    }
}
