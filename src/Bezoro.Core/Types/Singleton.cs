namespace Bezoro.Core.Types;

/// <summary>
///     A robust, thread-safe, and highly flexible Singleton base class.
///     Features:
///     - Lazy, thread-safe publication of the default instance
///     - Configurable creation factory (before or with reinitialization)
///     - Scoped runtime overrides for testing or special scenarios
///     - Reset support with optional disposal of existing instances
///     - Safe TryGet and state inspection helpers
///     - Protective construction checks to prevent unauthorized instantiation
/// </summary>
/// <typeparam name="T">The singleton's concrete type. Must be a reference type.</typeparam>
public abstract class Singleton<T> where T : class
{
	private static readonly object  _sync = new();
	private static          int     _initializing;
	private static          Func<T> _factory = DefaultFactory;


	// The current creation factory; defaults to reflective parameterless-ctor creation


	// Guards construction so derived types can only be instantiated through controlled factory paths


	// Primary lazy instance
	private static Lazy<T> _lazy = NewLazy();

	// Optional runtime override instance
	private static T? _overrideInstance;

	/// <summary>
	///     Initializes a new instance of the singleton type.
	///     Direct construction is disallowed; instances must be created via the internal guarded factory.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when constructed outside allowed guarded paths.</exception>
	protected Singleton()
	{
		// Allow construction only during guarded factory invocation
		if (_initializing == 1)
			return;

		throw new InvalidOperationException(
			$"Direct construction of {typeof(T).FullName} is not allowed. " +
			$"Use {typeof(T).FullName}.{nameof(Instance)} or configure a custom factory.");
	}

	/// <summary>
	///     Initializes the singleton by eagerly creating and setting an override instance using the provided factory.
	///     Fails if an instance already exists or an override is active.
	/// </summary>
	/// <param name="factory">Factory used to create the override instance.</param>
	/// <exception cref="ArgumentNullException">When factory is null.</exception>
	/// <exception cref="InvalidOperationException">When the singleton is already created or overridden.</exception>
	public static void Initialize(Func<T> factory)
	{
		if (factory is null) throw new ArgumentNullException(nameof(factory));

		lock (_sync)
		{
			if (IsValueCreated || _overrideInstance is { })
				throw new InvalidOperationException("The singleton has already been created or overridden.");

			_overrideInstance = CreateWithGuard(factory);
		}
	}

	/// <summary>
	///     Indicates whether an override instance is currently active.
	/// </summary>
	public static bool IsOverridden => Volatile.Read(ref _overrideInstance) is { };

	/// <summary>
	///     Indicates whether the singleton instance has been created (either override or default).
	/// </summary>
	public static bool IsValueCreated
	{
		get
		{
			if (Volatile.Read(ref _overrideInstance) is { }) return true;

			return _lazy.IsValueCreated;
		}
	}

	/// <summary>
	///     Gets the active singleton instance: an override if available, otherwise the lazily created one.
	/// </summary>
	public static T Instance
	{
		get
		{
			var o = Volatile.Read(ref _overrideInstance);
			if (o is { }) return o;

			return _lazy.Value;
		}
	}

	/// <summary>
	///     Attempts to get the current instance without forcing initialization.
	/// </summary>
	/// <param name="instance">Receives the instance if already created or overridden; otherwise null.</param>
	/// <returns>True if an instance is available; false otherwise.</returns>
	public static bool TryGet(out T? instance)
	{
		var o = Volatile.Read(ref _overrideInstance);
		if (o is { })
		{
			instance = o;
			return true;
		}

		if (_lazy.IsValueCreated)
		{
			instance = _lazy.Value;
			return true;
		}

		instance = null;
		return false;
	}

	/// <summary>
	///     Temporarily overrides the singleton instance with a new instance produced by <paramref name="factory" />.
	///     Returns a disposable scope that restores the previous state upon disposal.
	/// </summary>
	/// <param name="factory">Factory used to create the override instance.</param>
	/// <returns>An IDisposable scope that restores the previous instance on Dispose.</returns>
	/// <exception cref="ArgumentNullException">When factory is null.</exception>
	public static IDisposable Override(Func<T> factory)
	{
		if (factory is null) throw new ArgumentNullException(nameof(factory));

		lock (_sync)
		{
			var previous = Volatile.Read(ref _overrideInstance);
			var current  = CreateWithGuard(factory);
			Volatile.Write(ref _overrideInstance, current);
			return new OverrideScope(previous, current);
		}
	}

	/// <summary>
	///     Configures the creation factory. Optionally recreates the instance(s).
	/// </summary>
	/// <param name="factory">A non-null delegate that returns a non-null instance.</param>
	/// <param name="recreateIfInitialized">
	///     When true, disposes and recreates both override and default instances using the new factory.
	/// </param>
	/// <exception cref="ArgumentNullException">When factory is null.</exception>
	public static void ConfigureFactory(Func<T> factory, bool recreateIfInitialized = false)
	{
		if (factory is null) throw new ArgumentNullException(nameof(factory));

		lock (_sync)
		{
			_factory = factory;

			if (recreateIfInitialized)
			{
				DisposeIfNeeded(ref _overrideInstance);
				if (_lazy.IsValueCreated)
				{
					var created = _lazy.Value;
					DisposeIfNeeded(ref created);
				}

				_overrideInstance = null;
				_lazy             = NewLazy();
			}
		}
	}

	/// <summary>
	///     Resets the singleton to its initial state. Optionally disposes existing instances (override and default).
	/// </summary>
	/// <param name="disposeInstances">If true, disposes IDisposable instances before resetting.</param>
	public static void Reset(bool disposeInstances = false)
	{
		lock (_sync)
		{
			if (disposeInstances)
			{
				DisposeIfNeeded(ref _overrideInstance);
				if (_lazy.IsValueCreated)
				{
					var created = _lazy.Value;
					DisposeIfNeeded(ref created);
				}
			}

			_overrideInstance = null;
			_lazy             = NewLazy();
			_factory          = DefaultFactory;
		}
	}

	// -----------------------
	// Internals and helpers
	// -----------------------

	private static Lazy<T> NewLazy() =>
		new(() => CreateWithGuard(_factory), LazyThreadSafetyMode.ExecutionAndPublication);

	private static T CreateWithGuard(Func<T> creator)
	{
		Interlocked.Exchange(ref _initializing, 1);
		try
		{
			var instance = creator();
			if (instance is null)
				throw new TypeInitializationException(
					typeof(T).FullName ?? typeof(T).Name,
					new InvalidOperationException("Singleton factory returned null."));

			return instance;
		}
		catch (MissingMethodException ex)
		{
			throw new TypeInitializationException(
				typeof(T).FullName ?? typeof(T).Name,
				new InvalidOperationException(
					$"The type {typeof(T).FullName} must have a private or protected parameterless constructor " +
					$"or a configured factory to be used with {nameof(Singleton<T>)}.",
					ex));
		}
		finally
		{
			Interlocked.Exchange(ref _initializing, 0);
		}
	}

	private static T DefaultFactory()
	{
		var t = typeof(T);

		if (t.IsInterface)
			throw new TypeInitializationException(
				t.FullName ?? t.Name,
				new InvalidOperationException(
					$"Cannot create a singleton for interface type {t.FullName}. Configure a factory."));

		if (t.IsAbstract)
			throw new TypeInitializationException(
				t.FullName ?? t.Name,
				new InvalidOperationException(
					$"Cannot create a singleton for abstract type {t.FullName}. Configure a factory."));

		if (t.ContainsGenericParameters)
			throw new TypeInitializationException(
				t.FullName ?? t.Name,
				new InvalidOperationException($"Cannot create a singleton for open generic type {t.FullName}."));

		object? instance = Activator.CreateInstance(t, true);
		if (instance is null)
			throw new TypeInitializationException(
				t.FullName ?? t.Name,
				new InvalidOperationException($"Could not create an instance of type {t.FullName}."));

		return (T)instance;
	}

	private static void DisposeIfNeeded(ref T? instance)
	{
		if (instance is IDisposable d)
			try
			{
				d.Dispose();
			}
			catch
			{
				/* Swallow to keep Reset robust. */
			}

		instance = null;
	}

	private sealed class OverrideScope : IDisposable
	{
		private readonly T    _current;
		private readonly T?   _previous;
		private          bool _disposed;

		public OverrideScope(T? previous, T current)
		{
			_previous = previous;
			_current  = current;
		}

		public void Dispose()
		{
			if (_disposed) return;

			lock (_sync)
			{
				// Restore only if our override is still the active one
				if (ReferenceEquals(_overrideInstance, _current))
				{
					var toDispose = _current;
					DisposeIfNeeded(ref toDispose);
					Volatile.Write(ref _overrideInstance, _previous);
				}
			}

			_disposed = true;
		}
	}
}
