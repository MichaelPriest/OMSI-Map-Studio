using System.Diagnostics;
using MapStudio.Core.Commercial;
using MapStudio.Native.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MapStudio.Native.Dialogs;

internal static class NativeLicenseDialog
{
    public static async Task<NativeLicenseSnapshot>
        ShowAsync(
            XamlRoot xamlRoot,
            NativeLicenseService service)
    {
        ArgumentNullException.ThrowIfNull(
            xamlRoot);

        ArgumentNullException.ThrowIfNull(
            service);

        var latest =
            service
                .LoadCachedSnapshot();

        var serialBox =
            new TextBox
            {
                Header =
                    "Serial",
                PlaceholderText =
                    "OMS-MS-A5-XXXX-XXXX-XXXX",
                Text =
                    latest.Serial ??
                    string.Empty
            };

        var stateText =
            CreateStatusText();

        var subscriptionText =
            CreateStatusText();

        var validityText =
            CreateStatusText();

        var planText =
            CreateStatusText();

        var devicesText =
            CreateStatusText();

        var offlineText =
            CreateStatusText();

        var deviceIdText =
            CreateStatusText();

        deviceIdText.IsTextSelectionEnabled =
            true;

        var info =
            new InfoBar
            {
                IsOpen =
                    true,
                IsClosable =
                    false,
                Severity =
                    latest.SignatureVerificationConfigured
                        ? InfoBarSeverity
                            .Informational
                        : InfoBarSeverity
                            .Warning
            };

        var activateButton =
            new Button
            {
                Content =
                    "Ativar",
                MinWidth =
                    110
            };

        var verifyButton =
            new Button
            {
                Content =
                    "Verificar licença",
                MinWidth =
                    140
            };

        var subscriberButton =
            new Button
            {
                Content =
                    "Abrir área do assinante",
                MinWidth =
                    170
            };

        var billingButton =
            new Button
            {
                Content =
                    "Gerenciar assinatura",
                MinWidth =
                    165
            };

        var activationButtons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,
                Spacing =
                    8
            };

        activationButtons.Children.Add(
            activateButton);

        activationButtons.Children.Add(
            verifyButton);

        var portalButtons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,
                Spacing =
                    8
            };

        portalButtons.Children.Add(
            subscriberButton);

        portalButtons.Children.Add(
            billingButton);

        var statusCard =
            new Border
            {
                Padding =
                    new Thickness(
                        12),
                CornerRadius =
                    new CornerRadius(
                        8),
                Background =
                    (Microsoft.UI.Xaml.Media.Brush)
                    Application.Current.Resources[
                        "MapStudioSurfaceAltBrush"]
            };

        var statusStack =
            new StackPanel
            {
                Spacing =
                    5
            };

        statusStack.Children.Add(
            stateText);

        statusStack.Children.Add(
            subscriptionText);

        statusStack.Children.Add(
            validityText);

        statusStack.Children.Add(
            planText);

        statusStack.Children.Add(
            devicesText);

        statusStack.Children.Add(
            offlineText);

        statusStack.Children.Add(
            deviceIdText);

        statusCard.Child =
            statusStack;

        var panel =
            new StackPanel
            {
                Spacing =
                    12,
                MinWidth =
                    560
            };

        panel.Children.Add(
            new TextBlock
            {
                Text =
                    "Ative o OMSI Map Studio com o serial da sua assinatura. O desktop guarda apenas o token assinado e a chave pública de verificação; nenhuma chave secreta da Stripe é armazenada no aplicativo.",
                TextWrapping =
                    TextWrapping.Wrap
            });

        panel.Children.Add(
            statusCard);

        panel.Children.Add(
            serialBox);

        panel.Children.Add(
            activationButtons);

        panel.Children.Add(
            portalButtons);

        panel.Children.Add(
            info);

        var scroll =
            new ScrollViewer
            {
                Content =
                    panel,
                MaxHeight =
                    640,
                HorizontalScrollMode =
                    ScrollMode.Disabled,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                VerticalScrollMode =
                    ScrollMode.Auto,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    xamlRoot,
                Title =
                    "Assinatura e licença",
                Content =
                    scroll,
                CloseButtonText =
                    "Fechar",
                DefaultButton =
                    ContentDialogButton
                        .Close
            };

        void Render(
            NativeLicenseSnapshot snapshot,
            string? message = null,
            InfoBarSeverity? severity = null)
        {
            latest =
                snapshot;

            stateText.Text =
                $"Licença: {FormatStatus(snapshot.State.Status)}";

            subscriptionText.Text =
                $"Assinatura: {FormatSubscription(snapshot.SubscriptionStatus)}";

            validityText.Text =
                snapshot.SubscriptionExpiresAt is
                    { } subscriptionExpires
                    ? $"Validade da assinatura: {subscriptionExpires.ToLocalTime():g}"
                    : "Validade da assinatura: —";

            planText.Text =
                $"Plano: {snapshot.PlanId ?? "Public Alpha · ID do preço ainda não incluído no token"}";

            var maxDevices =
                snapshot.MaxDevices
                    ?.ToString() ??
                "2";

            devicesText.Text =
                snapshot.DeviceCount is
                    { } deviceCount
                    ? $"Dispositivos: {deviceCount}/{maxDevices}"
                    : $"Dispositivos: este PC · limite inicial {maxDevices}";

            offlineText.Text =
                snapshot.OfflineUntil is
                    { } offlineUntil
                    ? snapshot.IsOffline
                        ? $"Status offline: grace period até {offlineUntil.ToLocalTime():g}"
                        : $"Status offline: token renovado até {offlineUntil.ToLocalTime():g}"
                    : "Status offline: sem token válido.";

            deviceIdText.Text =
                $"Este computador: {snapshot.DeviceName} · {snapshot.DeviceId}";

            info.Message =
                message ??
                snapshot.Message;

            info.Severity =
                severity ??
                (
                    snapshot.State.Status is
                        MapStudioLicenseStatus.Active or
                        MapStudioLicenseStatus.GracePeriod
                        ? InfoBarSeverity
                            .Success
                        : snapshot.State.Status ==
                            MapStudioLicenseStatus
                                .DevelopmentPreview
                            ? InfoBarSeverity
                                .Warning
                            : InfoBarSeverity
                                .Informational
                );

            info.IsOpen =
                true;

            if (
                string.IsNullOrWhiteSpace(
                    serialBox.Text) &&
                !string.IsNullOrWhiteSpace(
                    snapshot.Serial))
            {
                serialBox.Text =
                    snapshot.Serial;
            }
        }

        async Task VerifyAsync()
        {
            activateButton.IsEnabled =
                false;

            verifyButton.IsEnabled =
                false;

            info.Severity =
                InfoBarSeverity
                    .Informational;

            info.Message =
                "Verificando licença no servidor...";

            try
            {
                var serial =
                    string.IsNullOrWhiteSpace(
                        serialBox.Text)
                        ? latest.Serial
                        : serialBox.Text;

                var result =
                    await service
                        .ActivateAsync(
                            serial);

                Render(
                    result.Snapshot,
                    result.Message,
                    result.Success
                        ? InfoBarSeverity
                            .Success
                        : InfoBarSeverity
                            .Error);
            }
            finally
            {
                activateButton.IsEnabled =
                    true;

                verifyButton.IsEnabled =
                    true;
            }
        }

        activateButton.Click +=
            async (_, _) =>
            {
                await VerifyAsync();
            };

        verifyButton.Click +=
            async (_, _) =>
            {
                await VerifyAsync();
            };

        subscriberButton.Click +=
            (_, _) =>
            {
                OpenSite(
                    "/conta",
                    info);
            };

        billingButton.Click +=
            (_, _) =>
            {
                OpenSite(
                    "/conta#assinatura",
                    info);
            };

        var hasSite =
            NativeCommerceEndpoint
                .ResolveApiBaseUri() is not
                null;

        subscriberButton.IsEnabled =
            hasSite;

        billingButton.IsEnabled =
            hasSite;

        Render(
            latest);

        await dialog
            .ShowAsync();

        return latest;
    }

    private static TextBlock CreateStatusText() =>
        new()
        {
            TextWrapping =
                TextWrapping.Wrap
        };

    private static string FormatStatus(
        MapStudioLicenseStatus status) =>
        status switch
        {
            MapStudioLicenseStatus.Active =>
                "ativa",
            MapStudioLicenseStatus.Trial =>
                "teste",
            MapStudioLicenseStatus.GracePeriod =>
                "offline / grace period",
            MapStudioLicenseStatus.PastDue =>
                "pagamento pendente",
            MapStudioLicenseStatus.Expired =>
                "expirada",
            MapStudioLicenseStatus.Revoked =>
                "revogada",
            MapStudioLicenseStatus.Unavailable =>
                "não disponível",
            _ =>
                "pré-lançamento"
        };

    private static string FormatSubscription(
        string? status) =>
        status
            ?.ToLowerInvariant() switch
        {
            "active" =>
                "ativa",
            "trialing" =>
                "período de teste",
            "past_due" =>
                "pagamento pendente",
            null or "" =>
                "—",
            _ =>
                status
        };

    private static void OpenSite(
        string relativePath,
        InfoBar info)
    {
        var baseUri =
            NativeCommerceEndpoint
                .ResolveApiBaseUri();

        if (baseUri is null)
        {
            info.Severity =
                InfoBarSeverity
                    .Warning;

            info.Message =
                "O endereço do site ainda não está configurado nesta build.";

            info.IsOpen =
                true;

            return;
        }

        try
        {
            var uri =
                new Uri(
                    baseUri,
                    relativePath);

            Process.Start(
                new ProcessStartInfo(
                    uri.AbsoluteUri)
                {
                    UseShellExecute =
                        true
                });
        }
        catch (Exception exception)
        {
            info.Severity =
                InfoBarSeverity
                    .Error;

            info.Message =
                $"Não foi possível abrir o navegador: {exception.Message}";

            info.IsOpen =
                true;
        }
    }
}
