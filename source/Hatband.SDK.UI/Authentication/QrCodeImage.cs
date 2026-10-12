using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using QRCoder;

namespace Hatband.SDK.UI.Authentication;

/// <summary>Renders a URI as a QR code image for plugin authentication flows.</summary>
public sealed class QrCodeImage : Image
{
    public static readonly StyledProperty<Uri?> UriProperty =
        AvaloniaProperty.Register<QrCodeImage, Uri?>(nameof(Uri));

    public Uri? Uri
    {
        get => GetValue(UriProperty);
        set => SetValue(UriProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UriProperty)
        {
            UpdateSource(change.GetNewValue<Uri?>());
        }
    }

    private void UpdateSource(Uri? uri)
    {
        var previous = Source as Bitmap;
        Source = uri is null ? null : CreateBitmap(uri);
        previous?.Dispose();
    }

    private static Bitmap CreateBitmap(Uri uri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri.AbsoluteUri, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        using var stream = new MemoryStream(renderer.GetGraphic(8));
        return new Bitmap(stream);
    }
}
