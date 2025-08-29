using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions.Collections.Process;
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
					processedElements.Add(element);
					return Task.CompletedTask;
				},
				CancellationToken.None);

			// Assert
			processedElements.Should().BeEquivalentTo("1", "3", "4");
		}
	}
}
