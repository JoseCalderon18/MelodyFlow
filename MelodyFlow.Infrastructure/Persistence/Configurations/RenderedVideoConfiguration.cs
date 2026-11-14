using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MelodyFlow.Infrastructure.Persistence.Configurations;

/// <summary>Mapea vídeos terminados y los IDs de canción y clip que los forman.</summary>
public sealed class RenderedVideoConfiguration : IEntityTypeConfiguration<RenderedVideo>
{
    public void Configure(EntityTypeBuilder<RenderedVideo> builder)
    {
        builder.ToTable("rendered_videos", table =>
            table.HasCheckConstraint("ck_rendered_videos_media_positive", "duration_ms > 0 AND width_px > 0 AND height_px > 0 AND file_size_bytes > 0"));

        builder.HasKey(video => video.Id);
        builder.Property(video => video.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(video => video.MusicProjectId).HasColumnName("music_project_id").IsRequired();
        builder.Property(video => video.RenderJobId).HasColumnName("render_job_id").IsRequired();
        builder.Property(video => video.VisualAssetId).HasColumnName("visual_asset_id").IsRequired();
        builder.Property(video => video.MusicTrackId).HasColumnName("music_track_id").IsRequired();
        builder.Property(video => video.Title).HasColumnName("title").HasMaxLength(RenderedVideo.MaxTitleLength).IsRequired();
        builder.Property(video => video.StorageKey).HasColumnName("storage_key").HasMaxLength(RenderedVideo.MaxStorageKeyLength).IsRequired();
        builder.Property(video => video.DurationMs).HasColumnName("duration_ms").IsRequired();
        builder.Property(video => video.WidthPx).HasColumnName("width_px").IsRequired();
        builder.Property(video => video.HeightPx).HasColumnName("height_px").IsRequired();
        builder.Property(video => video.Codec).HasColumnName("codec").HasMaxLength(RenderedVideo.MaxCodecLength).IsRequired();
        builder.Property(video => video.ContainerFormat).HasColumnName("container_format").HasMaxLength(RenderedVideo.MaxContainerLength).IsRequired();
        builder.Property(video => video.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(video => video.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<MusicProject>().WithMany().HasForeignKey(video => video.MusicProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GenerationJob>().WithMany().HasForeignKey(video => video.RenderJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VisualAsset>().WithMany().HasForeignKey(video => video.VisualAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MusicTrack>().WithMany().HasForeignKey(video => video.MusicTrackId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(video => video.MusicTracks)
            .WithOne()
            .HasForeignKey(track => track.RenderedVideoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(video => video.RenderJobId).IsUnique().HasDatabaseName("ux_rendered_videos_render_job_id");
        builder.HasIndex(video => new { video.MusicProjectId, video.CreatedAt })
            .HasDatabaseName("ix_rendered_videos_project_created_at");
    }
}
