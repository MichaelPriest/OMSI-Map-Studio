using MapStudio.Native.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MapStudio.Renderer.Scene;

namespace MapStudio.Native;

public sealed partial class MainWindow
{
    private bool _roadProfileOpen;

    private async void OnRoadProfileClick(object sender, RoutedEventArgs e)
    {
        if (_roadProfileOpen) return;
        var profile = Viewport.GetSelectedRoadProfile();
        if (profile is null)
        {
            StatusText.Text = "Selecione uma spline válida para ver o perfil longitudinal.";
            return;
        }
        _roadProfileOpen = true;
        try
        {
            const double width = 420, height = 210, margin = 18;
            var values = profile.Points.Select(p => (double)p.WorldPoint.Y)
                .Concat(profile.Points.Where(p => p.TerrainHeight.HasValue).Select(p => p.TerrainHeight!.Value)).ToArray();
            var low = values.Min();
            var high = values.Max();
            if (high - low < 1) { low -= 0.5; high += 0.5; }
            double X(double distance) => margin + distance / profile.Length * (width - 2 * margin);
            double Y(double elevation) => height - margin - (elevation - low) / (high - low) * (height - 2 * margin);
            var roadBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 41, 151, 255));
            var terrainBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 81, 185, 113));
            var canvas = new Canvas { Width = width, Height = height,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 30, 40)) };
            var gridBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 55, 73, 89));
            for (var i = 0; i <= 4; i++)
            {
                var y = margin + i * (height - 2 * margin) / 4;
                canvas.Children.Add(new Line { X1 = margin, X2 = width - margin, Y1 = y, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 });
            }
            var road = new Polyline { Stroke = roadBrush, StrokeThickness = 2 };
            Polyline? terrain = null;
            foreach (var p in profile.Points)
            {
                road.Points.Add(new Windows.Foundation.Point(X(p.Distance), Y(p.WorldPoint.Y)));
                if (p.TerrainHeight is not { } t) { terrain = null; continue; }
                if (terrain is null)
                {
                    terrain = new Polyline { Stroke = terrainBrush, StrokeThickness = 2 };
                    canvas.Children.Add(terrain);
                }
                terrain.Points.Add(new Windows.Foundation.Point(X(p.Distance), Y(t)));
            }
            canvas.Children.Add(road);
            var cursor = new Line { X1 = margin, X2 = margin, Y1 = margin, Y2 = height - margin,
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 185, 70)), StrokeThickness = 1 };
            canvas.Children.Add(cursor);
            var readout = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var slider = new Slider { Minimum = 0, Maximum = profile.Points.Count - 1, StepFrequency = 1, Value = 0 };
            void Update()
            {
                var p = profile.Points[Math.Clamp((int)Math.Round(slider.Value), 0, profile.Points.Count - 1)];
                cursor.X1 = cursor.X2 = X(p.Distance);
                readout.Text = $"Distância {p.Distance:F1} m · via {p.WorldPoint.Y:F2} m · inclinação {p.Gradient:+0.0;-0.0;0.0}%\n" +
                    (p.TerrainHeight is { } t ? $"Terreno {t:F2} m · folga {p.Clearance:F2} m" : "Terreno indisponível neste ponto");
            }
            slider.ValueChanged += (_, _) => Update();
            Update();
            var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            legend.Children.Add(new TextBlock { Text = "● Via", Foreground = roadBrush });
            legend.Children.Add(new TextBlock { Text = "● Terreno", Foreground = terrainBrush });
            var missing = profile.Points.Count(p => !p.TerrainHeight.HasValue);
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = $"Spline #{profile.SplineId} · {profile.Length:F1} m · desnível {profile.Rise:+0.0;-0.0;0.0} m · inclinação máxima {profile.MaxAbsoluteGradient:F1}%",
                TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(legend);
            panel.Children.Add(new Viewbox { Stretch = Stretch.Uniform, MaxWidth = width, Child = canvas });
            panel.Children.Add(new TextBlock { Text = $"Cotas: {low:F2} a {high:F2} m · 0 a {profile.Length:F1} m · {missing} amostras sem terreno", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
            panel.Children.Add(slider);
            panel.Children.Add(readout);
            var dialog = new ContentDialog { XamlRoot = MainRoot.XamlRoot, Title = "Road Studio · Perfil longitudinal", Content = panel,
                PrimaryButtonText = "Ir ao ponto", CloseButtonText = "Fechar" };
            if (await dialog.ShowAdaptiveAsync() == ContentDialogResult.Primary)
            {
                var p = profile.Points[Math.Clamp((int)Math.Round(slider.Value), 0, profile.Points.Count - 1)];
                Viewport.FocusWorldPoint(p.WorldPoint, 35);
            }
        }
        catch (Exception ex) { StatusText.Text = $"Perfil longitudinal: {ex.Message}"; }
        finally { _roadProfileOpen = false; }
    }
}
