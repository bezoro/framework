#if !NET6_0_OR_GREATER
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	[AttributeUsage(AttributeTargets.Parameter)]
	internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
	{
		public string ParameterName { get; } = parameterName;
	}
}
#endif
