using System.Text;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain.Common.Helpers;

[TestSubject(typeof(BackgroundLoopManager))]
public class BackgroundLoopManagerTests
{
	[Fact]
	public async Task StartStderrLoopIfNeeded_WhenLinesAreRead_ShouldFilterEmptyLinesAndPreserveOrderOnThreadPool()
	{
		var received = new List<(string Line, bool IsThreadPoolThread)>();
		var errors   = new List<(Exception Exception, string Context)>();
		var manager = new BackgroundLoopManager(
			new ProcessUciTransportOptions(),
			(exception, context) => errors.Add((exception, context)),
			new BackgroundLoopMetrics(),
			line => received.Add((line, Thread.CurrentThread.IsThreadPoolThread))
		);
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes("first\n\nsecond\n"));
		using var reader = new StreamReader(stream, Encoding.UTF8);

		manager.StartStderrLoopIfNeeded(reader);
		await manager.AwaitStderrLoopAsync();

		received.Select(item => item.Line).Should().Equal("first", "second");
		received.Should().OnlyContain(item => item.IsThreadPoolThread);
		errors.Should().BeEmpty();
	}

	[Fact]
	public async Task StartStderrLoopIfNeeded_WhenHandlerThrows_ShouldContinueWithoutReportingError()
	{
		var received = new List<string>();
		var errors   = new List<(Exception Exception, string Context)>();
		var manager = new BackgroundLoopManager(
			new ProcessUciTransportOptions(),
			(exception, context) => errors.Add((exception, context)),
			new BackgroundLoopMetrics(),
			line =>
			{
				received.Add(line);
				if (line == "first") throw new InvalidOperationException("handler failure");
			}
		);
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes("first\nsecond\n"));
		using var reader = new StreamReader(stream, Encoding.UTF8);

		manager.StartStderrLoopIfNeeded(reader);
		await manager.AwaitStderrLoopAsync();

		received.Should().Equal("first", "second");
		errors.Should().BeEmpty();
	}

	[Fact]
	public async Task StartStderrLoopIfNeeded_WhenReaderHasInvalidUtf8_ShouldReportDecoderFailureWithContext()
	{
		var errors = new List<(Exception Exception, string Context)>();
		var manager = new BackgroundLoopManager(
			new ProcessUciTransportOptions(),
			(exception, context) => errors.Add((exception, context)),
			new BackgroundLoopMetrics()
		);
		using var stream = new MemoryStream([0xC3, 0x28]);
		using var reader = new StreamReader(stream, new UTF8Encoding(false, true));

		manager.StartStderrLoopIfNeeded(reader);
		await manager.AwaitStderrLoopAsync();

		errors.Should().ContainSingle();
		errors[0].Exception.Should().BeOfType<DecoderFallbackException>();
		errors[0].Context.Should().Be("Stderr loop faulted.");
	}
}
