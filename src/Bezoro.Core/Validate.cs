using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Bezoro.Core
{
	public static class Validate
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Get<T>(
			Func<T?> func,
			Exception? custom = null,
			string? message = null
		) =>
			InternalGet(func, custom, message);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task DoAsync(
			Func<Task> action,
			Exception? custom = null,
			string? message = null
		) =>
			InternalDoAsync(action, custom, message);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<T?> GetAsync<T>(
			Func<Task<T?>> func,
			Exception? custom = null,
			string? message = null
		) =>
			InternalGetAsync(func, custom, message);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Do(
			Action action,
			Exception? custom = null,
			string? message = null
		) =>
			InternalDo(action, custom, message);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static T? InternalGet<T>(
			Func<T> func,
			Exception? custom,
			string? msg
		)
		{
			if (func == null)
			{
				throw new ArgumentNullException(nameof(func));
			}

			try { return func(); }
			catch (Exception e) { Throw(custom, msg, e); }

			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task InternalDoAsync(
			Func<Task> func,
			Exception? custom,
			string? msg
		)
		{
			if (func == null)
			{
				throw new ArgumentNullException(nameof(func));
			}

			try { await func().ConfigureAwait(false); }
			catch (Exception e) { Throw(custom, msg, e); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task<T?> InternalGetAsync<T>(
			Func<Task<T>> func,
			Exception? custom,
			string? msg
		)
		{
			if (func == null)
			{
				throw new ArgumentNullException(nameof(func));
			}

			try { return await func().ConfigureAwait(false); }
			catch (Exception e) { Throw(custom, msg, e); }

			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void InternalDo(
			Action action,
			Exception? custom,
			string? msg
		)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			try { action(); }
			catch (Exception e) { Throw(custom, msg, e); }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Throw(Exception? custom, string? msg, Exception original)
		{
			if (custom != null)
			{
				throw custom;
			}

			if (!string.IsNullOrEmpty(msg))
			{
				throw new(msg, original);
			}

			throw original;
		}
	}
}
