using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Elige el modo de composición del trabajo y obtiene el plan que renderizará el Worker.</summary>
internal interface IArrangementPlanProvider
{
    Task<ArrangementPlan> CreatePlanAsync(
        Guid generationJobId,
        MusicProject project,
        string requestedParametersJson,
        CancellationToken cancellationToken);
}

/// <summary>Adaptador opcional que pide una propuesta musical a un modelo local.</summary>
internal interface IAiArrangementPlanner
{
    Task<ArrangementPlan> CreatePlanAsync(
        MusicProject project,
        MusicGenerationRequest request,
        int variationSeed,
        CancellationToken cancellationToken);
}
