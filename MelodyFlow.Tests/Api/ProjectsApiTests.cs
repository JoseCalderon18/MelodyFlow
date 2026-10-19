using System.Net;
using System.Net.Http.Json;
using MelodyFlow.Application.Jobs;
using MelodyFlow.Application.Projects;
using MelodyFlow.Contracts.V1;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Contracts.V1.Projects;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MelodyFlow.Tests.Api;

public sealed class ProjectsApiTests : IClassFixture<MelodyFlowApiFactory>
{
    private readonly HttpClient _client;

    public ProjectsApiTests(MelodyFlowApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task CreateAndFetchProjectUsesVersionedHttpContract()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Night Drive",
            genre = "Lo-fi",
            mood = "Calm",
            targetDurationSeconds = 3600
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/v1/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectDto>();

        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("Night Drive", fetched.Title);
    }

    [Fact]
    public async Task InvalidProjectRequestReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "   ",
            genre = "Lo-fi",
            targetDurationSeconds = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task MissingProjectReturnsProblemDetails()
    {
        var response = await _client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ListProjectsReturnsPageMetadata()
    {
        var response = await _client.GetAsync("/api/v1/projects?page=1&pageSize=20");
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProjectResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task ConfiguredLocalOriginPassesCorsPreflight()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/projects");
        request.Headers.Add("Origin", "http://localhost:5073");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5073", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task PageOffsetOverflowReturnsValidationProblem()
    {
        var response = await _client.GetAsync("/api/v1/projects?page=2147483647&pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GenerationJobIsDurablyAcceptedAndIdempotentRequestsReuseTheSameJob()
    {
        var projectId = await CreateProjectAsync();

        using var firstRequest = CreateJobRequest(projectId, "{\"tempoBpm\":80,\"mood\":\"calm\"}");
        var firstResponse = await _client.SendAsync(firstRequest);
        var firstJob = await firstResponse.Content.ReadFromJsonAsync<GenerationJobResponse>();

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.NotNull(firstJob);
        Assert.Equal("Queued", firstJob.Status);
        Assert.Equal(0, firstJob.AttemptCount);
        Assert.NotNull(firstResponse.Headers.Location);

        using var equivalentRequest = CreateJobRequest(projectId, "{\"mood\":\"calm\",\"tempoBpm\":80}");
        var replayResponse = await _client.SendAsync(equivalentRequest);
        var replayedJob = await replayResponse.Content.ReadFromJsonAsync<GenerationJobResponse>();

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(firstJob.Id, replayedJob!.Id);
    }

    [Fact]
    public async Task ReusingIdempotencyKeyWithChangedParametersReturnsConflict()
    {
        var projectId = await CreateProjectAsync();

        using var originalRequest = CreateJobRequest(projectId, "{\"tempoBpm\":80}");
        using var changedRequest = CreateJobRequest(projectId, "{\"tempoBpm\":90}");
        _ = await _client.SendAsync(originalRequest);

        var response = await _client.SendAsync(changedRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task CancellingQueuedGenerationJobChangesItsStatusImmediately()
    {
        var projectId = await CreateProjectAsync();
        using var request = CreateJobRequest(projectId, "{}");
        var createResponse = await _client.SendAsync(request);
        var job = await createResponse.Content.ReadFromJsonAsync<GenerationJobResponse>();

        var cancellationResponse = await _client.PostAsync(
            $"/api/v1/generation-jobs/{job!.Id}/cancellation",
            content: null);
        var cancelledJob = await cancellationResponse.Content.ReadFromJsonAsync<GenerationJobResponse>();

        Assert.Equal(HttpStatusCode.OK, cancellationResponse.StatusCode);
        Assert.Equal("Cancelled", cancelledJob!.Status);
        Assert.NotNull(cancelledJob.CompletedAt);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Queue Test",
            genre = "Lo-fi",
            targetDurationSeconds = 300
        });
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return project!.Id;
    }

    private static HttpRequestMessage CreateJobRequest(Guid projectId, string requestedParametersJson)
    {
        using var parameters = System.Text.Json.JsonDocument.Parse(requestedParametersJson);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/generation-jobs")
        {
            Content = JsonContent.Create(new CreateGenerationJobRequest
            {
                MusicProjectId = projectId,
                JobType = "Music",
                RequestedParameters = parameters.RootElement.Clone()
            })
        };
        request.Headers.Add("Idempotency-Key", "n8n-daily-lofi-2026-10-18");
        return request;
    }

    private sealed record ProjectDto(Guid Id, string Title);
}

public sealed class MelodyFlowApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:MelodyFlow",
            "Host=localhost;Port=5433;Database=test;Username=test;Password=test");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5073");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:MelodyFlow"] = "Host=localhost;Port=5433;Database=test;Username=test;Password=test"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMusicProjectRepository>();
            services.AddSingleton<IMusicProjectRepository>(new InMemoryMusicProjectRepository());
            services.RemoveAll<IGenerationJobRepository>();
            services.AddSingleton<IGenerationJobRepository, InMemoryGenerationJobRepository>();
        });
    }

    private sealed class InMemoryMusicProjectRepository : IMusicProjectRepository
    {
        private readonly List<MusicProject> _projects = [];

        public Task AddAsync(MusicProject project, CancellationToken cancellationToken)
        {
            _projects.Add(project);
            return Task.CompletedTask;
        }

        public Task<MusicProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_projects.SingleOrDefault(project => project.Id == id));

        public Task<ProjectPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            var items = _projects
                .OrderByDescending(project => project.CreatedAt)
                .ThenBy(project => project.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();
            return Task.FromResult(new ProjectPage(items, page, pageSize, _projects.Count));
        }
    }

    private sealed class InMemoryGenerationJobRepository : IGenerationJobRepository
    {
        private readonly List<GenerationJob> _jobs = [];

        public Task<EnqueueGenerationJobResult> EnqueueOrGetAsync(
            GenerationJob job,
            CancellationToken cancellationToken)
        {
            var existing = job.IdempotencyKey is null
                ? null
                : _jobs.SingleOrDefault(candidate =>
                    candidate.MusicProjectId == job.MusicProjectId &&
                    candidate.JobType == job.JobType &&
                    candidate.IdempotencyKey == job.IdempotencyKey);

            if (existing is not null)
            {
                var outcome = existing.RequestFingerprint == job.RequestFingerprint
                    ? EnqueueGenerationJobOutcome.ExistingRequest
                    : EnqueueGenerationJobOutcome.IdempotencyConflict;
                return Task.FromResult(new EnqueueGenerationJobResult(existing, outcome));
            }

            _jobs.Add(job);
            return Task.FromResult(new EnqueueGenerationJobResult(job, EnqueueGenerationJobOutcome.Created));
        }

        public Task<GenerationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_jobs.SingleOrDefault(job => job.Id == id));

        public Task<CancellationGenerationJobResult> RequestCancellationAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var job = _jobs.SingleOrDefault(candidate => candidate.Id == id);
            if (job is null)
            {
                return Task.FromResult(new CancellationGenerationJobResult(null, CancellationGenerationJobOutcome.NotFound));
            }

            if (job.Status == JobStatus.Cancelled)
            {
                return Task.FromResult(new CancellationGenerationJobResult(job, CancellationGenerationJobOutcome.AlreadyCancelled));
            }

            if (job.Status == JobStatus.Cancelling)
            {
                return Task.FromResult(new CancellationGenerationJobResult(job, CancellationGenerationJobOutcome.AlreadyCancelling));
            }

            if (job.Status is JobStatus.Completed or JobStatus.Failed)
            {
                return Task.FromResult(new CancellationGenerationJobResult(job, CancellationGenerationJobOutcome.CannotCancelTerminalJob));
            }

            var wasRunning = job.Status == JobStatus.Running;
            job.RequestCancellation();
            return Task.FromResult(new CancellationGenerationJobResult(
                job,
                wasRunning
                    ? CancellationGenerationJobOutcome.CancellationRequested
                    : CancellationGenerationJobOutcome.CancelledImmediately));
        }

        public Task<ClaimedGenerationJob?> ClaimNextAsync(
            string workerId,
            IReadOnlyCollection<JobType> supportedJobTypes,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            Task.FromResult<ClaimedGenerationJob?>(null);

        public Task<LeaseRenewalStatus> RenewLeaseAsync(
            Guid jobId,
            Guid leaseToken,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            Task.FromResult(LeaseRenewalStatus.LeaseLost);

        public Task<bool> ReportProgressAsync(Guid jobId, Guid leaseToken, int progressPercent, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> CompleteAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<FailureHandlingResult> RecordFailureAsync(
            Guid jobId,
            Guid leaseToken,
            string errorCode,
            string safeErrorMessage,
            bool isTransient,
            TimeSpan retryDelay,
            CancellationToken cancellationToken) =>
            Task.FromResult(FailureHandlingResult.LeaseLost);

        public Task<bool> MarkCancelledAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}

public sealed class RateLimitApiTests
{
    [Fact]
    public async Task RequestsOverGlobalLimitReturnProblemDetails()
    {
        using var factory = new MelodyFlowApiFactory();
        using var client = factory.CreateClient();

        for (var requestNumber = 1; requestNumber <= 120; requestNumber++)
        {
            using var accepted = await client.GetAsync("/api/v1/projects?page=1&pageSize=1");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, accepted.StatusCode);
        }

        using var rejected = await client.GetAsync("/api/v1/projects?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("application/problem+json", rejected.Content.Headers.ContentType!.MediaType);
    }
}
