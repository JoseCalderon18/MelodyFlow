using System.ComponentModel.DataAnnotations;
using MelodyFlow.Application.Projects;
using MelodyFlow.Contracts.V1;
using MelodyFlow.Contracts.V1.Projects;
using MelodyFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MelodyFlowAPI.Controllers;

/// <summary>Endpoints HTTP para crear y consultar proyectos musicales.</summary>
[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController(MusicProjectApplicationService projects) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateMusicProjectRequest request,
        CancellationToken cancellationToken)
    {
        // El caso de uso crea la entidad; el controlador adapta la entrada y la respuesta HTTP.
        var project = await projects.CreateAsync(
            request.Title,
            request.Genre,
            request.Mood,
            request.TargetDurationSeconds,
            cancellationToken);
        var response = Map(project);

        return CreatedAtRoute("GetMusicProjectById", new { id = project.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<ProjectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProjectResponse>>> GetPage(
        [FromQuery] ProjectPageQuery query,
        CancellationToken cancellationToken)
    {
        // La respuesta incluye el total para que el cliente pueda construir la navegación del listado.
        var page = await projects.GetPageAsync(query.Page, query.PageSize, cancellationToken);
        var response = new PagedResponse<ProjectResponse>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);

        return Ok(response);
    }

    [HttpGet("{id:guid}", Name = "GetMusicProjectById")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(id, cancellationToken);
        return project is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found")
            : Ok(Map(project));
    }

    private static ProjectResponse Map(MusicProject project) => new(
        project.Id,
        project.Title,
        project.Genre,
        project.Mood,
        project.TargetDurationSeconds,
        project.Status.ToString(),
        project.CreatedAt,
        project.UpdatedAt);
}

/// <summary>Parámetros del listado y validación de desbordamiento del offset.</summary>
public sealed class ProjectPageQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page >= 1 && PageSize >= 1 && (long)(Page - 1) * PageSize > int.MaxValue)
        {
            yield return new ValidationResult(
                "The requested page is too large.",
                [nameof(Page)]);
        }
    }
}
