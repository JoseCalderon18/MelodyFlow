using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MelodyFlow.Infrastructure.Persistence.Configurations;

/// <summary>Mapea estados, leases, reintentos e índices usados para consultar la cola PostgreSQL.</summary>
public sealed class GenerationJobConfiguration : IEntityTypeConfiguration<GenerationJob>
{
    public void Configure(EntityTypeBuilder<GenerationJob> builder)
    {
        // Estas restricciones mantienen coherentes los invariantes que también aplica la entidad de dominio.
        builder.ToTable("generation_jobs", table =>
        {
            table.HasCheckConstraint("ck_generation_jobs_attempt_count", "attempt_count >= 0 AND attempt_count <= max_attempts");
            table.HasCheckConstraint("ck_generation_jobs_max_attempts", "max_attempts BETWEEN 1 AND 3");
            table.HasCheckConstraint("ck_generation_jobs_progress", "progress_percent BETWEEN 0 AND 100");
            table.HasCheckConstraint(
                "ck_generation_jobs_lease_fields",
                "(lease_owner IS NULL AND lease_token IS NULL AND lease_expires_at IS NULL) OR " +
                "(lease_owner IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL " +
                "AND status IN ('Running', 'Cancelling'))");
            table.HasCheckConstraint(
                "ck_generation_jobs_retry_time",
                "status <> 'Retrying' OR next_attempt_at IS NOT NULL");
        });

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(job => job.MusicProjectId).HasColumnName("music_project_id").IsRequired();
        builder.Property(job => job.JobType)
            .HasColumnName("job_type")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(job => job.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(job => job.RequestedParametersJson)
            .HasColumnName("requested_parameters_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(job => job.ProviderName).HasColumnName("provider_name");
        builder.Property(job => job.IdempotencyKey).HasColumnName("idempotency_key");
        builder.Property(job => job.RequestFingerprint)
            .HasColumnName("request_fingerprint")
            .HasMaxLength(64);
        builder.Property(job => job.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(job => job.MaxAttempts).HasColumnName("max_attempts").IsRequired();
        builder.Property(job => job.ProgressPercent).HasColumnName("progress_percent").IsRequired();
        builder.Property(job => job.ErrorCode).HasColumnName("error_code");
        builder.Property(job => job.ErrorMessage).HasColumnName("error_message");
        builder.Property(job => job.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(job => job.StartedAt).HasColumnName("started_at");
        builder.Property(job => job.CompletedAt).HasColumnName("completed_at");
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(job => job.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(job => job.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(GenerationJob.MaxLeaseOwnerLength);
        builder.Property(job => job.LeaseToken).HasColumnName("lease_token");
        builder.Property(job => job.LeaseExpiresAt).HasColumnName("lease_expires_at");

        builder.HasOne<MusicProject>()
            .WithMany()
            .HasForeignKey(job => job.MusicProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(job => new { job.Status, job.CreatedAt })
            .HasDatabaseName("ix_generation_jobs_status_created_at");
        builder.HasIndex(job => new { job.Status, job.NextAttemptAt, job.CreatedAt })
            .HasDatabaseName("ix_generation_jobs_queue_polling");
        builder.HasIndex(job => job.LeaseExpiresAt)
            .HasDatabaseName("ix_generation_jobs_lease_expiry")
            .HasFilter("lease_expires_at IS NOT NULL");
        builder.HasIndex(job => new { job.MusicProjectId, job.JobType, job.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_generation_jobs_project_type_idempotency")
            .HasFilter("idempotency_key IS NOT NULL");
    }
}
