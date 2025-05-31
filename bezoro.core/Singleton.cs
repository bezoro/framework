using System;

namespace Bezoro.Core
{
	public abstract class Singleton<T> where T : class, new()
	{
		protected Singleton()
		{
			// Prevent instantiation via reflection, if desired
			if (_INSTANCE.IsValueCreated) throw new InvalidOperationException("Instance already created");
		}

		private static readonly Lazy<T> _INSTANCE = new(() => new());

		public static T Instance => _INSTANCE.Value;
	}
}
