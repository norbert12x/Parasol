using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ParasolBackEnd.Services
{
	public class ImportJobStatus
	{
		public bool IsRunning { get; set; }
		public string? Wojewodztwo { get; set; }
		public int TotalImported { get; set; }
		public int TotalDeletedFiles { get; set; }
		public int TotalErrors { get; set; }
		public DateTime? StartedAt { get; set; }
		public DateTime? LastBatchAt { get; set; }
		public string? LastError { get; set; }
	}

	public class ImportJobService
	{
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger<ImportJobService> _logger;
		private readonly object _lock = new();
		private CancellationTokenSource? _cts;
		private Task? _currentTask;
		private readonly int _batchLimit;

		public ImportJobStatus Status { get; } = new();

		public ImportJobService(IServiceScopeFactory scopeFactory, ILogger<ImportJobService> logger, int batchLimit = 45)
		{
			_scopeFactory = scopeFactory;
			_logger = logger;
			_batchLimit = Math.Min(Math.Max(batchLimit, 1), 45); // 1..45
		}

		public bool Start(string? wojewodztwo = null)
		{
			lock (_lock)
			{
				if (Status.IsRunning) return false;
				_cts = new CancellationTokenSource();
				Status.IsRunning = true;
				Status.Wojewodztwo = wojewodztwo;
				Status.TotalImported = 0;
				Status.TotalDeletedFiles = 0;
				Status.TotalErrors = 0;
				Status.StartedAt = DateTime.UtcNow;
				Status.LastBatchAt = null;
				Status.LastError = null;

				_currentTask = Task.Run(() => RunBatchesAsync(wojewodztwo!, _cts.Token));
				return true;
			}
		}

		public bool Stop()
		{
			lock (_lock)
			{
				if (!Status.IsRunning) return false;
				_cts?.Cancel();
				return true;
			}
		}

		private async Task RunBatchesAsync(string? wojewodztwo, CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					using var scope = _scopeFactory.CreateScope();
					var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();

					var result = await databaseService.ImportFromGeolokalizacjaAsync(wojewodztwo, _batchLimit);
					Status.TotalImported += result.ImportedCount;
					Status.TotalDeletedFiles += result.DeletedFiles.Count;
					Status.TotalErrors += result.Errors.Count;
					Status.LastBatchAt = DateTime.UtcNow;
					if (result.Errors.Count > 0)
					{
						Status.LastError = string.Join("; ", result.Errors);
						_logger.LogWarning("Import batch finished with {Errors} errors", result.Errors.Count);
					}

					// Warunek zakończenia: nic nie zostało zaimportowane ani usunięte
					if (result.ImportedCount == 0 && result.DeletedFiles.Count == 0)
					{
						_logger.LogInformation("No more records to import. Finishing job.");
						break;
					}

					// Krótka przerwa między batchami, żeby nie wpaść w limity
					await Task.Delay(TimeSpan.FromSeconds(2), ct);
				}
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Import job cancelled by user.");
			}
			catch (Exception ex)
			{
				Status.LastError = ex.Message;
				_logger.LogError(ex, "Import job crashed");
			}
			finally
			{
				lock (_lock)
				{
					Status.IsRunning = false;
					_cts?.Dispose();
					_cts = null;
					_currentTask = null;
				}
			}
		}
	}
}
