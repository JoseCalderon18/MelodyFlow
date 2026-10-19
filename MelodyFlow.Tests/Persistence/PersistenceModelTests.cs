using MelodyFlow.Domain.Entities;
using MelodyFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MelodyFlow.Tests.Persistence;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelMapsPhaseOneTablesAndPostgresTypes()
    {
        var options = new DbContextOptionsBuilder<MelodyFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=model-only;Password=model-only")
            .Options;

        using var context = new MelodyFlowDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var project = model.FindEntityType(typeof(MusicProject))!;
        var job = model.FindEntityType(typeof(GenerationJob))!;

        Assert.Equal("music_projects", project.GetTableName());
        Assert.Equal("generation_jobs", job.GetTableName());
        Assert.Equal("jsonb", job.FindProperty(nameof(GenerationJob.RequestedParametersJson))!.GetColumnType());
        Assert.Equal("status", job.FindProperty(nameof(GenerationJob.Status))!.GetColumnName());
        Assert.Contains(job.GetIndexes(), index => index.GetDatabaseName() == "ix_generation_jobs_status_created_at");
        Assert.Contains(job.GetIndexes(), index => index.GetDatabaseName() == "ix_generation_jobs_queue_polling");
        Assert.Contains(job.GetIndexes(), index => index.GetDatabaseName() == "ux_generation_jobs_project_type_idempotency");
        Assert.Equal("lease_token", job.FindProperty(nameof(GenerationJob.LeaseToken))!.GetColumnName());
        Assert.Contains(project.GetCheckConstraints(), constraint => constraint.Name == "ck_music_projects_target_duration_positive");
    }
}
