namespace MelodyFlow.Infrastructure.MusicGeneration;

internal enum ArrangementSectionType
{
    Intro,
    Theme,
    Variation,
    Break,
    Return,
    Outro
}

internal sealed record ArrangementSection(
    string Name,
    ArrangementSectionType Type,
    long StartBar,
    long EndBar,
    double Energy);

/// <summary>Creates a short-form roadmap so motifs and layers evolve instead of looping unchanged.</summary>
internal static class ArrangementStructure
{
    private sealed record SectionTemplate(string Name, ArrangementSectionType Type, double EndFraction, double Energy);

    private static readonly IReadOnlyDictionary<string, SectionTemplate[]> Forms =
        new Dictionary<string, SectionTemplate[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["evolving-arc"] =
            [
                new("Intro", ArrangementSectionType.Intro, 0.12, 0.42),
                new("Tema", ArrangementSectionType.Theme, 0.34, 0.70),
                new("Variación", ArrangementSectionType.Variation, 0.58, 0.88),
                new("Respiro", ArrangementSectionType.Break, 0.73, 0.48),
                new("Regreso", ArrangementSectionType.Return, 0.92, 0.94),
                new("Outro", ArrangementSectionType.Outro, 1.00, 0.42)
            ],
            ["aaba"] =
            [
                new("Intro", ArrangementSectionType.Intro, 0.10, 0.45),
                new("A", ArrangementSectionType.Theme, 0.32, 0.72),
                new("A variada", ArrangementSectionType.Theme, 0.54, 0.78),
                new("B", ArrangementSectionType.Variation, 0.76, 0.94),
                new("A regreso", ArrangementSectionType.Return, 0.93, 0.88),
                new("Outro", ArrangementSectionType.Outro, 1.00, 0.42)
            ],
            ["build-break-return"] =
            [
                new("Intro", ArrangementSectionType.Intro, 0.10, 0.42),
                new("Construcción", ArrangementSectionType.Theme, 0.32, 0.72),
                new("Groove", ArrangementSectionType.Variation, 0.57, 1.00),
                new("Break", ArrangementSectionType.Break, 0.70, 0.42),
                new("Regreso", ArrangementSectionType.Return, 0.93, 0.96),
                new("Outro", ArrangementSectionType.Outro, 1.00, 0.42)
            ],
            ["cinematic-arc"] =
            [
                new("Apertura", ArrangementSectionType.Intro, 0.16, 0.38),
                new("Motivo", ArrangementSectionType.Theme, 0.34, 0.64),
                new("Desarrollo", ArrangementSectionType.Variation, 0.62, 0.86),
                new("Cima", ArrangementSectionType.Return, 0.78, 1.00),
                new("Resolución", ArrangementSectionType.Break, 0.93, 0.62),
                new("Cierre", ArrangementSectionType.Outro, 1.00, 0.38)
            ],
            ["steady-groove"] =
            [
                new("Entrada", ArrangementSectionType.Intro, 0.08, 0.50),
                new("Groove", ArrangementSectionType.Theme, 0.66, 0.76),
                new("Variación", ArrangementSectionType.Variation, 0.92, 0.88),
                new("Salida", ArrangementSectionType.Outro, 1.00, 0.48)
            ]
        };

    public static IReadOnlyList<ArrangementSection> Create(long barCount, string form)
    {
        if (barCount <= 0)
        {
            return [];
        }

        if (barCount < 12)
        {
            return CreateShortForm(barCount);
        }

        if (!Forms.TryGetValue(form, out var template))
        {
            template = Forms["evolving-arc"];
        }

        var result = new List<ArrangementSection>();
        long startBar = 0;
        foreach (var item in template)
        {
            var proposedEnd = item.EndFraction >= 1d
                ? barCount
                : (long)Math.Floor(barCount * item.EndFraction);
            var endBar = Math.Clamp(proposedEnd, startBar + 1, barCount);
            if (endBar <= startBar)
            {
                continue;
            }

            result.Add(new ArrangementSection(item.Name, item.Type, startBar, endBar, item.Energy));
            startBar = endBar;
            if (startBar == barCount)
            {
                break;
            }
        }

        if (result.Count == 0)
        {
            result.Add(new ArrangementSection("Tema", ArrangementSectionType.Theme, 0, barCount, 0.7));
        }
        else if (result[^1].EndBar < barCount)
        {
            var last = result[^1];
            result[^1] = last with { EndBar = barCount };
        }

        return result;
    }

    private static IReadOnlyList<ArrangementSection> CreateShortForm(long barCount)
    {
        if (barCount == 1)
        {
            return [new ArrangementSection("Tema", ArrangementSectionType.Theme, 0, 1, 0.72)];
        }

        if (barCount == 2)
        {
            return
            [
                new ArrangementSection("Tema", ArrangementSectionType.Theme, 0, 1, 0.72),
                new ArrangementSection("Cierre", ArrangementSectionType.Outro, 1, 2, 0.42)
            ];
        }

        if (barCount == 3)
        {
            return
            [
                new ArrangementSection("Entrada", ArrangementSectionType.Intro, 0, 1, 0.45),
                new ArrangementSection("Tema", ArrangementSectionType.Theme, 1, 2, 0.76),
                new ArrangementSection("Cierre", ArrangementSectionType.Outro, 2, 3, 0.42)
            ];
        }

        var introBars = 1L;
        var remaining = barCount - introBars;
        var themeBars = Math.Max(1, remaining / 2);
        var variationBars = Math.Max(1, remaining / 4);
        var returnBars = remaining - themeBars - variationBars;
        if (returnBars < 1)
        {
            variationBars--;
            returnBars = 1;
        }

        var sections = new List<ArrangementSection>();
        var start = 0L;
        sections.Add(new ArrangementSection("Entrada", ArrangementSectionType.Intro, start, ++start, 0.45));
        sections.Add(new ArrangementSection("Tema", ArrangementSectionType.Theme, start, start += themeBars, 0.76));
        sections.Add(new ArrangementSection("Variación", ArrangementSectionType.Variation, start, start += variationBars, 0.92));
        sections.Add(new ArrangementSection("Regreso", ArrangementSectionType.Return, start, start + returnBars, 0.96));
        return sections;
    }

    public static ArrangementSection At(IReadOnlyList<ArrangementSection> sections, long bar) =>
        sections.FirstOrDefault(section => bar >= section.StartBar && bar < section.EndBar)
        ?? sections[^1];

    public static int ChordOffset(ArrangementSection section) => section.Type switch
    {
        ArrangementSectionType.Variation => 4,
        ArrangementSectionType.Return => 0,
        _ => 0
    };

    public static double DensityMultiplier(string density) => density switch
    {
        "sparse" => 0.74,
        "layered" => 1.12,
        _ => 1.0
    };
}
