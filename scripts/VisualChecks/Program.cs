using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CalibrationSuite.Calibrator;
using CalibrationSuite.Core;
using CalibrationSuite.Otd;

internal static class Program
{
    private static readonly SourceGeometry Geometry = new(20000, 10000, 200, 120);
    private static readonly CaptureSettings Settings = new();
    private static string output = "";

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
            Application.Current?.Shutdown(1);
        }
    }

    private static void Run(string[] args)
    {
        output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/visual-checks");
        Directory.CreateDirectory(output);
        var app = new App();
        app.InitializeComponent(); // No Run/Show: no daemon, debug acquisition, or settings writes.
        var main = new MainWindow();
        var setup = (ScrollViewer)main.Content;
        Save(setup, 900, 780, 1, "setup-normal");
        if (Descendants(setup).OfType<TextBox>().Count() != 13 || Descendants(setup).OfType<Expander>().Any())
            throw new InvalidOperationException("Every setup input must stay visible without disclosure.");
        Save(setup, 540, 420, 2, "setup-small-top");
        setup.ScrollToBottom();
        Save(setup, 540, 420, 2, "setup-small-bottom");
        var rows = Descendants(setup).OfType<TextBox>().First();
        rows.Text = "1";
        setup.ScrollToTop();
        Save(setup, 540, 420, 2, "setup-validation");
        rows.Text = "3";
        main.Close();

        foreach (var (width, height, scale, name) in new[] { (1920, 1080, 1d, "capture-normal"), (960, 540, 2d, "capture-dpi"), (640, 360, 2d, "capture-small") })
        {
            foreach (var cue in Enum.GetValues<CaptureCue>())
            {
                var view = new CaptureView(Settings, Geometry, "Huion Kamvas 13 (Gen 3)");
                var state = State(cue);
                var reading = cue == CaptureCue.Waiting ? null : Reading(cue == CaptureCue.Adjusting ? new(0, 0) : new(20, 0));
                view.Update(state, reading, 1000);
                Save(view, width, height, scale, $"{name}-{cue.ToString().ToLowerInvariant()}");
                CheckCaptureBounds(view, state.Target);
            }
        }
        foreach (var target in new[] { new Point2(10000, 5000), new(19800, 100), new(100, 9800) })
        {
            var view = new CaptureView(Settings, Geometry, "Synthetic target placement");
            view.Update(State(CaptureCue.Capturing) with
            {
                Target = target
            }, Reading(new(20, 0)), 1000);
            Save(view, 640, 360, 2, $"capture-small-target-{target.X:F0}-{target.Y:F0}");
            CheckCaptureBounds(view, target);
        }
        foreach (var (message, name) in new[]
        {
            ("Tilt mesh does not retain every distinct sample; recapture unusable or collinear coverage.", "coverage"),
            ("Duplicate measured tilt. Recapture with distinct reported values.", "overlap"),
            ("Tilt missing from pen report.", "range")
        })
        {
            var view = new CaptureView(Settings, Geometry, "Synthetic failure");
            view.Update(State(CaptureCue.Retry) with
            {
                Message = message
            }, Reading(new(20, 0)), 1000);
            Save(view, 640, 360, 2, "capture-small-retry-" + name);
            CheckCaptureBounds(view, State(CaptureCue.Retry).Target);
        }
        var gallery = new UniformGrid { Rows = 3, Columns = 3, Background = Brush("#1C2A3D") };
        foreach (var pose in new[] { new RequestedPose(0), new(40, 0), new(40, 90), new(40, 180), new(40, 270), new(40, 45), new(40, 135), new(40, 225), new(40, 315) })
        {
            var cell = new DockPanel { Margin = new(12) };
            var title = new TextBlock { Text = pose.DirectionDeg is { } a ? $"Reported offset · {a:F0}° direction" : "Reference", Foreground = Brushes.White, FontSize = 15 };
            DockPanel.SetDock(title, Dock.Top);
            cell.Children.Add(title);
            var guide = new OrientationGuide();
            guide.Place(new(168, 109), 94);
            guide.Update(pose.Offset, pose.Offset, 3, 48, CaptureCue.Ready, 0);
            cell.Children.Add(guide);
            gallery.Children.Add(cell);
        }
        Save(gallery, 1080, 780, 1, "orientation-gallery");
        CheckTargetSpace();
        foreach (bool positionOnly in new[] { false, true })
        {
            var view = new CaptureView(Settings, Geometry, "Synthetic first measurement");
            var state = State(CaptureCue.Ready) with
            {
                UsesTilt = !positionOnly,
                ReportsTilt = !positionOnly,
                RequestedTilt = null,
                ReferenceTilt = null,
                Index = 0,
                Captured = 0,
                Total = positionOnly ? 9 : 153,
                Pose = new(0),
                Target = new(10000, 5000)
            };
            view.Update(state, Reading(new(5, -10)) with
            {
                Tilt = positionOnly ? null : new Vector2(5, -10)
            }, 1000);
            Save(view, 640, 360, 2, positionOnly ? "capture-position-only" : "capture-reference");
            CheckCaptureBounds(view, state.Target);
            if (positionOnly && !Descendants(view).OfType<TextBlock>().Any(t => t.Text == "OTD reports no tilt · tilt blocked"))
                throw new InvalidOperationException("Position-only capture must explain why tilt is blocked.");
        }
        CheckStableGuide();
        CheckLocalFeedback();
        CheckReusedCaptureLayout();

        var workspace = new CalibrationWorkspace();
        workspace.Begin(new()
        {
            RowCount = 2,
            ColumnCount = 2,
            TiltDirections = 0
        }, Geometry);
        while (!workspace.Session!.Complete)
            workspace.Session.Store(new(workspace.Session.CurrentTarget, default));
        workspace.Finish();
        var review = new ReviewWindow(workspace);
        Save((FrameworkElement)review.Content, 1000, 600, 1, "review-candidate");
        Save((FrameworkElement)review.Content, 520, 380, 2, "review-small");
        var table = Descendants((DependencyObject)review.Content).OfType<DataGrid>().Single();
        double headerHeight = Descendants(table).OfType<DataGridColumnHeadersPresenter>().Single().ActualHeight;
        if (table.ActualHeight < headerHeight + table.RowHeight + SystemParameters.HorizontalScrollBarHeight)
            throw new InvalidOperationException("Small review must show a complete row and its scrollbars.");
        review.Close();
        workspace.Accept();
        review = new(workspace);
        Save((FrameworkElement)review.Content, 1000, 600, 1, "review-accepted");
        review.Close();
        CheckInput();
        CheckRecapture();

        var perf = new CaptureView(Settings, Geometry, "Synthetic input");
        perf.Update(State(CaptureCue.Capturing), Reading(new(20, 0)), 1000);
        Save(perf, 960, 540, 1, "capture-performance");
        var times = new List<double>();
        for (int i = 0; i < 300; i++)
        {
            var watch = Stopwatch.StartNew();
            perf.Update(State(CaptureCue.Capturing), Reading(new(20 + i % 4 * 0.1f, i % 3 * 0.1f)), 1000);
            perf.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        Console.WriteLine($"WPF update/layout (300 synthetic updates): median {times[150]:F2} ms, p95 {times[284]:F2} ms; target geometry is reused between pose changes.");
        Console.WriteLine($"Rendered setup, capture states, nine orientations, and review to {output}. Capture bounds checks passed.");
        app.Shutdown();
    }

    private static CaptureProgress State(CaptureCue cue) => new(1, 1, 153, new(3276.8, 5461.3), new(20, 0), 12, false,
        cue == CaptureCue.Retry ? "Pose left the angle tolerance — hold the requested pose and retry." : "")
    {
        BurstStatus = cue == CaptureCue.Capturing ? CaptureBurstStatus.Capturing : cue == CaptureCue.Retry ? CaptureBurstStatus.Retry : CaptureBurstStatus.Idle,
        UsesTilt = true,
        ReferenceTilt = default(Point2),
        RequestedTilt = new(20, 0),
        BurstProgress = 0.6,
        BurstDurationMs = 150,
        AcceptedAtMs = cue == CaptureCue.Accepted ? 990 : null,
        AcceptedIndex = cue == CaptureCue.Accepted ? 0 : null
    };

    private static void CheckRecapture()
    {
        var workspace = new CalibrationWorkspace();
        workspace.Begin(new() { RowCount = 2, ColumnCount = 2, TiltDirections = 4, TiltLevels = 1 }, Geometry);
        while (!workspace.Session!.Complete)
            workspace.Session.Store(new(workspace.Session.CurrentTarget, workspace.Session.CurrentPose.Offset));
        workspace.Finish();
        workspace.Accept();
        var accepted = workspace.Working!;
        string payload = workspace.CopyPayload();

        foreach (bool wholeNode in new[] { false, true })
        {
            var review = new ReviewWindow(workspace);
            var controls = Descendants((DependencyObject)review.Content).ToArray();
            controls.OfType<DataGrid>().Single().SelectedIndex = 1;
            string action = wholeNode ? "Recapture Node" : "Recapture Pose";
            var button = controls.OfType<Button>().Single(button => Equals(button.Content, action));
            if (!button.IsEnabled)
                throw new InvalidOperationException("Completed measurements must allow recapture.");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            int cleared = wholeNode ? accepted.Poses.Count : 1;
            if (!review.RecaptureRequested || workspace.Session is not { } draft
                || draft.CapturedCount != accepted.Total - cleared || draft.CurrentIndex != (wholeNode ? 0 : 1)
                || draft.Sample(1) != null || accepted.Sample(1) == null || workspace.CopyPayload() != payload)
                throw new InvalidOperationException("Review must start the selected recapture without changing accepted data.");
            workspace.Discard();
        }
        Console.WriteLine("Review pose/node actions create the selected recapture and preserve accepted measurements.");
    }
    private static ReportReading Reading(Vector2 tilt) => new(Stopwatch.GetTimestamp(), new(3275, 5462), tilt, 0, "Paired raw pen reading", ReportType: typeof(OpenTabletDriver.Plugin.Tablet.TiltTabletReport).FullName);
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));

    private static void Save(FrameworkElement content, int width, int height, double scale, string name)
    {
        content.Measure(new(width, height));
        content.Arrange(new(0, 0, width, height));
        content.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        content.Measure(new(width, height));
        content.Arrange(new(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
            dc.DrawRectangle(Brush("#F5F7FB"), null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png"));
        encoder.Save(stream);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void CheckCaptureBounds(CaptureView view, Point2 rawTarget)
    {
        var hud = Descendants(view).OfType<Border>().Single(b => b.Name == "CaptureSummary");
        var panel = (StackPanel)hud.Child;
        foreach (var child in panel.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible))
        {
            var bounds = child.TransformToAncestor(panel).TransformBounds(new Rect(child.RenderSize));
            if (bounds.Bottom > panel.ActualHeight + 1 || bounds.Right > panel.ActualWidth + 1)
                throw new InvalidOperationException($"Clipped {child.GetType().Name} in {view.ActualWidth}×{view.ActualHeight}: {bounds}, panel {panel.RenderSize}");
        }
        var scene = Descendants(view).OfType<OrientationGuide>().Single();
        if (scene.Parent is not Canvas || Math.Abs(scene.Anchor.X - rawTarget.X / Geometry.MaxX * view.ActualWidth) > 0.0001
            || Math.Abs(scene.Anchor.Y - rawTarget.Y / Geometry.MaxY * view.ActualHeight) > 0.0001)
            throw new InvalidOperationException("Orientation guide is not anchored to the mapped physical crosshair.");
        CheckPrecision(scene);
        var details = Descendants(view).OfType<Border>().Single(b => b.Name == "CaptureDetails");
        if (details.Child.RenderSize.Height > details.ActualHeight - details.Padding.Top - details.Padding.Bottom + 1)
            throw new InvalidOperationException("Readings/keyboard hints overflow their panel.");
        var cardBounds = new List<OverlayBounds>();
        foreach (var card in new[] { hud, details })
        {
            var rect = card.TransformToAncestor(view).TransformBounds(new Rect(card.RenderSize));
            var occupied = new OverlayBounds(rect.X, rect.Y, rect.Width, rect.Height);
            cardBounds.Add(occupied);
            if (rect.Left < 0 || rect.Top < 0 || rect.Right > view.ActualWidth + 0.001 || rect.Bottom > view.ActualHeight + 0.001
                || occupied.DistanceTo(scene.Anchor) < scene.Radius + 12 - 0.001)
                throw new InvalidOperationException("A card clips the viewport or the crosshair guide.");
        }
        if (cardBounds[0].Overlaps(cardBounds[1], 7.99))
            throw new InvalidOperationException("Capture cards overlap.");

        var feedback = Descendants(view).OfType<StackPanel>().Single(p => p.Name == "CrosshairFeedback");
        var feedbackRect = feedback.TransformToAncestor(view).TransformBounds(new Rect(feedback.RenderSize));
        var feedbackBounds = new OverlayBounds(feedbackRect.X, feedbackRect.Y, feedbackRect.Width, feedbackRect.Height);
        if (!new Rect(view.RenderSize).Contains(feedbackRect) || scene.OverlapsGuidance(feedbackRect)
            || cardBounds.Any(b => b.Overlaps(feedbackBounds, 0)) || feedbackBounds.DistanceTo(scene.Anchor) > 112)
            throw new InvalidOperationException($"Local feedback obscures guidance/cards, clips, or is too far from the nib: {feedbackRect}, nib {scene.Anchor}.");
        foreach (var child in Descendants(feedback).OfType<TextBlock>())
        {
            var rect = child.TransformToAncestor(feedback).TransformBounds(new Rect(child.RenderSize));
            if (rect.Bottom > feedback.ActualHeight + 0.001)
                throw new InvalidOperationException("Local feedback text clips its reserved height.");
        }
        if (Descendants(hud).Concat(Descendants(details)).OfType<TextBlock>()
            .Any(t => t.Text == "Pose ready" || t.Text == "Capturing" || t.Text == "Try again" || t.Text == "Ready to retry" || t.Text == "Captured"))
            throw new InvalidOperationException("Time-sensitive capture cues still depend on a corner panel.");
    }

    private static IEnumerable<Drawing> Drawings(DrawingGroup group)
    {
        foreach (var child in group.Children)
        {
            yield return child;
            if (child is DrawingGroup nested)
                foreach (var drawing in Drawings(nested))
                    yield return drawing;
        }
    }

    private static void CheckPrecision(OrientationGuide guide)
    {
        var drawings = Drawings(VisualTreeHelper.GetDrawing(guide)).OfType<GeometryDrawing>().ToArray();
        if (drawings.Length == 0 || drawings.Any(d => d.Pen is { Thickness: > 1 } || d.Brush != null))
            throw new InvalidOperationException("Crosshair guidance must use thin unfilled precision landmarks.");
    }

    private static void CheckTargetSpace()
    {
        foreach (var target in new Point2[] { default, new(20, 0), new(0, 20), new(-20, -20) })
            foreach (double tolerance in new[] { 0.25, 3, 8 })
            {
                const double scale = 48;
                var guide = new OrientationGuide();
                guide.Place(new(220, 220), 200);
                guide.Update(target, target, tolerance, scale, CaptureCue.Ready, 0);
                var host = new Grid { Background = Brush("#101A28") };
                host.Children.Add(guide);
                Save(host, 440, 440, 2, $"precision-{target.X}-{target.Y}-{tolerance}");
                CheckPrecision(guide);
                var drawn = (Geometry)typeof(OrientationGuide).GetField("outline", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(guide)!;
                var outline = drawn.GetFlattenedPathGeometry(0.00001, ToleranceType.Absolute);
                foreach (double distance in new[] { 0, tolerance - 0.01, tolerance + 0.01 })
                    foreach (double direction in new[] { 0d, 45, 90, 180, 270 })
                    {
                        var current = target + OrientationScene.PointAt(distance, direction);
                        var tip = OrientationScene.ArrowTip(current, scale) * guide.Radius + guide.Anchor;
                        bool inside = outline.FillContains(new Point(tip.X, tip.Y), 0.000001, ToleranceType.Absolute);
                        if (inside != OrientationGuidance.WithinTolerance(current, target, tolerance))
                            throw new InvalidOperationException($"Drawn target disagrees with reported-tilt capture gate: {target}, {current}.");
                    }
            }
        Console.WriteLine("Native reported-tilt circles match the sampling gate, including tight tolerances and all directions.");
    }

    private static void CheckStableGuide()
    {
        var view = new CaptureView(Settings, Geometry, "Synthetic state transitions");
        double? radius = null;
        foreach (var cue in Enum.GetValues<CaptureCue>())
        {
            view.Update(State(cue), cue == CaptureCue.Waiting ? null : Reading(new(20, 0)), 1000);
            view.Measure(new(640, 360));
            view.Arrange(new(0, 0, 640, 360));
            view.UpdateLayout();
            var guide = Descendants(view).OfType<OrientationGuide>().Single();
            if (radius is { } previous && Math.Abs(guide.Radius - previous) > 0.001)
                throw new InvalidOperationException($"Capture state {cue} changed the orientation scale.");
            radius = guide.Radius;
        }
        Console.WriteLine("Crosshair guidance keeps its scale across ordinary capture-state transitions.");
    }

    private static void CheckReusedCaptureLayout()
    {
        var view = new CaptureView(Settings, Geometry, "Synthetic viewport changes");
        foreach (var (width, height) in new[] { (1920, 1080), (640, 360), (960, 540), (900, 650), (640, 360) })
        {
            var state = State(CaptureCue.Capturing) with
            {
                Target = new(10000, 5000)
            };
            view.Update(state, Reading(new(20, 0)), 1000);
            Save(view, width, height, 1, $"capture-resize-{width}-{height}");
            CheckCaptureBounds(view, state.Target);
            state = state with
            {
                BurstSamples = 10000,
                BurstDurationMs = 12345
            };
            view.Update(state, Reading(new(20, 0)) with
            {
                Position = new(12345, 6789)
            }, 1000);
            Save(view, width, height, 1, $"capture-resize-readings-{width}-{height}");
            CheckCaptureBounds(view, state.Target);
        }
        Console.WriteLine("Reused capture views adapt to viewport and reading-size changes without stale geometry or clipped cards.");
    }

    private static void CheckLocalFeedback()
    {
        var gallery = new UniformGrid { Rows = 2, Columns = 3, Background = Brush("#101A28") };
        foreach (var cue in Enum.GetValues<CaptureCue>())
        {
            var state = State(cue) with
            {
                Target = new(10000, 5000)
            };
            var view = new CaptureView(Settings, Geometry, "Synthetic crosshair feedback");
            view.Update(state, cue == CaptureCue.Waiting ? null : Reading(cue == CaptureCue.Adjusting ? default : new(20, 0)), 1000);
            Save(view, 1920, 1080, 1, "focus-" + cue.ToString().ToLowerInvariant());
            CheckCaptureBounds(view, state.Target);
            var guide = Descendants(view).OfType<OrientationGuide>().Single();
            var arms = Drawings(VisualTreeHelper.GetDrawing(guide)).OfType<GeometryDrawing>()
                .Where(d => d.Geometry is LineGeometry l && Math.Abs((l.EndPoint - l.StartPoint).Length - 13) < 0.0001).ToArray();
            string expected = cue switch
            {
                CaptureCue.Ready or CaptureCue.Accepted or CaptureCue.Retry => "#76E7C2",
                CaptureCue.Capturing => "#73DEFF",
                CaptureCue.Waiting => "#91A0B5",
                _ => "#FFD18B"
            };
            if (arms.Length != 4 || arms.Any(d => ((SolidColorBrush)d.Pen.Brush).Color != (Color)ColorConverter.ConvertFromString(expected)))
                throw new InvalidOperationException($"The {cue} cue is not visible in the crosshair arms.");
            var cell = new DockPanel { Margin = new(0, 8, 0, 8) };
            var label = new TextBlock { Text = cue.ToString(), Foreground = Brushes.White, FontSize = 15, Margin = new(12, 0, 0, 8) };
            DockPanel.SetDock(label, Dock.Top);
            cell.Children.Add(label);
            cell.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Fill = new VisualBrush(view)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new(guide.Anchor.X - 210, guide.Anchor.Y - 140, 420, 280),
                    Stretch = Stretch.Fill
                }
            });
            gallery.Children.Add(cell);
        }
        Save(gallery, 1260, 640, 1, "crosshair-feedback-gallery");

        var retry = new CaptureView(Settings, Geometry, "Synthetic retry recovery");
        var retryState = State(CaptureCue.Retry) with
        {
            Target = new(10000, 5000)
        };
        retry.Update(retryState, Reading(default), 1000);
        Save(retry, 640, 360, 2, "capture-retry-adjust");
        CheckCaptureBounds(retry, retryState.Target);
        retry.Update(retryState, Reading(new(20, 0)), 1000);
        Save(retry, 640, 360, 2, "capture-retry-ready");
        CheckCaptureBounds(retry, retryState.Target);
        if (!Descendants(retry).OfType<TextBlock>().Any(t => t.Text == "Ready to retry"))
            throw new InvalidOperationException("Recovered pose readiness is hidden by the previous retry outcome.");

        // Exercise all tilt quadrants and upright at the center and screen edges.
        foreach (var target in new[] { new Point2(10000, 5000), new(200, 100), new(19800, 100), new(200, 9900), new(19800, 9900) })
            foreach (var rawTilt in new[] { Vector2.Zero, new(20, 0), new(-20, 0), new(0, 20), new(0, -20), new(15, 15), new(-15, 15), new(15, -15), new(-15, -15) })
            {
                var view = new CaptureView(Settings, Geometry, "Synthetic local feedback placement");
                var state = State(CaptureCue.Ready) with
                {
                    Target = target,
                    RequestedTilt = new(rawTilt.X, rawTilt.Y)
                };
                view.Update(state, Reading(rawTilt), 1000);
                view.Measure(new(640, 360));
                view.Arrange(new(0, 0, 640, 360));
                view.UpdateLayout();
                CheckCaptureBounds(view, target);
                // Move the live arrow across the caption's first candidate placement.
                view.Update(state, Reading(-rawTilt), 1000);
                view.UpdateLayout();
                CheckCaptureBounds(view, target);
            }

        foreach (double tolerance in new[] { 0.25, 3, 12, 80 })
        {
            var view = new CaptureView(Settings with
            {
                TiltDirections = 0,
                TiltTolerance = tolerance
            }, Geometry, "Synthetic upright tolerance");
            var state = State(CaptureCue.Ready) with
            {
                Target = new(10000, 5000),
                Pose = new(0),
                UsesTilt = false,
                RequestedTilt = null
            };
            view.Update(state, Reading(default), 1000);
            Save(view, 640, 360, 2, $"capture-upright-tolerance-{tolerance:F2}");
            CheckCaptureBounds(view, state.Target);
        }

        // A success cue identifies the completed pose; the next pose keeps its own gate/color.
        var next = new CaptureView(Settings, Geometry, "Synthetic next-pose transition");
        var nextState = State(CaptureCue.Accepted) with
        {
            Target = new(10000, 5000),
            Pose = new(20, 90),
            RequestedTilt = new(0, 20)
        };
        next.Update(nextState, Reading(new(20, 0)), 1000);
        Save(next, 640, 360, 2, "capture-next-pose-success");
        CheckCaptureBounds(next, nextState.Target);
        var local = Descendants(next).OfType<StackPanel>().Single(p => p.Name == "CrosshairFeedback");
        if (!Descendants(local).OfType<TextBlock>().Any(t => t.Text == "Target 1 · pose 1 captured"))
            throw new InvalidOperationException("Local success does not identify the completed pose.");
        var nextGuide = Descendants(next).OfType<OrientationGuide>().Single();
        if (Drawings(VisualTreeHelper.GetDrawing(nextGuide)).OfType<GeometryDrawing>()
            .Any(d => d.Pen?.Brush is SolidColorBrush b && b.Color == (Color)ColorConverter.ConvertFromString("#76E7C2")))
            throw new InvalidOperationException("Previous-pose success makes the next, unready pose look ready.");
        Console.WriteLine("Readiness, progress, success, waiting, and retries appear beside the crosshair without covering precision landmarks or requiring corner cues.");
    }

    private static void CheckInput()
    {
        var settings = new CaptureSettings { RowCount = 2, ColumnCount = 2, TiltDirections = 0, CaptureWindowMs = 10, MinSamplesPerCapture = 2 };
        var draft = new CaptureDraft(settings, Geometry);
        var controller = new CaptureController(draft);
        var acquisition = new AcquisitionSession(new DaemonConnection("CalibrationSuite.VisualChecks.Unused"), new());
        var window = new CaptureWindow(acquisition, controller, draft, "Synthetic keyboard input", new(0, 0, 1, 1));
        var source = new DetachedSource();
        void KeyPress(Key key)
        {
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(e);
            if (!e.Handled)
                throw new InvalidOperationException($"{key} not handled.");
        }
        var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent };
        window.RaiseEvent(mouse);
        if (!mouse.Handled || controller.Snapshot(CaptureController.Now).Active)
            throw new InvalidOperationException("Mouse contact triggered capture.");
        var touch = new TouchEventArgs(new SyntheticTouch(), 0) { RoutedEvent = UIElement.PreviewTouchDownEvent };
        window.RaiseEvent(touch);
        if (!touch.Handled || controller.Snapshot(CaptureController.Now).Active)
            throw new InvalidOperationException("Touch contact triggered capture.");
        KeyPress(Key.Space);
        KeyPress(Key.Space);
        double now = CaptureController.Now;
        if (!controller.Snapshot(now).Active)
            throw new InvalidOperationException("Space did not start capture.");
        controller.Read(Reading(default) with
        {
            Timestamp = (long)Math.Ceiling((now + 1) * Stopwatch.Frequency / 1000)
        });
        controller.Read(Reading(default) with
        {
            Timestamp = (long)Math.Ceiling((now + 12) * Stopwatch.Frequency / 1000)
        });
        if (draft.CapturedCount != 1)
            throw new InvalidOperationException("Keyboard capture lost the burst.");
        KeyPress(Key.Back);
        if (draft.CurrentIndex != 0 || draft.CapturedCount != 1)
            throw new InvalidOperationException("Backspace lost stored progress.");
        KeyPress(Key.R);
        if (draft.CapturedCount != 0)
            throw new InvalidOperationException("R did not restart the selection.");
        KeyPress(Key.Escape);
        Console.WriteLine("Synthetic WPF keyboard routing and mouse/touch suppression passed; no acquisition was started.");
    }

    private sealed class DetachedSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = new DrawingVisual();
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private sealed class SyntheticTouch() : TouchDevice(1)
    {
        public override TouchPoint GetTouchPoint(IInputElement relativeTo) => new(this, new(0, 0), new(0, 0, 1, 1), TouchAction.Down);
        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) => new() { GetTouchPoint(relativeTo) };
    }
}
