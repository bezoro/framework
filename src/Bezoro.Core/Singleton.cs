using System;

namespace Bezoro.Core
{
	public abstract class Singleton<T> where T : class
	{
		private static readonly Lazy<T> _INSTANCE = new(
			() =>
			{
				try
				{
					return (T)Activator.CreateInstance(typeof(T), true);
				}
				catch (MissingMethodException ex)
				{
					throw new TypeInitializationException(
						$"The type {typeof(T).FullName} must have a private or protected parameterless constructor to be used with StrictSingleton<T>.",
						ex);
				}
			});

		protected Singleton()
		{
			if (!_INSTANCE.IsValueCreated || ReferenceEquals(_INSTANCE.Value, this))
			{
				return;
			}

			// Prevent instantiation via reflection, if desired
			if (_INSTANCE.IsValueCreated)
			{
				throw new InvalidOperationException(
					$"Cannot create a second instance of {typeof(T).Name}. Use {typeof(T).Name}.Instance instead.");
			}
		}

		public static T Instance => _INSTANCE.Value;
	}
}
