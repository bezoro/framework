namespace Bezoro.Core.Health
{
	public interface IHealth
	{
		public float Percentage { get; }
		public int   Current    { get; }
		public int   Max        { get; }
		public void DecreaseCurrentHealthBy(int value);
		public void DecreaseMaxHealthBy(int value);
		public void DepleteCurrentHealth();
		public void FullyRestoreCurrentHealth();
		public void IncreaseCurrentHealthBy(int value);
		public void IncreaseMaxHealthBy(int value);
		public void RestoreCurrentHealthBy(int value);

		// public int GetHashCode();

		public void SetCurrentHealthTo(int value);

		public void SetMaxHealthTo(int value);
	}
}
