using MapStudio.Native.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MapStudio.Native;

public sealed partial class MainWindow
{
    private bool _geoWorkspaceDocked = true;
    private bool _professionalInspectorReady;
    private bool _professionalInspectorQueued;
    private bool _professionalInspectorAutomatic = true;

    private void InitializeProfessionalWorkspace()
    {
        foreach (var menu in new[] { ShowTerrainMenuItem, ShowObjectsMenuItem, ShowSplinesMenuItem,
                     ShowGridMenuItem, ShowSplineProfilesMenuItem, MapReferenceVisibilityMenuItem })
        {
            menu.RegisterPropertyChangedCallback(ToggleMenuFlyoutItem.IsCheckedProperty,
                (_, _) => SyncProfessionalLayers());
        }
        SyncProfessionalLayers();
        WorkspaceGrid.SizeChanged += OnProfessionalWorkspaceSizeChanged;
        DesktopChromePanel.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => SyncProfessionalRail());
        SyncProfessionalRail();
        MapText.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) => RefreshProfessionalWelcome());
        SelectionContextPanel.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => RefreshProfessionalWelcome());
        MainRoot.Loaded += (_, _) =>
        {
            RefreshProfessionalWelcome();
            _professionalInspectorReady = true;
            QueueProfessionalInspector();
        };
        InspectorTransformCard.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => QueueProfessionalInspector());
        InspectorTerrainFields.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => QueueProfessionalInspector());
        DesktopChromePanel.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => QueueProfessionalInspector());
        AssetLibraryToolWindow.RegisterPropertyChangedCallback(FrameworkElement.VerticalAlignmentProperty,
            (_, _) => ApplyProfessionalAssetTrayBounds(WorkspaceGrid.ActualHeight));
    }

    private void QueueProfessionalInspector()
    {
        if (!_professionalInspectorReady || _professionalInspectorQueued) return;
        _professionalInspectorQueued = true;
        if (!MainRoot.DispatcherQueue.TryEnqueue(() =>
        {
            _professionalInspectorQueued = false;
            if (!_professionalInspectorAutomatic || IsFullscreen()) return;
            var needed = InspectorTransformCard.Visibility == Visibility.Visible ||
                InspectorTerrainFields.Visibility == Visibility.Visible ||
                InspectorSelectionActionsCard.Visibility == Visibility.Visible;
            var visible = InspectorPanel.Visibility == Visibility.Visible && InspectorColumn.Width.Value > 0;
            if (visible != needed) ToggleInspectorPanel();
        })) _professionalInspectorQueued = false;
    }

    private void OnProfessionalAutoInspectorClick(object sender, RoutedEventArgs e)
    {
        _professionalInspectorAutomatic = ProfessionalAutoInspectorMenuItem.IsChecked;
        QueueProfessionalInspector();
    }

    private void ToggleProfessionalInspectorManually()
    {
        _professionalInspectorAutomatic = false;
        ProfessionalAutoInspectorMenuItem.IsChecked = false;
        ToggleInspectorPanel();
    }

    private void SyncProfessionalRail()
    {
        var visibility = DesktopChromePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in new[] { ModuleObjectsButton, ModuleRoadsButton, ModuleTerrainButton, ModuleTrafficButton, ModuleTransportButton })
            button.Visibility = visibility;
    }

    private bool PreferProfessionalAssetSidebar => WorkspaceGrid.ActualWidth > 0 && WorkspaceGrid.ActualWidth < 1250;

    private void RefreshProfessionalWelcome()
    {
        ProfessionalWelcomePanel.Visibility = _session.CurrentMap is null && SelectionContextPanel.Visibility == Visibility.Visible
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnProfessionalWorkspaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width > 0 && e.NewSize.Width < 1200;
        ExplorerColumn.MaxWidth = compact ? 250 : double.PositiveInfinity;
        InspectorColumn.MaxWidth = compact ? 255 : double.PositiveInfinity;
        ApplyProfessionalAssetTrayBounds(e.NewSize.Height);
        if (PreferProfessionalAssetSidebar && _assetLibraryDocked && HasHostedAssetLibrary && _libraryMode && !IsFullscreen())
        {
            CloseAssetLibraryToolWindow();
            ExplorerListView.Visibility = Visibility.Collapsed;
            if (ExplorerPanel.Visibility != Visibility.Visible) ToggleExplorerPanel();
            UpdateExplorerModeVisual(LibraryExplorerModeButton, "Assets e categorias");
        }
    }

    private void ApplyProfessionalAssetTrayBounds(double workspaceHeight)
    {
        AssetDockActionsScroll.MaxHeight = !HasHostedAssetLibrary && workspaceHeight > 0
            ? Math.Max(96, Math.Min(160, workspaceHeight * 0.28)) : double.PositiveInfinity;
        AssetLibraryToolWindow.MaxHeight = _assetLibraryDocked && workspaceHeight > 0
            ? Math.Max(155, Math.Min(285, workspaceHeight * 0.40))
            : double.PositiveInfinity;
    }

    private void SyncProfessionalLayers()
    {
        ProfessionalTerrainLayer.IsChecked = ShowTerrainMenuItem.IsChecked;
        ProfessionalObjectsLayer.IsChecked = ShowObjectsMenuItem.IsChecked;
        ProfessionalSplinesLayer.IsChecked = ShowSplinesMenuItem.IsChecked;
        ProfessionalGridLayer.IsChecked = ShowGridMenuItem.IsChecked;
        ProfessionalProfilesLayer.IsChecked = ShowSplineProfilesMenuItem.IsChecked;
        ProfessionalReferenceLayer.IsChecked = MapReferenceVisibilityMenuItem.IsChecked;
    }

    private void OnProfessionalLayerClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string tag } box) return;
        var value = box.IsChecked == true;
        switch (tag)
        {
            case "Terrain": ShowTerrainMenuItem.IsChecked = value; ApplySceneVisibility(); break;
            case "Objects": ShowObjectsMenuItem.IsChecked = value; ApplySceneVisibility(); break;
            case "Splines": ShowSplinesMenuItem.IsChecked = value; ApplySceneVisibility(); break;
            case "Grid": ShowGridMenuItem.IsChecked = value; OnGridVisibilityClick(sender, e); break;
            case "Profiles": ShowSplineProfilesMenuItem.IsChecked = value; OnSplineProfilesVisibilityClick(sender, e); break;
            case "Reference": MapReferenceVisibilityMenuItem.IsChecked = value; OnToggleMapReferenceVisibilityClick(sender, e); break;
        }
        SyncProfessionalLayers();
    }

    private void RefreshProfessionalMinimap(NativeMapSnapshot snapshot)
    {
        var canvas = ProfessionalMinimapCanvas;
        canvas.Children.Clear();
        var active = snapshot.ActiveTile;
        var centerX = active?.X ?? snapshot.Map.Tiles.FirstOrDefault()?.X ?? 0;
        var centerY = active?.Y ?? snapshot.Map.Tiles.FirstOrDefault()?.Y ?? 0;
        var loaded = snapshot.Tiles.Select(t => (t.Reference.X, t.Reference.Y)).ToHashSet();
        const int columns = 9;
        const int rows = 6;
        const int cell = 24;
        var visibleCount = 0;
        foreach (var tile in snapshot.Map.Tiles.GroupBy(t => (t.X, t.Y)).Select(g => g.First()))
        {
            var column = (long)tile.X - centerX + columns / 2;
            var row = (long)tile.Y - centerY + rows / 2;
            if (column < 0 || column >= columns || row < 0 || row >= rows) continue;
            visibleCount++;
            var isActive = tile.X == active?.X && tile.Y == active?.Y;
            var isLoaded = loaded.Contains((tile.X, tile.Y));
            var color = isActive ? Windows.UI.Color.FromArgb(255, 41, 151, 255)
                : isLoaded ? Windows.UI.Color.FromArgb(255, 62, 97, 85)
                : Windows.UI.Color.FromArgb(255, 57, 70, 84);
            var button = new Button
            {
                Width = cell - 2, Height = cell - 2, Padding = new Thickness(0),
                MinWidth = 0, MinHeight = 0, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color), Content = isActive ? "●" : "",
                Tag = (tile.X, tile.Y)
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"Tile {tile.X}, {tile.Y}");
            ToolTipService.SetToolTip(button, $"Tile {tile.X}, {tile.Y} · {(isActive ? "ativo" : isLoaded ? "carregado" : "não carregado")}");
            button.Click += OnProfessionalTileClick;
            Canvas.SetLeft(button, column * cell + 1);
            Canvas.SetTop(button, row * cell + 1);
            canvas.Children.Add(button);
        }
        ProfessionalMinimapStatus.Text = $"Tile {centerX}, {centerY} · {visibleCount} na região · {snapshot.Map.Tiles.Count} no mapa";
    }

    private async void OnProfessionalTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ValueTuple<int, int> tile })
        {
            await NavigateToTileAsync(tile.Item1, tile.Item2);
        }
    }

    private void OnGeoDockClick(object sender, RoutedEventArgs e)
    {
        _geoWorkspaceDocked = !_geoWorkspaceDocked;
        ApplyGeoWorkspaceLayout();
    }

    private void ApplyGeoWorkspaceLayout()
    {
        var docked = _geoWorkspaceDocked;
        if (docked && !_realMapAreaWindowMinimized &&
            RealMapAreaWindow.Visibility == Visibility.Visible &&
            AssetLibraryToolWindow.Visibility == Visibility.Visible)
        {
            CloseAssetLibraryToolWindow();
        }
        RealMapAreaWindow.Width = docked ? double.NaN : Math.Min(960, Math.Max(280, WorkspaceGrid.ActualWidth - 32));
        RealMapAreaWindow.Height = _realMapAreaWindowMinimized ? 70 : docked ? double.NaN : Math.Min(660, Math.Max(200, WorkspaceGrid.ActualHeight - 32));
        RealMapAreaWindow.Margin = docked ? new Thickness(0) : new Thickness(16, 16, 0, 0);
        RealMapAreaWindow.HorizontalAlignment = docked ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        RealMapAreaWindow.VerticalAlignment = docked && !_realMapAreaWindowMinimized ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        RealMapAreaWindowTranslate.X = 0;
        RealMapAreaWindowTranslate.Y = 0;
        RealMapAreaWizardTabs.Visibility = _realMapAreaWindowMinimized ? Visibility.Collapsed : Visibility.Visible;
        RealMapAreaResizeGrip.Visibility = docked || _realMapAreaWindowMinimized ? Visibility.Collapsed : Visibility.Visible;
        RealMapAreaMinimizeButton.Content = _realMapAreaWindowMinimized ? "□" : "—";
        GeoDockButton.Content = docked ? "Flutuar" : "Expandir";
    }

    private void OnGeoSourcesClick(object sender, RoutedEventArgs e) => RealMapAreaWizardTabs.SelectedIndex = 1;
    private void OnGeoReconstructionClick(object sender, RoutedEventArgs e) => RealMapAreaWizardTabs.SelectedIndex = 2;
}
