using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MelodyFlow.Infrastructure.Persistence.Configurations;

/// <summary>Mapea archivos visuales subidos y sus metadatos, sin almacenar su contenido.</summary>
public sealed class VisualAssetConfiguration : IEntityTypeConfiguration<VisualAsset>
{
    public void Configure(EntityTypeBuilder<VisualAsset> builder)
    {
        builder.ToTable("visual_assets", table =>
        {
            table.HasCheckConstraint("ck_visual_assets_file_size_positive", "file_size_bytes > 0");
            table.HasCheckConstraint("ck_visual_assets_duration_positive", "duration_ms IS NULL OR duration_ms > 0");
            table.HasCheckConstraint("ck_visual_assets_dimensions_positive", "(width_px IS NULL AND height_px IS NULL) OR (width_px > 0 AND height_px > 0)");
        });

        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(asset => asset.MusicProjectId).HasColumnName("music_project_id");
        builder.Property(asset => asset.GenerationJobId).HasColumnName("generation_job_id");
        builder.Property(asset => asset.AssetType).HasColumnName("asset_type").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(asset => asset.FileName).HasColumnName("file_name").HasMaxLength(VisualAsset.MaxFileNameLength).IsRequired();
        builder.Property(asset => asset.StorageKey).HasColumnName("storage_key").HasMaxLength(VisualAsset.MaxStorageKeyLength).IsRequired();
        builder.Property(asset => asset.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(asset => asset.DurationMs).HasColumnName("duration_ms");
        builder.Property(asset => asset.WidthPx).HasColumnName("width_px");
        builder.Property(asset => asset.HeightPx).HasColumnName("height_px");
        builder.Property(asset => asset.ProviderName).HasColumnName("provider_name").HasMaxLength(VisualAsset.MaxProviderNameLength);
        builder.Property(asset => asset.IsManualUpload).HasColumnName("is_manual_upload").IsRequired();
        builder.Property(asset => asset.RightsEvidenceKey).HasColumnName("rights_evidence_key").HasMaxLength(1_024);
        builder.Property(asset => asset.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<MusicProject>().WithMany().HasForeignKey(asset => asset.MusicProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GenerationJob>().WithMany().HasForeignKey(asset => asset.GenerationJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(asset => new { asset.MusicProjectId, asset.CreatedAt })
            .HasDatabaseName("ix_visual_assets_project_created_at");
    }
}
