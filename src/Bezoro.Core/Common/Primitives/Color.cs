using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Bezoro.Core.Common.Primitives;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("Color(R={R}, G={G}, B={B}, A={A})")]
public readonly record struct Color(byte R, byte G, byte B, byte A)
	: IFormattable
{
	public Color(float R, float G, float B, float A) : this()
	{
		this.R = FloatToByte(R);
		this.G = FloatToByte(G);
		this.B = FloatToByte(B);
		this.A = FloatToByte(A);
	}

	public Color() : this(255, 255, 255, 255) { }

	public static Color FromRgb(byte   r, byte  g, byte  b)          => new(r, g, b, 255);
	public static Color FromRgb(float  r, float g, float b)          => new(r, g, b, 1f);
	public static Color FromArgb(byte  a, byte  r, byte  g, byte  b) => new(r, g, b, a);
	public static Color FromArgb(float a, float r, float g, float b) => new(r, g, b, a);

	public static Color Transparent => new(0, 0, 0, 0);
	public static Color White       => new(255, 255, 255, 255);
	public static Color Gray        => new(128, 128, 128, 255);
	public static Color Black       => new(0, 0, 0, 255);

	public float Af => A / 255f;
	public float Bf => B / 255f;
	public float Gf => G / 255f;
	public float Rf => R / 255f;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Color WithR(byte r) => new(r, G, B, A);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Color WithG(byte g) => new(R, g, B, A);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Color WithB(byte b) => new(R, G, b, A);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Color WithA(byte a) => new(R, G, B, a);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Color WithA(float a) => new(Rf, Gf, Bf, a);

	public static implicit operator System.Drawing.Color(Color color) =>
		System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

	public static implicit operator Vector4(Color c) => new(c.Rf, c.Gf, c.Bf, c.Af);
	public static explicit operator Color(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

	// Packed 32-bit helpers
	// 0xRRGGBBAA
	public uint ToRgba32() => (uint)R << 24 | (uint)G << 16 | (uint)B << 8 | A;

	public static Color FromRgba32(uint rgba) =>
		new((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

	// 0xAARRGGBB
	public uint ToArgb32() => (uint)A << 24 | (uint)R << 16 | (uint)G << 8 | B;

	public static Color FromArgb32(uint argb) =>
		new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

	// 0xAABBGGRR (little-endian BGRA in memory often)
	public uint ToAbgr32() => (uint)A << 24 | (uint)B << 16 | (uint)G << 8 | R;

	public static Color FromAbgr32(uint abgr) =>
		new((byte)abgr, (byte)(abgr >> 8), (byte)(abgr >> 16), (byte)(abgr >> 24));

	// Approximate relative luminance (uses sRGB coefficients; best with linearized channels)
	public float Luminance =>
		0.2126f * ToLinear(Rf) + 0.7152f * ToLinear(Gf) + 0.0722f * ToLinear(Bf);

	public static Color AlphaBlend(Color dst, Color src)
	{
		float sa   = src.Af, da = dst.Af;
		float outA = sa + da * (1f - sa);
		if (outA <= 0f) return Transparent;

		float r = (src.Rf * sa + dst.Rf * da * (1f - sa)) / outA;
		float g = (src.Gf * sa + dst.Gf * da * (1f - sa)) / outA;
		float b = (src.Bf * sa + dst.Bf * da * (1f - sa)) / outA;
		return new(r, g, b, outA);
	}

	public static Color AlphaBlendLinear(Color dst, Color src)
	{
		// Convert to linear
		float sR = ToLinear(src.Rf), sG = ToLinear(src.Gf), sB = ToLinear(src.Bf), sa = src.Af;
		float dR = ToLinear(dst.Rf), dG = ToLinear(dst.Gf), dB = ToLinear(dst.Bf), da = dst.Af;

		float outA = sa + da * (1f - sa);
		if (outA <= 0f) return Transparent;

		float r = (sR * sa + dR * da * (1f - sa)) / outA;
		float g = (sG * sa + dG * da * (1f - sa)) / outA;
		float b = (sB * sa + dB * da * (1f - sa)) / outA;

		// Back to sRGB
		return new(FromLinear(r), FromLinear(g), FromLinear(b), outA);
	}

	public override string ToString() => ToString("RGB", CultureInfo.InvariantCulture);

	// Formats:
	// "RGB"  -> "#RRGGBB"
	// "RGBA" -> "#RRGGBBAA"
	// "ARGB" -> "#AARRGGBB"
	// "CSS"  -> "rgba(r, g, b, a)"
	public string ToString(string? format, IFormatProvider? formatProvider)
	{
		Span<char> buffer = stackalloc char[ /*max*/ 11 + 1];
		if (TryFormat(buffer, out int written, format, formatProvider)) return new(buffer[..written]);

		// Fallback
		return $"#{R:X2}{G:X2}{B:X2}";
	}

	public bool TryFormat(
		Span<char>         destination,
		out int            charsWritten,
		ReadOnlySpan<char> format,
		IFormatProvider?   provider)
	{
		string fmt = format.Length == 0 ? "RGB" : new string(format).ToUpperInvariant();

		switch (fmt)
		{
			case "RGB":
			{
				if (destination.Length < 7)
				{
					charsWritten = 0;
					return false;
				}

				destination[0] = '#';
				WriteHex2(R, destination.Slice(1, 2));
				WriteHex2(G, destination.Slice(3, 2));
				WriteHex2(B, destination.Slice(5, 2));
				charsWritten = 7;
				return true;
			}
			case "RGBA":
			{
				if (destination.Length < 9)
				{
					charsWritten = 0;
					return false;
				}

				destination[0] = '#';
				WriteHex2(R, destination.Slice(1, 2));
				WriteHex2(G, destination.Slice(3, 2));
				WriteHex2(B, destination.Slice(5, 2));
				WriteHex2(A, destination.Slice(7, 2));
				charsWritten = 9;
				return true;
			}
			case "ARGB":
			{
				if (destination.Length < 9)
				{
					charsWritten = 0;
					return false;
				}

				destination[0] = '#';
				WriteHex2(A, destination.Slice(1, 2));
				WriteHex2(R, destination.Slice(3, 2));
				WriteHex2(G, destination.Slice(5, 2));
				WriteHex2(B, destination.Slice(7, 2));
				charsWritten = 9;
				return true;
			}
			case "CSS":
			{
				// rgba(r, g, b, a) with a in [0,1]
				var s = $"rgba({R}, {G}, {B}, {Af.ToString("0.###", CultureInfo.InvariantCulture)})";

				if (s.AsSpan().TryCopyTo(destination))
				{
					charsWritten = s.Length;
					return true;
				}

				charsWritten = 0;
				return false;
			}
			default:
				// Unknown -> default to RGB
				return TryFormat(destination, out charsWritten, "RGB".AsSpan(), provider);
		}
	}

	// Hex forms supported: #RGB, #RGBA, #RRGGBB, #RRGGBBAA (with or without leading '#')
	public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Color result)
	{
		s = s.Trim();

		if (s.Length == 0)
		{
			result = default;
			return false;
		}

		if (s[0] == '#') s = s[1..];

		// Short forms
		if (s.Length == 3 || s.Length == 4)
		{
			if (TryHexNibble(s[0], out byte r) &&
				TryHexNibble(s[1], out byte g) &&
				TryHexNibble(s[2], out byte b))
			{
				byte a = 0xF;

				if (s.Length == 4 && !TryHexNibble(s[3], out a))
				{
					result = default;
					return false;
				}

				// Expand 4-bit to 8-bit
				var R8 = (byte)(r << 4 | r);
				var G8 = (byte)(g << 4 | g);
				var B8 = (byte)(b << 4 | b);
				var A8 = (byte)(a << 4 | a);
				result = new(R8, G8, B8, s.Length == 4 ? A8 : (byte)255);
				return true;
			}

			result = default;
			return false;
		}

		// Long forms
		if (s.Length == 6 || s.Length == 8)
		{
			if (byte.TryParse(s.Slice(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
				byte.TryParse(s.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
				byte.TryParse(s.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
			{
				byte a = 255;

				if (s.Length == 8 &&
					!byte.TryParse(s.Slice(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a))
				{
					result = default;
					return false;
				}

				result = new(r, g, b, a);
				return true;
			}

			result = default;
			return false;
		}

		// CSS rgba(r, g, b, a)
		if (s.StartsWith("rgba", StringComparison.OrdinalIgnoreCase) ||
			s.StartsWith("rgb",  StringComparison.OrdinalIgnoreCase))
		{
			// Very small, permissive parser
			int open  = s.IndexOf('(');
			int close = s.LastIndexOf(')');

			if (open > 0 && close > open)
			{
				var         inner = s.Slice(open + 1, close - open - 1);
				Span<Range> parts = stackalloc Range[4];
				var         idx   = 0;
				var         start = 0;

				for (var i = 0; i <= inner.Length && idx < parts.Length; i++)
				{
					if (i == inner.Length || inner[i] == ',')
					{
						parts[idx++] = new(start, i);
						start        = i + 1;
					}
				}

				if (idx == 3 || idx == 4)
				{
					if (int.TryParse(
							inner[parts[0]].Trim(),
							NumberStyles.Integer,
							CultureInfo.InvariantCulture,
							out int rr) &&
						int.TryParse(
							inner[parts[1]].Trim(),
							NumberStyles.Integer,
							CultureInfo.InvariantCulture,
							out int gg) &&
						int.TryParse(
							inner[parts[2]].Trim(),
							NumberStyles.Integer,
							CultureInfo.InvariantCulture,
							out int bb))
					{
						var aa = 1f;

						if (idx == 4 &&
							!float.TryParse(
								inner[parts[3]].Trim(),
								NumberStyles.Float,
								CultureInfo.InvariantCulture,
								out aa))
						{
							result = default;
							return false;
						}

						result = new(
							(byte)Math.Clamp(rr, 0, 255),
							(byte)Math.Clamp(gg, 0, 255),
							(byte)Math.Clamp(bb, 0, 255),
							FloatToByte(aa));

						return true;
					}
				}
			}
		}

		result = default;
		return false;
	}

	public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
	{
		r = R;
		g = G;
		b = B;
		a = A;
	}

	public void Deconstruct(out float r, out float g, out float b, out float a)
	{
		r = Rf;
		g = Gf;
		b = Bf;
		a = Af;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte FloatToByte(float x)
	{
		// Clamp and round away from zero to reduce rounding bias
		float v = Math.Clamp(x, 0f, 1f) * 255f;
		return (byte)MathF.Round(v, MidpointRounding.AwayFromZero);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void WriteHex2(byte value, Span<char> dest)
	{
		const string hex = "0123456789ABCDEF";
		dest[0] = hex[value >> 4 & 0xF];
		dest[1] = hex[value      & 0xF];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryHexNibble(char c, out byte nibble)
	{
		if (c >= '0' && c <= '9')
		{
			nibble = (byte)(c - '0');
			return true;
		}

		if (c >= 'a' && c <= 'f')
		{
			nibble = (byte)(10 + (c - 'a'));
			return true;
		}

		if (c >= 'A' && c <= 'F')
		{
			nibble = (byte)(10 + (c - 'A'));
			return true;
		}

		nibble = 0;
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float ToLinear(float srgb)
		=> srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float FromLinear(float linear)
		=> linear <= 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
}
