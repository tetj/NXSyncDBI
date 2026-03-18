namespace CopyUpdates
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class FileDownloader
    {
        public event EventHandler<DownloadProgressEventArgs> ProgressChanged;

        public class DownloadProgressEventArgs : EventArgs
        {
            public long BytesReceived { get; }
            public long TotalBytes { get; }
            public double ProgressPercentage { get; }

            public DownloadProgressEventArgs(long bytesReceived, long totalBytes)
            {
                BytesReceived = bytesReceived;
                TotalBytes = totalBytes;
                ProgressPercentage = totalBytes > 0 ? (double)bytesReceived / totalBytes * 100 : 0;
            }
        }

        public static async Task Download()
        {
            var downloader = new FileDownloader();
            var url = "https://github.com/blawar/titledb/blob/master/US.en.json";
            var destinationPath = "US.en.json";

            downloader.ProgressChanged += (sender, e) =>
            {
                Debug.WriteLine($"Downloaded {e.BytesReceived:N0} of {e.TotalBytes:N0} bytes. " +
                                $"Progress: {e.ProgressPercentage:N2}%");
            };

            try
            {
                Debug.WriteLine($"Starting download from {url}");
                await downloader.DownloadFileAsync(url, destinationPath);
                Debug.WriteLine("Download completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
        {
            // For GitHub raw content, modify the URL
            if (url.Contains("github.com") && url.Contains("/blob/"))
            {
                url = url.Replace("github.com", "raw.githubusercontent.com")
                         .Replace("/blob/", "/");
            }

            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    _ = Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new ArgumentNullException(nameof(destinationPath)));

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var totalBytesRead = 0L;
                        var bytesRead = 0;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalBytesRead += bytesRead;

                            ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(totalBytesRead, totalBytes));
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"Failed to download file: {ex.Message}", ex);
                }
                catch (IOException ex)
                {
                    throw new Exception($"Failed to save file: {ex.Message}", ex);
                }
                catch (OperationCanceledException)
                {
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    throw;
                }
            }
        }
    }
}
