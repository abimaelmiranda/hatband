using System.Text;
using Microsoft.Extensions.Logging;

namespace Hatband.App.Services;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly Lock writeLock = new();
    private readonly StreamWriter writer;
    private bool isDisposed;

    public FileLoggerProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        writer = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new FileLogger(this, categoryName);
    }

    public void Dispose()
    {
        lock (writeLock)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            writer.Dispose();
        }
    }

    private void Write<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (writeLock)
        {
            if (isDisposed)
            {
                return;
            }

            writer.WriteLine(
                $"{DateTimeOffset.Now:O} [{logLevel}] {categoryName} (EventId: {eventId.Id}) {formatter(state, exception)}");
            if (exception is not null)
            {
                writer.WriteLine(exception);
            }
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _categoryName;

        public FileLogger(FileLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);
            _provider.Write(_categoryName, logLevel, eventId, state, exception, formatter);
        }
    }
}
