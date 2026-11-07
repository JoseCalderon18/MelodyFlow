using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MelodyFlow.Infrastructure.Persistence.Configurations;

/// <summary>Traduce las reglas de MusicTrack a columnas, restricciones, relaciones e índices PostgreSQL.</summary>
public sealed class MusicTrackConfiguration : IEntityTypeConfiguration<MusicTrack>
{
    public void Configure(EntityTypeBuilder<MusicTrack> builder)
    {
        builder.ToTable("music_tracks", table =>
            table.HasCheckConstraint("ck_music_tracks_duration_positive", "duration_ms IS NULL OR duration_ms > 0"));

        builder.HasKey(track => track.Id);
        builder.Property(track => track.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(track => track.MusicProjectId).HasColumnName("music_project_id");
        builder.Property(track => track.GenerationJobId).HasColumnName("generation_job_id");
        builder.Property(track => track.Title).HasColumnName("title").HasMaxLength(MusicTrack.MaxTitleLength).IsRequired();
        builder.Property(track => track.DurationMs).HasColumnName("duration_ms");
        builder.Property(track => track.StorageKey).HasColumnName("storage_key").HasMaxLength(MusicTrack.MaxStorageKeyLength).IsRequired();
        builder.Property(track => track.Format).HasColumnName("format").HasMaxLength(MusicTrack.MaxFormatLength);
        builder.Property(track => track.ProviderAssetId).HasColumnName("provider_asset_id").HasMaxLength(MusicTrack.MaxProviderAssetIdLength);
        builder.Property(track => track.LicenseEvidenceKey).HasColumnName("license_evidence_key").HasMaxLength(MusicTrack.MaxLicenseEvidenceKeyLength);
        builder.Property(track => track.ArrangementPlanJson)
            .HasColumnName("arrangement_plan_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(track => track.UsedAI).HasColumnName("used_ai").IsRequired();
        builder.Property(track => track.CreatedAt).HasColumnName("created_at").IsRequired();

        // Las referencias de origen se conservan y no se borran en cascada junto con sus filas.
        builder.HasOne<MusicProject>()
            .WithMany()
            .HasForeignKey(track => track.MusicProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GenerationJob>()
            .WithMany()
            .HasForeignKey(track => track.GenerationJobId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un trabajo solo puede producir un registro de audio; los registros sin trabajo siguen permitidos.
        builder.HasIndex(track => track.GenerationJobId)
            .IsUnique()
            .HasDatabaseName("ux_music_tracks_generation_job_id")
            .HasFilter("generation_job_id IS NOT NULL");
        builder.HasIndex(track => new { track.MusicProjectId, track.CreatedAt })
            .HasDatabaseName("ix_music_tracks_project_created_at");
    }
}
