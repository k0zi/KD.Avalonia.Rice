using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Line between two elements of a graph diagram as PlantUML writes it, e.g. <c>&lt;|--</c>, <c>*-up-</c>, <c>.[hidden].&gt;</c>.</summary>
internal readonly partial record struct PlantUmlArrow(
    UmlArrowHead FromHead, UmlArrowHead ToHead, UmlLineStyle Style, UmlLinkDirection Direction, int Length)
{
    public static bool TryParse(string text, int start, out PlantUmlArrow arrow, out int end)
    {
        var m = ArrowRegex().Match(text, start);
        if (!m.Success || m.Index != start)
        {
            arrow = default;
            end = start;
            return false;
        }

        var body = m.Groups["b1"].Value + m.Groups["b2"].Value + m.Groups["b3"].Value + m.Groups["b4"].Value;
        var style = body.Contains('.') ? UmlLineStyle.Dashed : UmlLineStyle.Solid;
        foreach (var option in (m.Groups["o1"].Value + "," + m.Groups["o2"].Value).Split(',', StringSplitOptions.TrimEntries))
        {
            style = option.ToLowerInvariant() switch
            {
                "hidden" => UmlLineStyle.Hidden,
                "dashed" => UmlLineStyle.Dashed,
                "dotted" => UmlLineStyle.Dotted,
                "bold" => UmlLineStyle.Bold,
                _ => style,
            };
        }

        var direction = m.Groups["dir"].Value.ToLowerInvariant() switch
        {
            "up" or "u" => UmlLinkDirection.Up,
            "down" or "do" or "d" => UmlLinkDirection.Down,
            "left" or "le" or "l" => UmlLinkDirection.Left,
            "right" or "ri" or "r" => UmlLinkDirection.Right,
            _ => UmlLinkDirection.Auto,
        };

        arrow = new PlantUmlArrow(ParseHead(m.Groups["l"].Value), ParseHead(m.Groups["r"].Value), style, direction,
            Math.Max(1, body.Length));
        end = m.Index + m.Length;
        return true;
    }

    public static string Format(UmlRelation relation)
    {
        var sb = new StringBuilder();
        sb.Append(relation.FromHead switch
        {
            UmlArrowHead.Arrow => "<",
            UmlArrowHead.Triangle or UmlArrowHead.FilledTriangle => "<|",
            UmlArrowHead.Diamond => "o",
            UmlArrowHead.FilledDiamond => "*",
            UmlArrowHead.Plus => "+",
            UmlArrowHead.Cross => "x",
            UmlArrowHead.Square => "#",
            UmlArrowHead.Circle => "0",
            UmlArrowHead.Socket => "(",
            _ => "",
        });

        var c = relation.LineStyle == UmlLineStyle.Dashed ? '.' : '-';
        var option = relation.LineStyle switch
        {
            UmlLineStyle.Hidden => "[hidden]",
            UmlLineStyle.Dotted => "[dotted]",
            UmlLineStyle.Bold => "[bold]",
            _ => "",
        };
        var direction = relation.Direction;
        var length = Math.Max(1, relation.Length);
        // "-[hidden]" alone would not parse back: a horizontal line with an option says so explicitly.
        if (length == 1 && option.Length > 0 && direction == UmlLinkDirection.Auto)
            direction = UmlLinkDirection.Right;

        sb.Append(c).Append(option);
        if (direction != UmlLinkDirection.Auto)
        {
            sb.Append(direction.ToString().ToLowerInvariant());
            sb.Append(c, Math.Max(1, length - 1));
        }
        else
            sb.Append(c, length - 1);

        sb.Append(relation.ToHead switch
        {
            UmlArrowHead.Arrow => ">",
            UmlArrowHead.Triangle or UmlArrowHead.FilledTriangle => "|>",
            UmlArrowHead.Diamond => "o",
            UmlArrowHead.FilledDiamond => "*",
            UmlArrowHead.Plus => "+",
            UmlArrowHead.Cross => "x",
            UmlArrowHead.Square => "#",
            UmlArrowHead.Circle => "0",
            UmlArrowHead.Socket => ")",
            _ => "",
        });
        return sb.ToString();
    }

    private static UmlArrowHead ParseHead(string token) => token switch
    {
        "<" or ">" => UmlArrowHead.Arrow,
        "<|" or "|>" or "^" => UmlArrowHead.Triangle,
        "o" => UmlArrowHead.Diamond,
        "*" => UmlArrowHead.FilledDiamond,
        "+" => UmlArrowHead.Plus,
        "x" => UmlArrowHead.Cross,
        "#" => UmlArrowHead.Square,
        "0" => UmlArrowHead.Circle,
        "(" or ")" => UmlArrowHead.Socket,
        _ => UmlArrowHead.None,
    };

    // l: left head, b1..b4: line characters, o1/o2: [options], dir: direction word, r: right head.
    // The letter heads (o, x) must not run into a following name ("A --obj" is not "A --o bj").
    [GeneratedRegex(@"\G(?:(?<l><\||\^|<|\*|\+|\#|0|\()|(?<l>[ox])(?=[-.]))?(?<b1>[-.]+)(?:\[(?<o1>[^\]]*)\])?(?<b2>[-.]*)(?:(?<dir>up|down|left|right|do|le|ri|u|d|l|r)(?<b3>[-.]+))?(?:\[(?<o2>[^\]]*)\])?(?<b4>[-.]*)(?:(?<r>\|>|\^|>|\*|\+|\#|0|\))|(?<r>[ox])(?![\p{L}\p{N}_]))?",
        RegexOptions.IgnoreCase)]
    private static partial Regex ArrowRegex();
}
