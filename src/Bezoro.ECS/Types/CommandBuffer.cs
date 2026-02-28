namespace Bezoro.ECS.Types;

/// <summary>
///     Ergonomic wrapper over the low-level command stream used during system execution.
/// </summary>
public readonly struct CommandBuffer(CommandStream stream)
{
	private readonly CommandStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

	public static implicit operator CommandStream(CommandBuffer buffer) =>
		buffer._stream;

	/// <summary>
	///     Gets diagnostics for the underlying command storage.
	/// </summary>
	public CommandStreamDiagnostics GetDiagnostics() =>
		_stream.GetDiagnostics();

	/// <summary>
	///     Indicates whether commands are currently recorded.
	/// </summary>
	public bool HasCommands => _stream.HasCommands;

	/// <summary>
	///     Records a deferred entity spawn.
	/// </summary>
	public Entity Spawn() =>
		_stream.CreateEntity();

	/// <summary>
	///     Records a deferred entity spawn.
	/// </summary>
	public Entity CreateEntity() =>
		Spawn();

	/// <summary>
	///     Records a deferred entity spawn with one initial component.
	/// </summary>
	public Entity Spawn<T>(in T component) where T : struct =>
		_stream.CreateEntity(in component);

	/// <summary>
	///     Records a deferred entity spawn with one initial component.
	/// </summary>
	public Entity CreateEntity<T>(in T component) where T : struct =>
		Spawn(in component);

	/// <summary>
	///     Records a deferred entity despawn.
	/// </summary>
	public void Despawn(Entity entity) =>
		_stream.Destroy(entity);

	/// <summary>
	///     Records a deferred entity despawn.
	/// </summary>
	public void Destroy(Entity entity) =>
		Despawn(entity);

	/// <summary>
	///     Records a deferred component add.
	/// </summary>
	public void Add<T>(Entity entity, in T component) where T : struct =>
		Replace(entity, in component);

	/// <summary>
	///     Records a deferred component replacement.
	/// </summary>
	public void Replace<T>(Entity entity, in T component) where T : struct
		=> _stream.SetManaged(entity, in component);

	/// <summary>
	///     Records a deferred component replacement.
	/// </summary>
	public void Set<T>(Entity entity, in T component) where T : struct =>
		Replace(entity, in component);

	/// <summary>
	///     Records a deferred component removal.
	/// </summary>
	public void Remove<T>(Entity entity) where T : struct =>
		_stream.Remove<T>(entity);
}
