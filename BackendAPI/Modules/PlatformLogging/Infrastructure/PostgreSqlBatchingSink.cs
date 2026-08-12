using System.Text.Json;
using System.Threading.Channels;
using PlatformLogging.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace PlatformLogging.Infrastructure;

public sealed class PostgreSqlBatchingSink : ILogEventSink, IHostedService, IDisposable
{
	private readonly string _connectionString;
	private readonly PlatformLoggingOptions _options;
	private readonly Channel<LogEvent> _channel;
	private readonly CancellationTokenSource _stopping = new();
	private Task? _worker;

	public PostgreSqlBatchingSink(
		string connectionString,
		PlatformLoggingOptions options)
	{
		_connectionString = connectionString;
		_options = options;

		ValidateIdentifier(options.Schema);
		ValidateIdentifier(options.Table);

		_channel = Channel.CreateBounded<LogEvent>(
			new BoundedChannelOptions(Math.Max(100, options.BufferSize))
			{
				SingleReader = true,
				FullMode = BoundedChannelFullMode.DropWrite
			});
	}

	public void Emit(LogEvent logEvent)
	{
		if (!_channel.Writer.TryWrite(logEvent))
		{
			SelfLog.WriteLine(
				"PostgreSQL log buffer is full; event was dropped.");
		}
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_worker = ProcessAsync(_stopping.Token);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_channel.Writer.TryComplete();

		if (_worker is not null)
		{
			await _worker.WaitAsync(cancellationToken);
		}
	}

	private async Task ProcessAsync(CancellationToken cancellationToken)
	{
		var batch = new List<LogEvent>(Math.Max(1, _options.BatchSize));

		while (await _channel.Reader.WaitToReadAsync(cancellationToken))
		{
			batch.Clear();

			while (batch.Count < Math.Max(1, _options.BatchSize)
				&& _channel.Reader.TryRead(out var logEvent))
			{
				batch.Add(logEvent);
			}

			if (batch.Count == 0)
			{
				await Task.Delay(
					TimeSpan.FromSeconds(Math.Max(1, _options.FlushIntervalSeconds)),
					cancellationToken);
				continue;
			}

			try
			{
				await WriteAsync(batch, cancellationToken);
			}
			catch (Exception exception)
			{
				SelfLog.WriteLine(
					"PostgreSQL logging batch failed: {0}",
					exception);
			}
		}
	}

	private async Task WriteAsync(
		IReadOnlyList<LogEvent> batch,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var transaction =
			await connection.BeginTransactionAsync(cancellationToken);

		var table = $"\"{_options.Schema}\".\"{_options.Table}\"";

		foreach (var logEvent in batch)
		{
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = $"""
				INSERT INTO {table}
				(
					occurred_at, level, message_template, rendered_message,
					exception, properties, platform, application, environment,
					source_context, trace_id, request_id
				)
				VALUES
				(
					@time, @level, @template, @message,
					@exception, @properties::jsonb, @platform, @application,
					@environment, @source, @trace, @request
				)
				""";

			AddParameter(command, "time", logEvent.Timestamp);
			AddParameter(command, "level", logEvent.Level.ToString());
			AddParameter(command, "template", logEvent.MessageTemplate.Text);
			AddParameter(command, "message", logEvent.RenderMessage());
			AddParameter(command, "exception", logEvent.Exception?.ToString());
			AddParameter(command, "properties", SerializeProperties(logEvent));
			AddParameter(command, "platform", GetProperty(logEvent, "Platform") ?? "1CibiPlatform");
			AddParameter(command, "application", GetProperty(logEvent, "Application") ?? "Platform");
			AddParameter(command, "environment", GetProperty(logEvent, "Environment") ?? "Unknown");
			AddParameter(command, "source", GetProperty(logEvent, "SourceContext"));
			AddParameter(command, "trace", GetProperty(logEvent, "TraceId"));
			AddParameter(command, "request", GetProperty(logEvent, "RequestId"));

			await command.ExecuteNonQueryAsync(cancellationToken);
		}

		await transaction.CommitAsync(cancellationToken);
	}

	private static string SerializeProperties(LogEvent logEvent)
	{
		return JsonSerializer.Serialize(
			logEvent.Properties.ToDictionary(
				property => property.Key,
				property => property.Value.ToString()));
	}

	private static string? GetProperty(LogEvent logEvent, string name)
	{
		return logEvent.Properties.TryGetValue(name, out var value)
			? value.ToString().Trim('"')
			: null;
	}

	private static void AddParameter(
		NpgsqlCommand command,
		string name,
		object? value)
	{
		command.Parameters.AddWithValue(name, value ?? DBNull.Value);
	}

	private static void ValidateIdentifier(string value)
	{
		if (string.IsNullOrWhiteSpace(value)
			|| value.Any(character =>
				!char.IsLetterOrDigit(character) && character != '_'))
		{
			throw new InvalidOperationException("Invalid logging identifier.");
		}
	}

	public void Dispose()
	{
		_stopping.Cancel();
		_stopping.Dispose();
	}
}
