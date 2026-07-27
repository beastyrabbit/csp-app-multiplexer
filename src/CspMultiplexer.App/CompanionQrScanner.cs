// ═══ CSP SUITE SHARED FILE ══════════════════════════════════════════════════
// Reconcile with tools/suite-sync.ps1 (spec §0.1). Tier 2.
//   Companion : src/CspPaletteCompanion.App/CompanionQrScanner.cs
//   Mux       : src/CspMultiplexer.App/CompanionQrScanner.cs
// ════════════════════════════════════════════════════════════════════════════
using System.Drawing;
using System.Windows.Forms;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

// ── SYNC-LOCAL BEGIN ──
namespace CspMultiplexer.App;
// ── SYNC-LOCAL END ──

/// <summary>
/// Finds a companion URL in a QR code currently visible on any active display.
/// Screen capture happens only while <see cref="ScanAsync"/> is running and is
/// retained in memory only for the duration of a decode attempt.
/// </summary>
internal sealed class CompanionQrScanner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FastRetryInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan SustainedRetryInterval = TimeSpan.FromMilliseconds(900);

    private readonly Func<Uri, bool> _urlPredicate;

    internal CompanionQrScanner(Func<Uri, bool>? urlPredicate = null)
    {
        _urlPredicate = urlPredicate ?? (_ => true);
    }

    internal Task<Uri> ScanAsync(CancellationToken cancellationToken = default) =>
        ScanAsync(DefaultTimeout, cancellationToken);

    internal Task<Uri> ScanUntilFoundAsync(CancellationToken cancellationToken = default) =>
        ScanCoreAsync(null, cancellationToken);

    internal async Task<Uri> ScanAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The QR scan timeout must be a finite, positive duration.");
        }

        try
        {
            return await ScanCoreAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"No valid companion QR URL was found on any active display within {timeout.TotalSeconds:0.#} seconds.");
        }
    }

    private async Task<Uri> ScanCoreAsync(
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = timeout is null
            ? null
            : new CancellationTokenSource(timeout.Value);
        using var linkedSource = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        var attempt = 0;
        try
        {
            while (true)
            {
                linkedSource.Token.ThrowIfCancellationRequested();

                var uri = await Task.Run(
                    () => ScanDisplaysOnce(linkedSource.Token),
                    linkedSource.Token);
                if (uri is not null)
                {
                    return uri;
                }

                attempt++;
                var retryInterval = attempt <= 12
                    ? FastRetryInterval
                    : SustainedRetryInterval;
                await Task.Delay(retryInterval, linkedSource.Token);
            }
        }
        catch (OperationCanceledException) when (
            timeoutSource?.IsCancellationRequested == true &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    private Uri? ScanDisplaysOnce(CancellationToken cancellationToken)
    {
        var screens = Screen.AllScreens
            .Where(screen => screen.Bounds.Width > 0 && screen.Bounds.Height > 0)
            .ToArray();
        if (screens.Length == 0)
        {
            throw new InvalidOperationException(
                "Windows did not report any active displays to scan for a QR code.");
        }

        var captureErrors = new List<Exception>();
        foreach (var screen in screens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var uri = CaptureAndDecode(screen, cancellationToken);
                if (uri is not null)
                {
                    return uri;
                }
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException and
                not OperationCanceledException)
            {
                captureErrors.Add(new InvalidOperationException(
                    $"Could not capture display '{screen.DeviceName}' at " +
                    $"{screen.Bounds.Left},{screen.Bounds.Top} " +
                    $"({screen.Bounds.Width} × {screen.Bounds.Height}px).",
                    exception));
            }
        }

        if (captureErrors.Count == screens.Length)
        {
            throw new AggregateException(
                "QR scanning could not capture any active Windows display.",
                captureErrors);
        }

        return null;
    }

    private Uri? CaptureAndDecode(Screen screen, CancellationToken cancellationToken)
    {
        var bounds = screen.Bounds;
        using var screenshot = new Bitmap(
            bounds.Width,
            bounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(screenshot))
        {
            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                bounds.Size,
                CopyPixelOperation.SourceCopy);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var reader = new BarcodeReader
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE],
            },
        };

        var result = reader.Decode(screenshot);
        return TryAcceptUrl(result?.Text, out var uri) ? uri : null;
    }

    private bool TryAcceptUrl(string? text, out Uri? uri)
    {
        if (!string.IsNullOrWhiteSpace(text) &&
            Uri.TryCreate(text.Trim(), UriKind.Absolute, out var candidate) &&
            (candidate.Scheme == Uri.UriSchemeHttp ||
             candidate.Scheme == Uri.UriSchemeHttps) &&
            _urlPredicate(candidate))
        {
            uri = candidate;
            return true;
        }

        uri = null;
        return false;
    }
}
