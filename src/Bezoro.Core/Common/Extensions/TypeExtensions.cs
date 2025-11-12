using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Type"/> to determine characteristics such as
/// anonymous type, static type, or nullness.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines whether the specified <see cref="Type"/> is an anonymous type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>
    /// <c>true</c> if the type is compiler-generated as an anonymous type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Uses a set of heuristics to identify compiler-generated anonymous types used in C# and VB.NET.
    /// </remarks>
    public static bool IsAnonymous(this Type type) =>
        Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) &&
        type.IsGenericType                                                   &&
        type.Name.Contains("AnonymousType")                                  &&
        (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))          &&
        type.Attributes.HasFlag(TypeAttributes.NotPublic);

    /// <summary>
    /// Determines whether the specified <see cref="Type"/> instance is <c>null</c>.
    /// </summary>
    /// <param name="componentType">The type to check for <c>null</c> reference.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="componentType"/> is <c>null</c>; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNull(this Type componentType) =>
        componentType == null;

    /// <summary>
    /// Determines whether the specified <see cref="Type"/> represents a static class.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="type"/> is both abstract and sealed (static class); otherwise, <c>false</c>.
    /// </returns>
    public static bool IsStatic(this Type type) =>
        type.IsAbstract && type.IsSealed;
}
