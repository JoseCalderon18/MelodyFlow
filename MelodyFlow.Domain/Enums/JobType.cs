namespace MelodyFlow.Domain.Enums;

/// <summary>Tipos de trabajo que pueden circular por la cola de generación.</summary>
public enum JobType
{
    Music,
    Visual,
    Render,
    Publish
}
