using System.ComponentModel.DataAnnotations;
using MelodyFlow.Application.Visuals;
using MelodyFlow.Application.Media;
using MelodyFlow.Contracts.V1;
using MelodyFlow.Contracts.V1.Media;
using MelodyFlow.Contracts.V1.VisualAssets;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MelodyFlowAPI.Controllers;

/// <summary>Importa clips MP4 manuales y transmite sus archivos desde almacenamiento local.</summary>
[ApiController]
[Route("api/v1/visual-assets")]
public sealed class VisualAssetsController(VisualAssetApplicationService assets) : ControllerBase
{
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(VisualAssetApplicationService.MaxUploadBytes + 1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = VisualAssetApplicationService.MaxUploadBytes + 1_048_576)]
    [ProducesResponseType<VisualAssetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisualAssetResponse>> Import(
        [FromForm] ImportVisualAssetForm request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length <= 0)
        {
            ModelState.AddModelError(nameof(request.File), "Selecciona un clip MP4.");
            return ValidationProblem(ModelState);
        }

        if (request.File.Length > VisualAssetApplicationService.MaxUploadBytes)
        {
            ModelState.AddModelError(nameof(request.File), "El clip no puede superar 512 MB.");
            return ValidationProblem(ModelState);
        }

        try
        {
            await using var content = request.File.OpenReadStream();
            var asset = await assets.ImportVideoAsync(
                request.MusicProjectId,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                content,
                request.ProviderName,
                cancellationToken);
            var response = Map(asset);
            return CreatedAtAction(nameof(GetById), new { id = asset.Id }, response);
        }
        catch (KeyNotFoundException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Music project not found");
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(request.File), exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(request.ProviderName), exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<VisualAssetResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<VisualAssetResponse>>> GetPage(
        [FromQuery] VisualAssetPageQuery query,
        CancellationToken cancellationToken)
    {
        var page = await assets.GetPageAsync(query.Page, query.PageSize, cancellationToken);
        return Ok(new PagedResponse<VisualAssetResponse>(
            page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<VisualAssetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VisualAssetResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(id, cancellationToken);
        return asset is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Visual asset not found")
            : Ok(Map(asset));
    }

    [HttpGet("{id:guid}/content")]
    [Produces("video/mp4")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(id, cancellationToken);
        if (asset is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Visual asset not found");
        }

        var content = await assets.OpenVideoAsync(asset, cancellationToken);
        return content is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Video file not found")
            : File(content, "video/mp4", asset.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<DeleteResourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await assets.DeleteAsync(id, cancellationToken);
        return result switch
        {
            DeleteResourceOutcome.Deleted => NoContent(),
            DeleteResourceOutcome.FileCleanupFailed => Ok(new DeleteResourceResponse(
                true,
                "El clip se eliminó de la biblioteca, pero no se pudo borrar su archivo local.")),
            DeleteResourceOutcome.InUse => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "El clip está en uso",
                detail: "Elimina primero los videoclips que usan este clip."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Visual asset not found")
        };
    }

    private static VisualAssetResponse Map(VisualAsset asset) => new(
        asset.Id,
        asset.MusicProjectId,
        asset.AssetType.ToString(),
        asset.FileName,
        asset.FileSizeBytes,
        asset.DurationMs,
        asset.WidthPx,
        asset.HeightPx,
        asset.ProviderName,
        asset.IsManualUpload,
        asset.CreatedAt,
        $"/api/v1/visual-assets/{asset.Id:D}/content");
}

public sealed class ImportVisualAssetForm
{
    public Guid? MusicProjectId { get; init; }

    [StringLength(VisualAsset.MaxProviderNameLength)]
    public string? ProviderName { get; init; }

    [Required]
    public IFormFile? File { get; init; }
}

public sealed class VisualAssetPageQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page >= 1 && PageSize >= 1 && (long)(Page - 1) * PageSize > int.MaxValue)
        {
            yield return new ValidationResult("The requested page is too large.", [nameof(Page)]);
        }
    }
}
