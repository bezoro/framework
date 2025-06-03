using Bezoro.Core.Chess;

public interface IGameRules
{
	bool IsCastleLegal(KingModel king, CastleSide side);
}
