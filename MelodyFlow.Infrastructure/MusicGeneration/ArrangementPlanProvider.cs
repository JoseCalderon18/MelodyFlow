using MelodyFlow.Application.Music;
using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Usa C# por defecto y llama al modelo local solo cuando el trabajo lo solicita.</summary>
internal sealed class ArrangementPlanProvider(
    IAiArrangementPlanner aiPlanner) : IArrangementPlanProvider
{
    public async Task<ArrangementPlan> CreatePlanAsync(
        Guid generationJobId,
        MusicProject project,
        string requestedParametersJson,
        CancellationToken cancellationToken)
    {
        var request = MusicGenerationRequest.Parse(requestedParametersJson);
        var seed = request.GetVariationSeed(generationJobId);
        if (request.UseAI)
        {
            return await aiPlanner.CreatePlanAsync(project, request, seed, cancellationToken);
        }

        return DeterministicArrangementPlanner.Create(project, request, seed);
    }
}
