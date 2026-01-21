using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.API.Abstractions;

/// <summary>
///     Repository interface for player profile persistence.
///     Unity (or other consumers) implements this to store profiles.
/// </summary>
public interface IProfileRepository
{
	/// <summary>
	///     Saves a player profile.
	/// </summary>
	/// <param name="profile">The profile to save.</param>
	/// <param name="ct">Cancellation token.</param>
	Task SaveAsync(PlayerProfile profile, CancellationToken ct = default);

	/// <summary>
	///     Updates a player's stats after a game.
	/// </summary>
	/// <param name="id">The profile ID.</param>
	/// <param name="result">The game result.</param>
	/// <param name="eloChange">The change in ELO rating.</param>
	/// <param name="ct">Cancellation token.</param>
	Task UpdateStatsAsync(string id, GameResult result, int eloChange, CancellationToken ct = default);

	/// <summary>
	///     Deletes a profile by ID.
	/// </summary>
	/// <param name="id">The profile ID to delete.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if the profile was deleted, false if not found.</returns>
	Task<bool> DeleteAsync(string id, CancellationToken ct = default);

	/// <summary>
	///     Checks if a profile exists.
	/// </summary>
	/// <param name="id">The profile ID.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if the profile exists.</returns>
	Task<bool> ExistsAsync(string id, CancellationToken ct = default);

	/// <summary>
	///     Gets all stored profiles.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>All profiles in the repository.</returns>
	Task<IReadOnlyList<PlayerProfile>> GetAllAsync(CancellationToken ct = default);

	/// <summary>
	///     Gets a player profile by ID.
	/// </summary>
	/// <param name="id">The profile ID.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The profile, or null if not found.</returns>
	Task<PlayerProfile?> GetAsync(string id, CancellationToken ct = default);

	/// <summary>
	///     Gets or creates the local player profile.
	///     If no local profile exists, creates one with default values.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The local player profile.</returns>
	Task<PlayerProfile> GetOrCreateLocalAsync(CancellationToken ct = default);
}
