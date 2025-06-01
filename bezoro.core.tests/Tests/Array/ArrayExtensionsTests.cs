using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	public class ArrayExtensionsTests
	{
	#region IsEmpty

		[Test]
		public void IsEmpty_WhenArrayIsNull_ReturnsFalse()
		{
			int[]? ints = null;
			Assert.That(ints.IsEmpty(), Is.False);
		}

		[Test]
		public void IsEmpty_WhenArrayIsEmpty_ReturnsTrue()
		{
			var ints = new int[0];
			Assert.That(ints.IsEmpty(), Is.True);
		}

		[Test]
		public void IsEmpty_WhenArrayIsNotEmpty_ReturnsFalse()
		{
			var ints = new[] { 42 };
			Assert.That(ints.IsEmpty(), Is.False);
		}

	#endregion

	#region IsNotEmpty

		[Test]
		public void IsNotEmpty_WhenArrayIsNull_ReturnsFalse()
		{
			string[]? strings = null;
			Assert.That(strings.IsNotEmpty(), Is.False);
		}

		[Test]
		public void IsNotEmpty_WhenArrayIsEmpty_ReturnsFalse()
		{
			var strings = System.Array.Empty<string>();
			Assert.That(strings.IsNotEmpty(), Is.False);
		}

		[Test]
		public void IsNotEmpty_WhenArrayIsNotEmpty_ReturnsTrue()
		{
			var strings = new[] { "hello" };
			Assert.That(strings.IsNotEmpty(), Is.True);
		}

	#endregion

	#region IsNotNull / IsNull

		[Test]
		public void IsNotNull_WhenArrayIsNull_ReturnsFalse()
		{
			int[]? ints = null;
			Assert.That(ints.IsNotNull(), Is.False);
		}

		[Test]
		public void IsNotNull_WhenArrayIsInstantiated_ReturnsTrue()
		{
			var ints = new int[0];
			Assert.That(ints.IsNotNull(), Is.True);
		}

		[Test]
		public void IsNull_WhenArrayIsNull_ReturnsTrue()
		{
			int[]? ints = null;
			Assert.That(ints.IsNull(), Is.True);
		}

		[Test]
		public void IsNull_WhenArrayIsInstantiated_ReturnsFalse()
		{
			var ints = new int[1];
			Assert.That(ints.IsNull(), Is.False);
		}

	#endregion

	#region IsNotNullOrEmpty / IsNullOrEmpty

		[Test]
		public void IsNotNullOrEmpty_WhenArrayIsNull_ReturnsFalse()
		{
			int[]? ints = null;
			Assert.That(ints.IsNotNullOrEmpty(), Is.False);
		}

		[Test]
		public void IsNotNullOrEmpty_WhenArrayIsEmpty_ReturnsFalse()
		{
			var ints = new int[0];
			Assert.That(ints.IsNotNullOrEmpty(), Is.False);
		}

		[Test]
		public void IsNotNullOrEmpty_WhenArrayIsNotEmpty_ReturnsTrue()
		{
			var ints = new[] { 1, 2, 3 };
			Assert.That(ints.IsNotNullOrEmpty(), Is.True);
		}

		[Test]
		public void IsNullOrEmpty_WhenArrayIsNull_ReturnsTrue()
		{
			string[]? strings = null;
			Assert.That(strings.IsNullOrEmpty(), Is.True);
		}

		[Test]
		public void IsNullOrEmpty_WhenArrayIsEmpty_ReturnsTrue()
		{
			var strings = new string[0];
			Assert.That(strings.IsNullOrEmpty(), Is.True);
		}

		[Test]
		public void IsNullOrEmpty_WhenArrayIsNotEmpty_ReturnsFalse()
		{
			var strings = new[] { "value" };
			Assert.That(strings.IsNullOrEmpty(), Is.False);
		}

	#endregion
	}
}
