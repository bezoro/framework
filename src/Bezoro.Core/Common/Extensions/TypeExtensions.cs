using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

public static class TypeExtensions
{
	public static bool IsAnonymous(this Type type) =>
		Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) &&
		type.IsGenericType                                                   &&
		type.Name.Contains("AnonymousType")                                  &&
		(type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))          &&
		type.Attributes.HasFlag(TypeAttributes.NotPublic);

	public static bool IsNull(this Type componentType) =>
		componentType == null;

	public static bool IsStatic(this Type type) =>
		type.IsAbstract && type.IsSealed;
}
