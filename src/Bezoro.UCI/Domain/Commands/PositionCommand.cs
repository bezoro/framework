using System.Collections.Generic;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain.Commands;

internal record struct PositionCommand
{
	public PositionCommand(string fen, IEnumerable<string>? moves = null)
	{
		var parsedFen = Fen.Parse(fen);
		parsedFen.ThrowIfNull();

		Fen   = parsedFen.Value;
		Moves = moves;
	}

	public Fen                  Fen   { get; set; }
	public IEnumerable<string>? Moves { get; set; }

	public static implicit operator string(PositionCommand positionCommand)
	{
		var command = $"{UciConstants.POSITION_COMMAND} fen {positionCommand.Fen}";
		if (positionCommand.Moves != null)
		{
			var movesPart = string.Join(' ', positionCommand.Moves);
			if (!string.IsNullOrWhiteSpace(movesPart))
				command += $" moves {movesPart}";
		}

		return command;
	}

	public override string ToString() => this;

	public readonly void Deconstruct(out Fen fen, out IEnumerable<string>? moves)
	{
		fen   = Fen;
		moves = Moves;
	}
}
