using System;

namespace Bezoro.Core.Common.Extensions;

public readonly record struct Color
{
	public Color(float R, float G, float B) : this()
	{
		this.R = (byte)Math.Round(Math.Clamp(R, 0f, 1f) * 255);
		this.G = (byte)Math.Round(Math.Clamp(G, 0f, 1f) * 255);
		this.B = (byte)Math.Round(Math.Clamp(B, 0f, 1f) * 255);
	}

	public Color() : this(255, 255, 255) { }

	public Color(byte R, byte G, byte B)
	{
		this.R = R;
		this.G = G;
		this.B = B;
	}

	public static Color Black   { get; } = new(0, 0, 0);
	public static Color Blue    { get; } = new(0, 0, 255);
	public static Color Cyan    { get; } = new(0, 255, 255);
	public static Color Gray    { get; } = new(128, 128, 128);
	public static Color Green   { get; } = new(0, 255, 0);
	public static Color Magenta { get; } = new(255, 0, 255);
	public static Color Red     { get; } = new(255, 0, 0);
	public static Color White   { get; } = new(255, 255, 255);
	public static Color Yellow  { get; } = new(255, 255, 0);

	public float Bf => B / 255f;
	public float Gf => G / 255f;
	public float Rf => R / 255f;

	public byte B { get; init; }
	public byte G { get; init; }
	public byte R { get; init; }

	public override string ToString() =>
		$"#{R:X2}{G:X2}{B:X2}";

	public void Deconstruct(out byte R, out byte G, out byte B)
	{
		R = this.R;
		G = this.G;
		B = this.B;
	}

	public void Deconstruct(out float R, out float G, out float B)
	{
		R = Rf;
		G = Gf;
		B = Bf;
	}
}
