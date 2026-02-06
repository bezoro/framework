using Bezoro.Events.Abstractions;

namespace Bezoro.Events.Services;

/// <summary>
///     Default implementation of <see cref="IUnityEventBuses" /> backed by <see cref="EventBus" /> instances.
/// </summary>
public sealed class UnityEventBuses : IUnityEventBuses
{
	private readonly bool _ownsBuses;

	/// <summary>
	///     Creates Unity event buses backed by new <see cref="EventBus" /> instances.
	/// </summary>
	public UnityEventBuses()
		: this(new EventBus(), new EventBus(), new EventBus()) { }

	/// <summary>
	///     Creates Unity event buses backed by the provided <see cref="IEventBus" /> instances.
	/// </summary>
	/// <param name="update">The Update bus.</param>
	/// <param name="fixedUpdate">The FixedUpdate bus.</param>
	/// <param name="lateUpdate">The LateUpdate bus.</param>
	/// <param name="ownsBuses">Whether this instance should dispose the provided buses.</param>
	public UnityEventBuses(IEventBus update, IEventBus fixedUpdate, IEventBus lateUpdate, bool ownsBuses = true)
	{
		Update      = update ?? throw new ArgumentNullException(nameof(update));
		FixedUpdate = fixedUpdate ?? throw new ArgumentNullException(nameof(fixedUpdate));
		LateUpdate  = lateUpdate ?? throw new ArgumentNullException(nameof(lateUpdate));
		_ownsBuses  = ownsBuses;
	}

	/// <inheritdoc />
	public IEventBus FixedUpdate { get; }

	/// <inheritdoc />
	public IEventBus LateUpdate { get; }

	/// <inheritdoc />
	public IEventBus Update { get; }

	/// <inheritdoc />
	public int FlushFixedUpdate() => FixedUpdate.FlushQueued();

	/// <inheritdoc />
	public int FlushLateUpdate() => LateUpdate.FlushQueued();

	/// <inheritdoc />
	public int FlushUpdate() => Update.FlushQueued();

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_ownsBuses)
			return;

		Update.Dispose();
		FixedUpdate.Dispose();
		LateUpdate.Dispose();
	}
}
