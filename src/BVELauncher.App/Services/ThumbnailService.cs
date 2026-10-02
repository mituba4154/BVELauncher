using System.Security.Cryptography;
using System.Text;
using BVELauncher.Application;
using BVELauncher.Infrastructure;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace BVELauncher.App.Services;

public sealed class ThumbnailService(AppPaths paths, ILocalLog log)
{
    private readonly SemaphoreSlim gate = new(2, 2);
    public async Task<string?> GetAsync(string? imagePath, CancellationToken cancellationToken)
    {
        if (imagePath == null) return null;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(async () =>
            {
                var info = new FileInfo(imagePath); if (!info.Exists || info.Length > 128 * 1024 * 1024) return null;
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(info.FullName.ToUpperInvariant() + info.LastWriteTimeUtc.Ticks + info.Length)));
                string target = Path.Combine(paths.Cache, key + ".png"); if (File.Exists(target)) return target;
                var file = await StorageFile.GetFileFromPathAsync(imagePath);
                using var stream = await file.OpenReadAsync();
                var decoder = await BitmapDecoder.CreateAsync(stream);
                if ((long)decoder.PixelWidth * decoder.PixelHeight > 100_000_000) return null;
                double scale = Math.Min(1, 400.0 / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
                uint width = Math.Max(1, (uint)(decoder.PixelWidth * scale)), height = Math.Max(1, (uint)(decoder.PixelHeight * scale));
                using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { ScaledWidth = width, ScaledHeight = height }, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                cancellationToken.ThrowIfCancellationRequested();
                string temporary = target + ".tmp";
                var folder = await StorageFolder.GetFolderFromPathAsync(paths.Cache);
                var output = await folder.CreateFileAsync(Path.GetFileName(temporary), CreationCollisionOption.ReplaceExisting);
                using (var outputStream = await output.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream); encoder.SetSoftwareBitmap(bitmap); await encoder.FlushAsync();
                }
                File.Move(temporary, target, true); return target;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { log.Write("Warning", "Thumbnail unavailable.", ex); return null; }
        finally { gate.Release(); }
    }
}
