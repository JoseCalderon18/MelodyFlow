namespace MelodyFlow.Application.Music;

/// <summary>
/// Error controlado del flujo; su mensaje puede guardarse en el trabajo sin exponer datos sensibles.
/// </summary>
public sealed class MusicGenerationException(
    string errorCode,
    string safeMessage,
    bool isTransient,
    Exception? innerException = null) : Exception(safeMessage, innerException)
{
    /// <summary>Código estable que permite clasificar el fallo sin guardar detalles internos.</summary>
    public string ErrorCode { get; } = errorCode;

    /// <summary>Texto seguro para registrar en el trabajo y mostrar en respuestas controladas.</summary>
    public string SafeMessage { get; } = safeMessage;

    /// <summary>Indica si el Worker puede volver a intentarlo según su política de reintentos.</summary>
    public bool IsTransient { get; } = isTransient;
}
