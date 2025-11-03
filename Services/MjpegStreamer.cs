using System.Buffers;
using System.Net.Http;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace IPCameraViewer.Services
{
	public sealed class MjpegStreamer : IAsyncDisposable
	{
		private const string UserAgentHeader = "IpCameraViewer/1.0 (MJPEG)";

		private readonly HttpClient httpClient;
		private CancellationTokenSource? cts;
		private Task? readTask;

		public event Action<byte[]>? FrameReceived;
		public event Action? MotionDetected;
		public event Action<string>? Error;
		public event Action<float,int,int>? Metrics; // ratio, changed, total pixels

		private SixLabors.ImageSharp.Image<Rgba32>? previousFrame;
		private readonly object stateLock = new();
		private readonly int downscaleWidth;
		private readonly int downscaleHeight;
		private readonly float differenceThresholdRatio;
		private readonly byte perChannelThreshold;
		private long lastDetectionMs;
		private readonly int cooldownMs;

		public MjpegStreamer(HttpClient httpClient,
			int downscaleWidth = 96,
			int downscaleHeight = 72,
			float differenceThresholdRatio = 0.015f,
			byte perChannelThreshold = 18,
			int cooldownMs = 2000)
		{
			this.httpClient = httpClient;
			this.downscaleWidth = downscaleWidth;
			this.downscaleHeight = downscaleHeight;
			this.differenceThresholdRatio = differenceThresholdRatio;
			this.perChannelThreshold = perChannelThreshold;
			this.cooldownMs = cooldownMs;
		}

		public void Start(string url)
		{
			this.cts?.Cancel();
			this.cts = new CancellationTokenSource();
			this.readTask = Task.Run(() => this.ReadLoopAsync(url, this.cts.Token));
		}

		public async ValueTask DisposeAsync()
		{
			this.cts?.Cancel();
			if (this.readTask != null)
			{
				try { await this.readTask.ConfigureAwait(false); } catch { }
			}
			this.cts?.Dispose();
			this.cts = null;
			lock (this.stateLock)
			{
				this.previousFrame?.Dispose();
				this.previousFrame = null;
			}
		}

		private async Task ReadLoopAsync(string url, CancellationToken cancellationToken)
		{
			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				request.Headers.TryAddWithoutValidation("User-Agent", MjpegStreamer.UserAgentHeader);
				using var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				response.EnsureSuccessStatusCode();

				await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
				var buffer = ArrayPool<byte>.Shared.Rent(1024 * 64);
				var readBuffer = new List<byte>(1024 * 256);
				try
				{
					while (!cancellationToken.IsCancellationRequested)
					{
						var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
						if (bytesRead <= 0) break;

						for (int i = 0; i < bytesRead; i++)
						{
							readBuffer.Add(buffer[i]);
						}

						while (true)
						{
							int soi = IndexOfPattern(readBuffer, 0xFF, 0xD8);
							if (soi < 0) { break; }
							int eoi = IndexOfPattern(readBuffer, 0xFF, 0xD9, soi + 2);
							if (eoi < 0) { break; }

							int length = (eoi + 2) - soi;
							var frameBytes = readBuffer.GetRange(soi, length).ToArray();
							readBuffer.RemoveRange(0, eoi + 2);

							OnFrame(frameBytes);
						}
					}
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex.Message);
			}
		}

		private static int IndexOfPattern(List<byte> data, byte b1, byte b2, int start = 0)
		{
			for (int i = start; i < data.Count - 1; i++)
			{
				if (data[i] == b1 && data[i + 1] == b2) return i;
			}
			return -1;
		}

		private void OnFrame(byte[] jpegBytes)
		{
			FrameReceived?.Invoke(jpegBytes);

			try
			{
				using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(jpegBytes);
				image.Mutate(ctx => ctx.Resize(this.downscaleWidth, this.downscaleHeight));

				bool fireMotion = false;
				float ratioForMetrics = 0f;
				int changedForMetrics = 0;
				int pixelCountForMetrics = 0;
				lock (this.stateLock)
				{
					if (this.previousFrame == null)
					{
						this.previousFrame = image.Clone();
						return;
					}

					int width = image.Width;
					int height = image.Height;
					int pixelCount = width * height;
					int changed = 0;

					var currentPixels = new Rgba32[pixelCount];
					var previousPixels = new Rgba32[pixelCount];
					image.CopyPixelDataTo(currentPixels);
					this.previousFrame!.CopyPixelDataTo(previousPixels);

					for (int i = 0; i < pixelCount; i++)
					{
						var c = currentPixels[i];
						var p = previousPixels[i];
						int dr = Math.Abs(c.R - p.R);
						int dg = Math.Abs(c.G - p.G);
						int db = Math.Abs(c.B - p.B);
						if (dr > this.perChannelThreshold || dg > this.perChannelThreshold || db > this.perChannelThreshold)
						{
							changed++;
						}
					}

					float ratio = (float)changed / pixelCount;
					var now = Environment.TickCount64;
					if (ratio >= this.differenceThresholdRatio && (now - this.lastDetectionMs) > this.cooldownMs)
					{
						this.lastDetectionMs = now;
						fireMotion = true;
					}

					this.previousFrame.Dispose();
					this.previousFrame = image.Clone();

					ratioForMetrics = ratio;
					changedForMetrics = changed;
					pixelCountForMetrics = pixelCount;
				}

				try { Metrics?.Invoke(ratioForMetrics, changedForMetrics, pixelCountForMetrics); } catch { }

				if (fireMotion)
				{
					MotionDetected?.Invoke();
				}
			}
			catch (Exception ex)
			{
				Error?.Invoke(ex.Message);
			}
		}
	}
}


