using System.ComponentModel.DataAnnotations;

namespace MelodyFlow.Contracts.Validation;

/// <summary>Complementa Required para rechazar cadenas vacías o formadas solo por espacios.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string text && !string.IsNullOrWhiteSpace(text);
}
