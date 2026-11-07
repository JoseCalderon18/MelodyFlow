namespace MelodyFlow.Contracts.V1;

/// <summary>Sobre común para devolver resultados paginados y sus datos de navegación.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
