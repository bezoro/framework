using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions.Collections.Process;
using Bezoro.Core.Common.Interfaces;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.Collections.Process;

[TestSubject(typeof(ArrayProcess))]
public static class ArrayProcessTests
{
	public class Unit
	{
		[Fact]
		public async Task Process_Should_Stop_When_Cancelled()
		{
			// Arrange
			int[,]    array             = new[,] { { 1, 2 }, { 3, 4 } };
			var       processedElements = new List<int>();
			using var cts               = new CancellationTokenSource();

			// Act
			await array.Process(
				async (element, _, _, ct) =>
				{
					if (element == 3) await cts.CancelAsync();
					await Task.Delay(1, ct);
					processedElements.Add(element);
				},
				cts.Token);

			// Assert
			processedElements.Should().BeEquivalentTo([1, 2]);
		}

		[Fact]
		public async Task Process_ShouldProcess_AllElements()
		{
			// Arrange
			int[,] array             = new[,] { { 1, 2 }, { 3, 4 } };
			var    processedElements = new List<int>();

			// Act
			await array.Process(
				(element, _, _, _) =>
				{
					processedElements.Add(element);
					return Task.CompletedTask;
				},
				CancellationToken.None);

			// Assert
			processedElements.Should().BeEquivalentTo([1, 2, 3, 4]);
		}

		[Fact]
		public async Task Process_ShouldSkip_NullElements()
		{
			// Arrange
			string?[,] array             = new[,] { { "1", null }, { "3", "4" } };
			var        processedElements = new List<string>();

			// Act
			await array.Process(
				(element, _, _, _) =>
				{
					processedElements.Add(element!);
					return Task.CompletedTask;
				},
				CancellationToken.None);

			// Assert
			processedElements.Should().BeEquivalentTo("1", "3", "4");
		}

		[Fact]
		public async Task ProcessArrayAsync_Should_ProcessAllNonNullElements()
		{
			// Arrange
			string?[] array             = ["1", null, "3", "4"];
			var       processedElements = new List<string>();

			// Act
			await array.ProcessArrayAsync(element =>
			{
				processedElements.Add(element);
				return Task.CompletedTask;
			});

			// Assert
			processedElements.Should().BeEquivalentTo("1", "3", "4");
		}

		[Fact]
		public async Task ProcessArrayAsync_Should_ThrowArgumentNullException_WhenArrayIsNull()
		{
			// Arrange
			string?[] array = null!;

			// Act
			var act = () => array.ProcessArrayAsync(_ => Task.CompletedTask);

			// Assert
			await act.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public void ProcessArray_Should_ProcessAllNonNullElements()
		{
			// Arrange
			string?[] array             = ["1", null, "3", "4"];
			var       processedElements = new List<string>();

			// Act
			array.ProcessArray(element => processedElements.Add(element));

			// Assert
			processedElements.Should().BeEquivalentTo("1", "3", "4");
		}

		[Fact]
		public void ProcessArray_Should_ProcessAllNonNullElements_WithIProcessable()
		{
			// Arrange
			var items = new[]
			{
				new ProcessableItem(),
				null,
				new ProcessableItem()
			};

			// Act
			items.ProcessArray();

			// Assert
			items[0]!.WasProcessed.Should().BeTrue();
			items[2]!.WasProcessed.Should().BeTrue();
		}

		[Fact]
		public void ProcessArray_Should_ThrowArgumentNullException_WhenArrayIsNull()
		{
			// Arrange
			string?[] array = null!;

			// Act
			var act = () => array.ProcessArray(_ => { });

			// Assert
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void ProcessArray_Should_ThrowArgumentNullException_WhenArrayIsNull_WithIProcessable()
		{
			// Arrange
			ProcessableItem?[] array = null!;

			// Act
			var act = () => array.ProcessArray();

			// Assert
			act.Should().Throw<ArgumentNullException>();
		}
	}

	private class ProcessableItem : IProcessable
	{
		public bool WasProcessed { get; private set; }

		public void Process()
		{
			WasProcessed = true;
		}
	}
}
