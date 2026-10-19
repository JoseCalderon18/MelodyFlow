using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MelodyFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostgresGenerationJobQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_generation_jobs_music_project_id",
                table: "generation_jobs");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                table: "generation_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_owner",
                table: "generation_jobs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                table: "generation_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "generation_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_fingerprint",
                table: "generation_jobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Existing retry rows predate scheduled retries; make them eligible immediately after migration.
            migrationBuilder.Sql(
                "UPDATE generation_jobs " +
                "SET next_attempt_at = CURRENT_TIMESTAMP " +
                "WHERE status = 'Retrying' AND next_attempt_at IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_generation_jobs_lease_expiry",
                table: "generation_jobs",
                column: "lease_expires_at",
                filter: "lease_expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_generation_jobs_queue_polling",
                table: "generation_jobs",
                columns: new[] { "status", "next_attempt_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_generation_jobs_project_type_idempotency",
                table: "generation_jobs",
                columns: new[] { "music_project_id", "job_type", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_generation_jobs_lease_fields",
                table: "generation_jobs",
                sql: "(lease_owner IS NULL AND lease_token IS NULL AND lease_expires_at IS NULL) OR (lease_owner IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL AND status IN ('Running', 'Cancelling'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_generation_jobs_retry_time",
                table: "generation_jobs",
                sql: "status <> 'Retrying' OR next_attempt_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_generation_jobs_lease_expiry",
                table: "generation_jobs");

            migrationBuilder.DropIndex(
                name: "ix_generation_jobs_queue_polling",
                table: "generation_jobs");

            migrationBuilder.DropIndex(
                name: "ux_generation_jobs_project_type_idempotency",
                table: "generation_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_generation_jobs_lease_fields",
                table: "generation_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_generation_jobs_retry_time",
                table: "generation_jobs");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "generation_jobs");

            migrationBuilder.DropColumn(
                name: "lease_owner",
                table: "generation_jobs");

            migrationBuilder.DropColumn(
                name: "lease_token",
                table: "generation_jobs");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "generation_jobs");

            migrationBuilder.DropColumn(
                name: "request_fingerprint",
                table: "generation_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_generation_jobs_music_project_id",
                table: "generation_jobs",
                column: "music_project_id");
        }
    }
}
