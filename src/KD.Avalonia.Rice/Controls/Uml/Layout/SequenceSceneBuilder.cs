using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Lays out a <see cref="UmlSequenceDiagram"/>: participants in columns spaced so the messages fit, events as rows
/// from top to bottom with activation bars, fragments, notes and dividers.
/// </summary>
internal sealed class SequenceSceneBuilder(UmlSequenceDiagram diagram, UmlStyle style)
{
    private const double BarWidth = 10;
    private const double BarStep = 5;
    private const double SelfLoopWidth = 36;
    private const double BorderMargin = 48;

    private sealed class Column(UmlParticipant participant, int index)
    {
        public UmlParticipant Participant { get; } = participant;
        public int Index { get; } = index;
        public Size HeaderSize { get; set; }
        public double X { get; set; }
        public double? CreatedAt { get; set; }
        public double? DestroyedAt { get; set; }
        public List<double> Open { get; } = [];
        public List<(double Top, double Bottom, int Depth)> Bars { get; } = [];
        public double Offset(int depth) => depth <= 0 ? 0 : BarWidth / 2 + (depth - 1) * BarStep;
    }

    private sealed class Frame(UmlFragmentStart start, double top)
    {
        public UmlFragmentStart Start { get; } = start;
        public double Top { get; } = top;
        public List<(double Y, string? Label)> Operands { get; } = [];
        public double MinX { get; set; } = double.MaxValue;
        public double MaxX { get; set; } = double.MinValue;
        public void Include(double a, double b)
        {
            MinX = Math.Min(MinX, Math.Min(a, b));
            MaxX = Math.Max(MaxX, Math.Max(a, b));
        }
    }

    private readonly Dictionary<string, Column> _columns = [];
    private readonly List<Column> _ordered = [];
    private readonly List<Frame> _frames = [];
    private readonly List<Action<UmlScene>> _background = [];
    private readonly List<Action<UmlScene>> _foreground = [];
    private double _leftBorder;
    private double _rightBorder;

    private double Small => style.Measure("Xg", small: true).Height;

    public void Build(UmlScene scene, Point origin)
    {
        foreach (var p in diagram.Participants)
            AddColumn(p);
        // Participants only used in events (models built in code) still get a column.
        foreach (var id in diagram.Events.SelectMany(Participants))
            if (!_columns.ContainsKey(id) && id is not (UmlSequenceDiagram.LeftBorder or UmlSequenceDiagram.RightBorder))
                AddColumn(new UmlParticipant(id));
        if (_ordered.Count == 0)
            return;

        foreach (var c in _ordered)
            c.HeaderSize = HeaderSize(c.Participant);
        var headerHeight = _ordered.Max(c => c.HeaderSize.Height);
        AssignColumns(origin.X);

        var top = origin.Y;
        var y = top + headerHeight + 18;
        y = LayoutEvents(y);
        var footerTop = y + 14;

        // Lifelines from below the head (or where the participant is created) to the foot (or its destruction).
        foreach (var c in _ordered)
        {
            var start = c.CreatedAt is { } created ? created + c.HeaderSize.Height / 2 : top + headerHeight;
            var end = c.DestroyedAt ?? footerTop;
            foreach (var open in c.Open.ToList())
                c.Bars.Add((open, Math.Max(open + 12, end - 6), c.Open.IndexOf(open) + 1));
            scene.Add(new LinePrim { Points = [new Point(c.X, start), new Point(c.X, end)], Style = UmlStroke.Dashed, Brush = UmlBrush.Border, Thickness = 1.25 });
        }
        foreach (var c in _ordered)
            foreach (var (barTop, barBottom, depth) in c.Bars.OrderBy(b => b.Depth))
            {
                var x = c.X - BarWidth / 2 + (depth - 1) * BarStep;
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = new Rect(x, barTop, BarWidth, barBottom - barTop), Radius = 2, Fill = UmlBrush.Header, Stroke = UmlBrush.Border });
            }

        foreach (var action in _background)
            action(scene);
        foreach (var action in _foreground)
            action(scene);

        foreach (var c in _ordered)
        {
            var headTop = c.CreatedAt is { } created ? created - c.HeaderSize.Height / 2 : top + headerHeight - c.HeaderSize.Height;
            PaintHeader(scene, c, headTop);
            if (diagram.ShowFootbox && c.DestroyedAt is null)
                PaintHeader(scene, c, footerTop);
        }
    }

    private void AddColumn(UmlParticipant p)
    {
        if (_columns.ContainsKey(p.Id))
            return;
        var column = new Column(p, _ordered.Count);
        _columns[p.Id] = column;
        _ordered.Add(column);
    }

    private static IEnumerable<string> Participants(UmlSequenceEvent e) => e switch
    {
        UmlMessage m => [m.From, m.To],
        UmlActivation a => [a.Participant],
        UmlSequenceNote n => n.Participants,
        UmlInteractionUse r => r.Participants,
        _ => [],
    };

    // ---- columns ----

    private Size HeaderSize(UmlParticipant p)
    {
        var name = style.Measure(p.Name, bold: true);
        var stereo = style.Measure(Stereo(p), small: true);
        var text = new Size(Math.Max(name.Width, stereo.Width), name.Height + stereo.Height);
        return p.Kind switch
        {
            UmlParticipantKind.Actor => new Size(Math.Max(40, text.Width), 44 + 6 + text.Height),
            UmlParticipantKind.Boundary or UmlParticipantKind.Control or UmlParticipantKind.Entity =>
                new Size(Math.Max(44, text.Width), 30 + 6 + text.Height),
            UmlParticipantKind.Database => new Size(Math.Max(44, text.Width), 34 + 6 + text.Height),
            _ => new Size(Math.Max(84, text.Width + 30), text.Height + 20),
        };
    }

    private static string Stereo(UmlParticipant p) => p.Stereotypes.Count == 0 ? "" : UmlStyle.Guillemets(p.Stereotypes);

    /// <summary>Column positions: neighbors do not overlap and every message label fits between its two ends.</summary>
    private void AssignColumns(double left)
    {
        var constraints = new List<(int From, int To, double Distance)>();
        for (var i = 0; i + 1 < _ordered.Count; i++)
            constraints.Add((i, i + 1, (_ordered[i].HeaderSize.Width + _ordered[i + 1].HeaderSize.Width) / 2 + 28));

        var number = 0;
        foreach (var e in diagram.Events)
        {
            switch (e)
            {
                case UmlMessage m:
                {
                    var label = style.Measure(MessageText(m, ref number), small: true).Width;
                    var a = _columns.GetValueOrDefault(m.From);
                    var b = _columns.GetValueOrDefault(m.To);
                    if (a is null || b is null)
                        break;
                    if (a == b)
                    {
                        if (a.Index + 1 < _ordered.Count)
                            constraints.Add((a.Index, a.Index + 1, label + SelfLoopWidth + 28));
                    }
                    else
                    {
                        var (i, j) = a.Index < b.Index ? (a.Index, b.Index) : (b.Index, a.Index);
                        constraints.Add((i, j, label + 36 + 2 * BarWidth));
                    }
                    break;
                }
                case UmlSequenceNote n:
                {
                    var width = NoteSize(n.Text).Width;
                    var ids = n.Participants.Where(_columns.ContainsKey).Select(id => _columns[id].Index).ToList();
                    if (ids.Count == 0)
                        break;
                    var i = ids.Min();
                    if (n.Position == UmlNotePosition.Right && i + 1 < _ordered.Count)
                        constraints.Add((i, i + 1, width + 24));
                    else if (n.Position == UmlNotePosition.Left && i > 0)
                        constraints.Add((i - 1, i, width + 24));
                    else if (n.Position == UmlNotePosition.Over && ids.Max() > i)
                        constraints.Add((i, ids.Max(), width - 40));
                    break;
                }
            }
        }

        var x = new double[_ordered.Count];
        x[0] = left + Math.Max(_ordered[0].HeaderSize.Width / 2, LeftNeed());
        for (var i = 1; i < x.Length; i++)
            x[i] = x[i - 1];
        foreach (var (from, to, distance) in constraints.OrderBy(c => c.To - c.From))
        {
            var deficit = x[from] + distance - x[to];
            if (deficit <= 0)
                continue;
            for (var k = to; k < x.Length; k++)
                x[k] += deficit;
        }
        for (var i = 0; i < x.Length; i++)
            _ordered[i].X = x[i];

        _leftBorder = x[0] - _ordered[0].HeaderSize.Width / 2 - BorderMargin;
        _rightBorder = x[^1] + _ordered[^1].HeaderSize.Width / 2 + BorderMargin;
        if (_leftBorder < left)
        {
            var shift = left - _leftBorder;
            foreach (var c in _ordered)
                c.X += shift;
            _leftBorder += shift;
            _rightBorder += shift;
        }
    }

    /// <summary>Room left of the first column for notes on its left and found messages.</summary>
    private double LeftNeed()
    {
        if (_ordered.Count == 0)
            return 0;
        var first = _ordered[0].Participant.Id;
        var need = 0.0;
        foreach (var e in diagram.Events)
        {
            if (e is UmlSequenceNote { Position: UmlNotePosition.Left } n && n.Participants.FirstOrDefault() == first)
                need = Math.Max(need, NoteSize(n.Text).Width + 12);
            if (e is UmlMessage m && (m.From == UmlSequenceDiagram.LeftBorder || m.To == UmlSequenceDiagram.LeftBorder))
                need = Math.Max(need, style.Measure(m.Text ?? "", small: true).Width + 24);
        }
        return need;
    }

    private string MessageText(UmlMessage m, ref int number)
    {
        var text = m.Text ?? "";
        return diagram.AutoNumber ? $"{++number}  {text}".TrimEnd() : text;
    }

    private Size NoteSize(string text)
    {
        var size = style.Measure(text, small: true);
        return new Size(size.Width + 26, size.Height + 14);
    }

    // ---- events ----

    private double LayoutEvents(double y)
    {
        var number = 0;
        UmlMessage? lastMessage = null;
        var lastArrowY = y;
        var small = Small;

        foreach (var e in diagram.Events)
        {
            switch (e)
            {
                case UmlMessage m:
                {
                    var text = MessageText(m, ref number);
                    var labelHeight = text.Length > 0 ? style.Measure(text, small: true).Height : 0;
                    _columns.TryGetValue(m.From, out var from);
                    _columns.TryGetValue(m.To, out var to);

                    if (m.IsSelf && from is not null)
                    {
                        y += labelHeight + 6;
                        var ya = y;
                        var startX = from.X + from.Offset(from.Open.Count);
                        if (m.ActivateTarget)
                            from.Open.Add(ya + 18);
                        var endX = from.X + from.Offset(from.Open.Count);
                        var points = new List<Point> { new(startX, ya), new(startX + SelfLoopWidth, ya), new(startX + SelfLoopWidth, ya + 18), new(endX, ya + 18) };
                        AddMessage(m, points, text, new Point(startX + 6, ya - labelHeight - 3), alignLeft: true);
                        Include(startX, startX + SelfLoopWidth + style.Measure(text, small: true).Width);
                        lastArrowY = ya + 18;
                        y = ya + 18 + 12;
                    }
                    else
                    {
                        y += labelHeight + 8;
                        var ya = y;
                        if (m.ActivateTarget && to is not null)
                            to.Open.Add(ya);
                        if (m.CreateTarget && to is not null)
                        {
                            to.CreatedAt = ya;
                            y += to.HeaderSize.Height / 2;
                        }

                        var fromX = from?.X ?? (m.From == UmlSequenceDiagram.RightBorder ? _rightBorder : _leftBorder);
                        var toX = to?.X ?? (m.To == UmlSequenceDiagram.LeftBorder ? _leftBorder : _rightBorder);
                        var rightwards = toX >= fromX;
                        if (from is not null)
                            fromX += (rightwards ? 1 : -1) * from.Offset(from.Open.Count);
                        if (to is not null)
                            toX += (rightwards ? -1 : 1) * (m.CreateTarget ? to.HeaderSize.Width / 2 : to.Offset(to.Open.Count));

                        var labelWidth = style.Measure(text, small: true).Width;
                        var labelOrigin = new Point((fromX + toX) / 2 - labelWidth / 2, ya - labelHeight - 3);
                        AddMessage(m, [new Point(fromX, ya), new Point(toX, ya)], text, labelOrigin, alignLeft: false);
                        Include(fromX, toX);
                        lastArrowY = ya;
                        y = Math.Max(y, ya) + 10;
                    }

                    if (m.DeactivateSource && from is not null)
                        CloseBar(from, lastArrowY);
                    if (m.DestroyTarget && to is not null)
                        Destroy(to, lastArrowY);
                    lastMessage = m;
                    break;
                }

                case UmlActivation a when _columns.TryGetValue(a.Participant, out var column):
                    switch (a.Kind)
                    {
                        case UmlActivationKind.Activate:
                            column.Open.Add(lastMessage?.To == a.Participant ? lastArrowY : y);
                            break;
                        case UmlActivationKind.Deactivate:
                            CloseBar(column, lastMessage is not null && (lastMessage.From == a.Participant || lastMessage.To == a.Participant)
                                ? Math.Max(lastArrowY, column.Open.LastOrDefault() + 12) : y);
                            break;
                        case UmlActivationKind.Destroy:
                            y += 8;
                            Destroy(column, y);
                            y += 10;
                            break;
                        case UmlActivationKind.Create:
                            column.CreatedAt = y + column.HeaderSize.Height / 2;
                            y += column.HeaderSize.Height + 8;
                            break;
                    }
                    break;

                case UmlFragmentStart f:
                    y += 6;
                    _frames.Add(new Frame(f, y));
                    y += small + 18;
                    break;

                case UmlFragmentElse f when _frames.Count > 0:
                    y += 6;
                    _frames[^1].Operands.Add((y, f.Label));
                    y += small + 12;
                    break;

                case UmlFragmentEnd when _frames.Count > 0:
                {
                    y += 10;
                    var frame = _frames[^1];
                    _frames.RemoveAt(_frames.Count - 1);
                    CloseFrame(frame, y);
                    y += 6;
                    break;
                }

                case UmlInteractionUse r:
                {
                    var columns = r.Participants.Where(_columns.ContainsKey).Select(id => _columns[id]).ToList();
                    if (columns.Count == 0)
                        columns = [.. _ordered];
                    var text = style.Measure(r.Text);
                    var x0 = columns.Min(c => c.X) - 30;
                    var x1 = Math.Max(columns.Max(c => c.X) + 30, x0 + text.Width + 60);
                    y += 8;
                    var rect = new Rect(x0, y, x1 - x0, text.Height + small + 22);
                    _foreground.Add(scene =>
                    {
                        var item = scene.AddItem(new UmlHitItem(r, rect) { CornerRadius = 4 });
                        var tag = style.Measure("ref", bold: true, small: true);
                        scene.Add(new ShapePrim { Shape = UmlShape.Frame, Rect = rect, Extra = tag.Width + 18, Extra2 = tag.Height + 6, Fill = UmlBrush.Node }, item);
                        scene.Add(style.Text("ref", new Point(rect.X + 7, rect.Y + 3), bold: true, small: true), item);
                        scene.Add(style.CenteredText(r.Text, rect.Center.X, rect.Y + small + 12), item);
                    });
                    Include(rect.X, rect.Right);
                    y = rect.Bottom + 10;
                    break;
                }

                case UmlDivider d:
                {
                    y += 10;
                    var dividerY = y;
                    var text = d.Text;
                    _foreground.Add(scene =>
                    {
                        scene.Add(new LinePrim { Points = [new Point(_leftBorder, dividerY - 1.5), new Point(_rightBorder, dividerY - 1.5)], Brush = UmlBrush.Border, Thickness = 1 });
                        scene.Add(new LinePrim { Points = [new Point(_leftBorder, dividerY + 1.5), new Point(_rightBorder, dividerY + 1.5)], Brush = UmlBrush.Border, Thickness = 1 });
                        if (text is { Length: > 0 })
                        {
                            var size = style.Measure(text, bold: true, small: true);
                            var center = (_leftBorder + _rightBorder) / 2;
                            var pill = new Rect(center - size.Width / 2 - 10, dividerY - size.Height / 2 - 3, size.Width + 20, size.Height + 6);
                            scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = pill, Radius = pill.Height / 2, Fill = UmlBrush.Header, Stroke = UmlBrush.Border });
                            scene.Add(style.Text(text, new Point(pill.X + 10, pill.Y + 3), bold: true, small: true));
                        }
                    });
                    y += small + 10;
                    break;
                }

                case UmlDelay d:
                {
                    y += 8;
                    var delayY = y;
                    var text = d.Text;
                    _foreground.Add(scene =>
                    {
                        // The lifelines fade out: background colored gaps over them.
                        foreach (var c in _ordered)
                            scene.Add(new LinePrim { Points = [new Point(c.X, delayY), new Point(c.X, delayY + 22)], Brush = UmlBrush.Background, Thickness = 3 });
                        foreach (var c in _ordered)
                            scene.Add(new LinePrim { Points = [new Point(c.X, delayY), new Point(c.X, delayY + 22)], Brush = UmlBrush.Border, Style = UmlStroke.Dotted, Thickness = 1.25 });
                        if (text is { Length: > 0 })
                            scene.Add(style.CenteredText(text, (_leftBorder + _rightBorder) / 2, delayY + 11 - small / 2, small: true, brush: UmlBrush.MutedText, italic: true));
                    });
                    y += 30;
                    break;
                }

                case UmlSpacing s:
                    y += s.Height ?? 20;
                    break;

                case UmlSequenceNote n:
                {
                    var columns = n.Participants.Where(_columns.ContainsKey).Select(id => _columns[id]).ToList();
                    if (columns.Count == 0)
                        break;
                    var size = NoteSize(n.Text);
                    var first = columns[0];
                    var noteTop = y + 6;
                    var rect = n.Position switch
                    {
                        UmlNotePosition.Left => new Rect(first.X - first.Offset(first.Open.Count) - 8 - size.Width, noteTop, size.Width, size.Height),
                        UmlNotePosition.Right => new Rect(first.X + first.Offset(first.Open.Count) + 8, noteTop, size.Width, size.Height),
                        _ when columns.Count > 1 => SpanRect(columns, size, noteTop),
                        _ => new Rect(first.X - size.Width / 2, noteTop, size.Width, size.Height),
                    };
                    _foreground.Add(scene =>
                    {
                        var item = scene.AddItem(new UmlHitItem(n, rect) { CornerRadius = 2 });
                        scene.Add(new ShapePrim { Shape = UmlShape.Note, Rect = rect, Fill = UmlBrush.Note, Stroke = UmlBrush.NoteBorder, Shadow = true }, item);
                        scene.Add(style.Text(n.Text, new Point(rect.X + 10, rect.Y + 7), small: true), item);
                    });
                    Include(rect.X, rect.Right);
                    y = rect.Bottom + 8;
                    break;
                }
            }
        }

        // Fragments without "end" still get their frame.
        while (_frames.Count > 0)
        {
            y += 10;
            var frame = _frames[^1];
            _frames.RemoveAt(_frames.Count - 1);
            CloseFrame(frame, y);
        }
        return y;
    }

    private static Rect SpanRect(List<Column> columns, Size size, double top)
    {
        var x0 = columns.Min(c => c.X) - 24;
        var x1 = columns.Max(c => c.X) + 24;
        var width = Math.Max(size.Width, x1 - x0);
        var center = (x0 + x1) / 2;
        return new Rect(center - width / 2, top, width, size.Height);
    }

    private void Include(double a, double b)
    {
        foreach (var frame in _frames)
            frame.Include(a, b);
    }

    private void AddMessage(UmlMessage m, List<Point> points, string text, Point labelOrigin, bool alignLeft)
    {
        var head = m.Kind == UmlMessageKind.Synchronous ? UmlArrowHead.FilledTriangle : UmlArrowHead.Arrow;
        _foreground.Add(scene =>
        {
            var line = new LinePrim
            {
                Points = points,
                Style = m.Kind == UmlMessageKind.Reply ? UmlStroke.Dashed : UmlStroke.Solid,
                Thickness = 1.25,
            };
            var bounds = UmlGeometry.BoundsOf(points).Inflate(6);
            TextPrim? label = null;
            if (text.Length > 0)
            {
                label = style.Text(text, labelOrigin, small: true, center: !alignLeft);
                bounds = bounds.Union(new Rect(labelOrigin, label.Size));
            }
            var item = scene.AddItem(new UmlHitItem(m, bounds)
            {
                Path = points,
                From = _columns.GetValueOrDefault(m.From)?.Participant,
                To = _columns.GetValueOrDefault(m.To)?.Participant,
            });
            scene.Add(line, item);
            var direction = UmlGeometry.Normalize(points[^1] - points[^2]);
            scene.Add(new HeadPrim { Tip = points[^1], Direction = direction, Head = head }, item);
            if (m.From is UmlSequenceDiagram.LeftBorder or UmlSequenceDiagram.RightBorder)
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Dot, Rect = new Rect(points[0].X - 4, points[0].Y - 4, 8, 8), Fill = UmlBrush.Line }, item);
            if (label is not null)
                scene.Add(label, item);
        });
    }

    private static void CloseBar(Column column, double y)
    {
        if (column.Open.Count == 0)
            return;
        var depth = column.Open.Count;
        var start = column.Open[^1];
        column.Open.RemoveAt(column.Open.Count - 1);
        column.Bars.Add((start, Math.Max(start + 12, y), depth));
    }

    private void Destroy(Column column, double y)
    {
        while (column.Open.Count > 0)
            CloseBar(column, y);
        column.DestroyedAt = y;
        _foreground.Add(scene => scene.Add(new GlyphPrim { Glyph = UmlGlyph.Cross, Rect = new Rect(column.X - 8, y - 8, 16, 16), Brush = UmlBrush.Private }));
    }

    private void CloseFrame(Frame frame, double bottom)
    {
        if (frame.MinX > frame.MaxX)
            frame.Include(_ordered[0].X - 20, _ordered[^1].X + 20);
        var kindText = frame.Start.Kind.ToString().ToLowerInvariant();
        var tag = style.Measure(kindText, bold: true, small: true);
        var label = frame.Start.Label is { Length: > 0 } l ? (frame.Start.Kind == UmlFragmentKind.Group ? l : $"[{l}]") : null;
        var labelSize = label is null ? default : style.Measure(label, small: true);
        var x0 = frame.MinX - 16;
        var x1 = Math.Max(frame.MaxX + 16, x0 + tag.Width + labelSize.Width + 48);
        var rect = new Rect(x0, frame.Top, x1 - x0, bottom - frame.Top);
        foreach (var outer in _frames)
            outer.Include(rect.X, rect.Right);

        _background.Add(scene =>
        {
            var item = scene.AddItem(new UmlHitItem(frame.Start, new Rect(rect.X, rect.Y, tag.Width + 18, tag.Height + 6)) { CornerRadius = 4 });
            scene.Add(new ShapePrim
            {
                Shape = UmlShape.Frame,
                Rect = rect,
                Extra = tag.Width + 18,
                Extra2 = tag.Height + 6,
                Fill = UmlBrush.None,
                Stroke = UmlBrush.Line,
                Radius = 4,
            }, item);
            scene.Add(style.Text(kindText, new Point(rect.X + 7, rect.Y + 3), bold: true, small: true), item);
            if (label is not null)
                scene.Add(style.Text(label, new Point(rect.X + tag.Width + 26, rect.Y + 3), small: true, brush: UmlBrush.MutedText), item);
            foreach (var (y, operand) in frame.Operands)
            {
                scene.Add(new LinePrim { Points = [new Point(rect.X, y), new Point(rect.Right, y)], Style = UmlStroke.Dashed, Brush = UmlBrush.Line, Thickness = 1 }, item);
                if (operand is { Length: > 0 })
                    scene.Add(style.Text($"[{operand}]", new Point(rect.X + 10, y + 4), small: true, brush: UmlBrush.MutedText), item);
            }
        });
    }

    // ---- participants ----

    private void PaintHeader(UmlScene scene, Column c, double top)
    {
        var size = c.HeaderSize;
        var rect = new Rect(c.X - size.Width / 2, top, size.Width, size.Height);
        var item = scene.AddItem(new UmlHitItem(c.Participant, rect) { CornerRadius = 8 });
        var p = c.Participant;
        var stereo = Stereo(p);

        double CaptionTop(double iconHeight) => top + iconHeight + 6;
        void Caption(double y)
        {
            if (stereo.Length > 0)
                y += scene.Add(style.CenteredText(stereo, c.X, y, small: true, brush: UmlBrush.MutedText), item).Size.Height;
            scene.Add(style.CenteredText(p.Name, c.X, y, bold: true), item);
        }

        switch (p.Kind)
        {
            case UmlParticipantKind.Actor:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.StickFigure, Rect = new Rect(c.X - 14, top, 28, 44), Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                Caption(CaptionTop(44));
                break;
            case UmlParticipantKind.Boundary or UmlParticipantKind.Control or UmlParticipantKind.Entity:
                var glyph = p.Kind switch
                {
                    UmlParticipantKind.Boundary => UmlGlyph.Boundary,
                    UmlParticipantKind.Control => UmlGlyph.Control,
                    _ => UmlGlyph.Entity,
                };
                scene.Add(new GlyphPrim { Glyph = glyph, Rect = new Rect(c.X - 18, top, 36, 30), Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                Caption(CaptionTop(30));
                break;
            case UmlParticipantKind.Database:
                scene.Add(new ShapePrim { Shape = UmlShape.Cylinder, Rect = new Rect(c.X - 15, top, 30, 34), Extra = 6, Shadow = true }, item);
                Caption(CaptionTop(34));
                break;
            default:
            {
                var shape = p.Kind switch
                {
                    UmlParticipantKind.Collections => UmlShape.Collections,
                    _ => UmlShape.RoundedRectangle,
                };
                var radius = p.Kind == UmlParticipantKind.Queue ? size.Height / 2 : 8;
                scene.Add(new ShapePrim { Shape = shape, Rect = rect, Radius = radius, Fill = UmlBrush.Header, Shadow = true }, item);
                var textHeight = style.Measure(p.Name, bold: true).Height + style.Measure(stereo, small: true).Height;
                Caption(rect.Center.Y - textHeight / 2);
                break;
            }
        }
    }
}
