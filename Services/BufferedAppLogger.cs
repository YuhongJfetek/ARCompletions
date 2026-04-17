using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public interface IBufferedAppLogger
    {
        ValueTask EnqueueLogAsync(string level, string message, object? props = null);
    }

    internal class LogItem
    {
        public string Level { get; set; } = "Information";
        public string Message { get; set; } = string.Empty;
        public object? Props { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }

    public class BufferedAppLogger : BackgroundService, IBufferedAppLogger
    {
        readonly ILogger<BufferedAppLogger> _logger;
        readonly BlockingCollection<LogItem> _queue;
        readonly IServiceProvider _serviceProvider;

        public BufferedAppLogger(ILogger<BufferedAppLogger> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _queue = new BlockingCollection<LogItem>(new ConcurrentQueue<LogItem>());
            _logger.LogInformation("BufferedAppLogger constructed");
        }

        public ValueTask EnqueueLogAsync(string level, string message, object? props = null)
        {
            if (_queue.IsAddingCompleted) return ValueTask.CompletedTask;
            try
            {
                _queue.Add(new LogItem { Level = level, Message = message, Props = props, TimeStamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue app log");
            }
            return ValueTask.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BufferedAppLogger ExecuteAsync starting");
            // yield once so Host startup can continue (avoids blocking other IHostedService startups)
            await Task.Yield();
            try
            {
                foreach (var item in _queue.GetConsumingEnumerable(stoppingToken))
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<ARCompletionsContext>();
                        var log = new AppLog
                        {
                            Id = Guid.NewGuid().ToString(),
                            TimeStamp = item.TimeStamp,
                            Level = item.Level,
                            Message = item.Message,
                            MessageTemplate = item.Message,
                            Properties = item.Props == null ? null : System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(item.Props))
                        };
                        db.AppLogs.Add(log);
                        await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "BufferedAppLogger: failed to persist log");
                    }

                    if (stoppingToken.IsCancellationRequested) break;
                }
            }
            catch (OperationCanceledException) { }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.CompleteAdding();
            return base.StopAsync(cancellationToken);
        }
    }
}
