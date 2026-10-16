using System.Globalization;
using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Lays out a <see cref="UmlTimingDiagram"/>: one row per lifeline over a shared, scaled time axis.</summary>
internal sealed class TimingSceneBuilder(UmlTimingDiagram diagram, UmlStyle style)
{
    private const double LevelHeight = 24;
    private const double RowGap = 14;

    public void Build(UmlScene scene, Point origin)
    {
        if (diagram.Lifelines.Count == 0)
            return;

        var times = diagram.Changes.Select(c => c.Time)
            .Concat(diagram.Messages.SelectMany(m => new[] { m.FromTime, m.ToTime }))
            .Distinct().OrderBy(t => t).ToList();
        if (times.Count == 0)
            times.Add(0);
        var start = Math.Min(0, times[0]);
        var span = Math.Max(1, times[^1] - start);
        var end = times[^1] + Math.Max(span * 0.15, span / Math.Max(1, times.Count));

        var nameWidth = diagram.Lifelines.Max(l => style.Measure(l.Name, bold: true).Width) + 24;
        var statesWidth = diagram.Lifelines.Where(l => l.Kind == UmlTimingLifelineKind.Robust)
            .SelectMany(StatesOf).Select(s => style.Measure(s, small: true).Width + 12).DefaultIfEmpty(0).Max();

        // Pixels per time unit: every concise segment fits its state name, every clock half period 12 px.
        var scale = 640 / (end - start);
        foreach (var lifeline in diagram.Lifelines)
        {
            var changes = ChangesOf(lifeline);
            for (var i = 0; i < changes.Count; i++)
            {
                var segmentEnd = i + 1 < changes.Count ? changes[i + 1].Time : end;
                var duration = segmentEnd - changes[i].Time;
                if (duration > 0 && lifeline.Kind == UmlTimingLifelineKind.Concise)
                    scale = Math.Max(scale, (style.Measure(changes[i].State, small: true).Width + 20) / duration);
            }
            if (lifeline.Kind == UmlTimingLifelineKind.Clock && lifeline.ClockPeriod > 0)
                scale = Math.Max(scale, 24 / lifeline.ClockPeriod);
        }
        foreach (var pair in times.Zip(times.Skip(1)))
            scale = Math.Max(scale, (style.Measure(Format(pair.Second), small: true).Width + 12) / Math.Max(1e-9, pair.Second - pair.First));
        scale = Math.Min(scale, 4000 / (end - start));

        var plotLeft = origin.X + nameWidth + statesWidth;
        var plotRight = plotLeft + (end - start) * scale;
        double X(double time) => plotLeft + (time - start) * scale;

        var rows = new Dictionary<string, (double Top, double Height, List<string> States)>();
        var y = origin.Y;
        foreach (var lifeline in diagram.Lifelines)
        {
            var states = StatesOf(lifeline);
            var height = lifeline.Kind switch
            {
                UmlTimingLifelineKind.Robust => Math.Max(2, states.Count) * LevelHeight + 12,
                UmlTimingLifelineKind.Concise => 40,
                _ => 34,
            };
            rows[lifeline.Id] = (y, height, states);
            y += height + RowGap;
        }
        var bottom = y - RowGap;

        // Background: lanes, guides at every change time.
        var index = 0;
        foreach (var lifeline in diagram.Lifelines)
        {
            var (top, height, _) = rows[lifeline.Id];
            scene.Add(new ShapePrim
            {
                Shape = UmlShape.RoundedRectangle,
                Rect = new Rect(origin.X, top - RowGap / 2, plotRight - origin.X + 12, height + RowGap),
                Radius = 6,
                Fill = index++ % 2 == 0 ? UmlBrush.Lane : UmlBrush.None,
                Stroke = UmlBrush.None,
            });
        }
        foreach (var time in times)
            scene.Add(new LinePrim { Points = [new Point(X(time), origin.Y - 4), new Point(X(time), bottom + 4)], Brush = UmlBrush.Border, Style = UmlStroke.Dotted, Thickness = 1 });

        foreach (var lifeline in diagram.Lifelines)
        {
            var (top, height, states) = rows[lifeline.Id];
            var item = scene.AddItem(new UmlHitItem(lifeline, new Rect(origin.X, top, plotRight - origin.X, height)) { CornerRadius = 6 });
            var name = style.Measure(lifeline.Name, bold: true);
            scene.Add(style.Text(lifeline.Name, new Point(origin.X + 8, top + (height - name.Height) / 2), bold: true), item);

            var changes = ChangesOf(lifeline);
            switch (lifeline.Kind)
            {
                case UmlTimingLifelineKind.Robust:
                    PaintRobust(scene, lifeline, changes, states, top, plotLeft, X, end, item);
                    break;
                case UmlTimingLifelineKind.Concise:
                    PaintConcise(scene, changes, top, height, X, end, item);
                    break;
                case UmlTimingLifelineKind.Clock:
                    PaintClock(scene, lifeline, top, height, X, start, end, item);
                    break;
                case UmlTimingLifelineKind.Binary:
                    PaintBinary(scene, changes, top, height, X, end, item);
                    break;
            }
        }

        // Time axis.
        var axisY = bottom + 14;
        scene.Add(new LinePrim { Points = [new Point(plotLeft, axisY), new Point(plotRight, axisY)], Brush = UmlBrush.Line, Thickness = 1.25 });
        scene.Add(new HeadPrim { Tip = new Point(plotRight + 6, axisY), Direction = new Vector(1, 0), Head = UmlArrowHead.Arrow });
        foreach (var time in times)
        {
            scene.Add(new LinePrim { Points = [new Point(X(time), axisY - 4), new Point(X(time), axisY + 4)], Brush = UmlBrush.Line, Thickness = 1.25 });
            scene.Add(style.CenteredText(Format(time), X(time), axisY + 6, small: true, brush: UmlBrush.MutedText));
        }

        foreach (var message in diagram.Messages)
        {
            if (!rows.TryGetValue(message.From, out var fromRow) || !rows.TryGetValue(message.To, out var toRow))
                continue;
            var a = new Point(X(message.FromTime), fromRow.Top + fromRow.Height / 2);
            var b = new Point(X(message.ToTime), toRow.Top + toRow.Height / 2);
            var item = scene.AddItem(new UmlHitItem(message, UmlGeometry.BoundsOf([a, b]).Inflate(6)) { Path = [a, b] });
            var painter = new UmlEdgePainter(style);
            painter.Paint(scene, new UmlEdgePainter.Edge { Points = [a, b], ToHead = UmlArrowHead.Arrow, Label = message.Text }, item);
        }
    }

    private List<UmlTimingChange> ChangesOf(UmlTimingLifeline lifeline) =>
        diagram.Changes.Where(c => c.Lifeline == lifeline.Id).OrderBy(c => c.Time).ToList();

    private List<string> StatesOf(UmlTimingLifeline lifeline)
    {
        var states = lifeline.States.ToList();
        foreach (var change in ChangesOf(lifeline))
            if (!states.Contains(change.State) && !IsHidden(change.State))
                states.Add(change.State);
        return states;
    }

    private static bool IsHidden(string state) => state is "{-}" or "{hidden}" or "{...}";

    private static string Format(double time) => time.ToString("0.###", CultureInfo.InvariantCulture);

    private void PaintRobust(UmlScene scene, UmlTimingLifeline lifeline, List<UmlTimingChange> changes, List<string> states,
        double top, double plotLeft, Func<double, double> x, double end, int item)
    {
        double Level(string state)
        {
            var i = states.IndexOf(state);
            return top + 6 + LevelHeight / 2 + Math.Max(0, i) * LevelHeight;
        }
        foreach (var state in states)
        {
            var size = style.Measure(state, small: true);
            scene.Add(style.Text(state, new Point(plotLeft - size.Width - 8, Level(state) - size.Height / 2), small: true, brush: UmlBrush.MutedText), item);
            scene.Add(new LinePrim { Points = [new Point(plotLeft, Level(state)), new Point(x(end), Level(state))], Brush = UmlBrush.Border, Style = UmlStroke.Dotted, Thickness = 1 }, item);
        }
        if (changes.Count == 0)
            return;

        var points = new List<Point>();
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            var level = Level(change.State);
            var segmentEnd = i + 1 < changes.Count ? x(changes[i + 1].Time) : x(end);
            if (points.Count > 0)
                points.Add(new Point(x(change.Time) + 3, level));
            else
                points.Add(new Point(x(change.Time), level));
            points.Add(new Point(segmentEnd - (i + 1 < changes.Count ? 3 : 0), level));
        }
        scene.Add(new LinePrim { Points = points, Brush = UmlBrush.Accent, Thickness = 2 }, item);
    }

    private void PaintConcise(UmlScene scene, List<UmlTimingChange> changes, double top, double height, Func<double, double> x, double end, int item)
    {
        var bandTop = top + 6;
        var bandHeight = height - 12;
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            var x0 = x(change.Time);
            var x1 = i + 1 < changes.Count ? x(changes[i + 1].Time) : x(end);
            if (IsHidden(change.State))
            {
                scene.Add(new LinePrim { Points = [new Point(x0, bandTop + bandHeight / 2), new Point(x1, bandTop + bandHeight / 2)], Brush = UmlBrush.Border, Thickness = 1.25 }, item);
                continue;
            }
            var rect = new Rect(x0, bandTop, Math.Max(1, x1 - x0), bandHeight);
            scene.Add(new ShapePrim { Shape = UmlShape.Band, Rect = rect, Fill = UmlBrush.Header, Stroke = UmlBrush.Accent, Thickness = 1.25 }, item);
            var size = style.Measure(change.State, small: true);
            if (size.Width + 10 < rect.Width)
                scene.Add(style.Text(change.State, new Point(rect.Center.X - size.Width / 2, rect.Center.Y - size.Height / 2), small: true, center: true), item);
        }
    }

    private void PaintClock(UmlScene scene, UmlTimingLifeline lifeline, double top, double height, Func<double, double> x,
        double start, double end, int item)
    {
        var period = lifeline.ClockPeriod > 0 ? lifeline.ClockPeriod : 1;
        var pulse = lifeline.ClockPulse > 0 ? Math.Min(lifeline.ClockPulse, period) : period / 2;
        var high = top + 6;
        var low = top + height - 6;
        var points = new List<Point> { new(x(start), low) };
        for (var t = start + lifeline.ClockOffset; t < end && points.Count < 4000; t += period)
        {
            points.Add(new Point(x(t), low));
            points.Add(new Point(x(t), high));
            var fall = Math.Min(t + pulse, end);
            points.Add(new Point(x(fall), high));
            points.Add(new Point(x(fall), low));
        }
        points.Add(new Point(x(end), low));
        scene.Add(new LinePrim { Points = points, Brush = UmlBrush.Accent, Thickness = 1.5 }, item);
    }

    private static void PaintBinary(UmlScene scene, List<UmlTimingChange> changes, double top, double height, Func<double, double> x, double end, int item)
    {
        if (changes.Count == 0)
            return;
        var high = top + 6;
        var low = top + height - 6;
        var points = new List<Point>();
        for (var i = 0; i < changes.Count; i++)
        {
            var isHigh = changes[i].State.ToLowerInvariant() is "high" or "1" or "true" or "on";
            var y = isHigh ? high : low;
            points.Add(new Point(x(changes[i].Time), y));
            points.Add(new Point(i + 1 < changes.Count ? x(changes[i + 1].Time) : x(end), y));
        }
        scene.Add(new LinePrim { Points = points, Brush = UmlBrush.Accent, Thickness = 1.5 }, item);
    }
}
