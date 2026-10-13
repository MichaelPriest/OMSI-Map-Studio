using System.Globalization;
using System.Numerics;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using MapStudio.Renderer.Viewport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native;

public sealed partial class MainWindow
{
    private const string KeepOriginalTextureLabel =
        "— Manter textura original —";

    private void OnRefreshVisualTexturesClick(
        object sender,
        RoutedEventArgs e)
    {
        var textures = Viewport.GetAvailableVisualTexturePaths(
            limit: 2000);

        VisualVariantTextureComboBox.ItemsSource =
            new[] { KeepOriginalTextureLabel }
                .Concat(textures)
                .ToArray();

        VisualVariantTextureComboBox.SelectedIndex = 0;
        VisualVariantStatusTextBlock.Text =
            textures.Count == 0
                ? "Nenhuma textura encontrada. Confirme se a pasta do OMSI 2 está carregada."
                : $"{textures.Count} texturas encontradas. Selecione uma para substituir o material escolhido.";
    }

    private void OnApplyVisualVariantClick(
        object sender,
        RoutedEventArgs e)
    {
        var selected = Viewport.GetSelectedSelectionInfos()
            .FirstOrDefault(info => info.Kind == PickingKind.Object);

        if (selected is null)
        {
            VisualVariantStatusTextBlock.Text =
                "Selecione um objeto de cenário no mapa antes de aplicar a variante.";
            return;
        }

        var textureSelection =
            VisualVariantTextureComboBox.SelectedItem as string;

        var overrides = new Dictionary<int, string>();
        if (
            !string.IsNullOrWhiteSpace(textureSelection) &&
            !string.Equals(
                textureSelection,
                KeepOriginalTextureLabel,
                StringComparison.Ordinal))
        {
            if (
                !int.TryParse(
                    VisualVariantMaterialIndexTextBox.Text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var materialIndex) ||
                materialIndex < 0)
            {
                VisualVariantStatusTextBlock.Text =
                    "Informe um índice de material válido (0 ou maior) para trocar a textura.";
                return;
            }

            if (!File.Exists(textureSelection))
            {
                VisualVariantStatusTextBlock.Text =
                    "A textura selecionada não existe mais. Atualize a lista e tente novamente.";
                return;
            }

            overrides[materialIndex] = textureSelection;
        }

        Vector4? tint = null;
        var tintText = VisualVariantTintTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(tintText))
        {
            if (!TryParseVisualTint(tintText, out var parsedTint))
            {
                VisualVariantStatusTextBlock.Text =
                    "Cor inválida. Use #RRGGBB ou #RRGGBBAA, por exemplo #2468a2.";
                return;
            }

            tint = parsedTint;
        }

        var variantName = string.IsNullOrWhiteSpace(
            VisualVariantNameTextBox.Text)
            ? "Personalizado"
            : VisualVariantNameTextBox.Text.Trim();

        try
        {
            if (!Viewport.ApplyVisualVariantToSelection(
                    selected,
                    variantName,
                    overrides,
                    tint))
            {
                VisualVariantStatusTextBlock.Text =
                    "Não foi possível aplicar a variante ao item selecionado.";
                return;
            }

            VisualVariantStatusTextBlock.Text =
                $"Variante “{variantName}” aplicada ao objeto #{selected.EntityId} no bloco ({selected.TileX}, {selected.TileY}).";
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            VisualVariantStatusTextBlock.Text =
                $"Não foi possível aplicar a variante: {exception.Message}";
        }
    }

    private void OnResetVisualVariantClick(
        object sender,
        RoutedEventArgs e)
    {
        var selected = Viewport.GetSelectedSelectionInfos()
            .FirstOrDefault(info => info.Kind == PickingKind.Object);

        if (selected is null)
        {
            VisualVariantStatusTextBlock.Text =
                "Selecione um objeto de cenário no mapa para restaurar sua aparência.";
            return;
        }

        Viewport.ResetVisualVariantForSelection(selected);
        VisualVariantNameTextBox.Text = "Original";
        VisualVariantTintTextBox.Text = string.Empty;
        VisualVariantMaterialIndexTextBox.Text = string.Empty;
        VisualVariantTextureComboBox.SelectedIndex = 0;
        VisualVariantStatusTextBlock.Text =
            $"A aparência original do objeto #{selected.EntityId} foi restaurada nesta sessão.";
    }

    private static bool TryParseVisualTint(
        string value,
        out Vector4 tint)
    {
        tint = Vector4.One;
        var hex = value.StartsWith('#') ? value[1..] : value;

        if (
            hex.Length is not (6 or 8) ||
            !uint.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var packed))
        {
            return false;
        }

        var red = (packed >> (hex.Length == 8 ? 24 : 16)) & 0xff;
        var green = (packed >> (hex.Length == 8 ? 16 : 8)) & 0xff;
        var blue = (packed >> (hex.Length == 8 ? 8 : 0)) & 0xff;
        var alpha = hex.Length == 8 ? packed & 0xff : 0xff;

        tint = new Vector4(
            red / 255f,
            green / 255f,
            blue / 255f,
            alpha / 255f);
        return true;
    }
}
