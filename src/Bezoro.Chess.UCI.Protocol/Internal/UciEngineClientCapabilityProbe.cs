using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.Internal;

internal static class UciEngineClientCapabilityProbe
{
	public static async Task ProbeCoordinatorExtensionsAsync(UciEngineClient client, CancellationToken ct)
	{
		if (client is null)
			throw new ArgumentNullException(nameof(client));

		await client.SetPositionAsync(Fen.Default, null, ct).ConfigureAwait(false);

		var                  displayBoardFen  = UciCapabilityState.Unsupported;
		var                  perftMoveListing = UciCapabilityState.Unsupported;
		Fen?                 probedFen        = null;
		ImmutableArray<string> probedMoves      = default;

		try
		{
			probedFen = await client.TryGetFenViaDisplayBoardAsync(ct).ConfigureAwait(false);
			displayBoardFen = probedFen.HasValue
								  ? UciCapabilityState.Supported
								  : UciCapabilityState.Unsupported;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			displayBoardFen = UciCapabilityState.Unsupported;
		}
		catch (Exception) when (!ct.IsCancellationRequested)
		{
			displayBoardFen = UciCapabilityState.Unsupported;
		}

		try
		{
			probedMoves = await client.GetLegalMovesViaPerftAsync(ct).ConfigureAwait(false);
			perftMoveListing = !probedMoves.IsDefault && probedMoves.Length > 0
								   ? UciCapabilityState.Supported
								   : UciCapabilityState.Unsupported;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			perftMoveListing = UciCapabilityState.Unsupported;
		}
		catch (Exception) when (!ct.IsCancellationRequested)
		{
			perftMoveListing = UciCapabilityState.Unsupported;
		}

		client.SetExtensionCapabilities(displayBoardFen, perftMoveListing);
	}
}
