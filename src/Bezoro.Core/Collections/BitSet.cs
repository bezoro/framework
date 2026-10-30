namespace Bezoro.Core.Collections;

/// <summary>Stores a fixed number of flags using one bit per flag.</summary>
/// <remarks>
/// Construction allocates one array of 64-bit words. Operations never resize it.
/// Concurrent reads are supported while no thread mutates the set; mutations require external serialization.
/// </remarks>
public sealed class BitSet
{
	private readonly ulong[] _words;
	private readonly ulong _lastWordMask;
	private long _version;

	/// <summary>Creates a set of cleared flags. Zero length is permitted.</summary>
	/// <exception cref="ArgumentOutOfRangeException">Length is negative.</exception>
	public BitSet(int length)
	{
		if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
		Length = length;
		int wordCount = (length >> 6) + ((length & 63) == 0 ? 0 : 1);
		_words = wordCount == 0 ? Array.Empty<ulong>() : new ulong[wordCount];
		_lastWordMask = (length & 63) == 0 ? ulong.MaxValue : (1UL << (length & 63)) - 1;
	}

	/// <summary>Gets the fixed number of addressable flags.</summary>
	public int Length { get; }

	/// <summary>Counts set flags in O(ceil(Length / 64)) time without allocating.</summary>
	public int Count
	{
		get
		{
			int count = 0;
			foreach (ulong word in _words) count += PopCount(word);
			return count;
		}
	}

	/// <summary>Gets or sets a flag at a zero-based index.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the fixed length.</exception>
	public bool this[int index]
	{
		get => Contains(index);
		set => Set(index, value);
	}

	/// <summary>Reports whether the indexed flag is set.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the fixed length.</exception>
	public bool Contains(int index)
	{
		ValidateIndex(index);
		return (_words[index >> 6] & (1UL << (index & 63))) != 0;
	}

	/// <summary>Sets or clears a flag, returning true only when its value changes.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the fixed length.</exception>
	public bool Set(int index, bool value = true)
	{
		ValidateIndex(index);
		ref ulong word = ref _words[index >> 6];
		ulong mask = 1UL << (index & 63);
		ulong updated = value ? word | mask : word & ~mask;
		if (updated == word) return false;
		word = updated;
		Changed();
		return true;
	}

	/// <summary>Clears a flag, returning true only when its value changes.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the fixed length.</exception>
	public bool Reset(int index) => Set(index, false);

	/// <summary>Inverts a flag and returns its new value.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the fixed length.</exception>
	public bool Toggle(int index)
	{
		ValidateIndex(index);
		ulong mask = 1UL << (index & 63);
		ref ulong word = ref _words[index >> 6];
		word ^= mask;
		Changed();
		return (word & mask) != 0;
	}

	/// <summary>Sets every addressable flag, leaving unused tail bits clear.</summary>
	public void SetAll()
	{
		bool changed = false;
		for (int index = 0; index < _words.Length; index++)
		{
			ulong value = index == _words.Length - 1 ? _lastWordMask : ulong.MaxValue;
			changed |= _words[index] != value;
			_words[index] = value;
		}
		if (changed) Changed();
	}

	/// <summary>Clears every flag while retaining the backing storage.</summary>
	public void Clear()
	{
		foreach (ulong word in _words)
		{
			if (word == 0) continue;
			Array.Clear(_words, 0, _words.Length);
			Changed();
			return;
		}
	}

	/// <summary>Sets flags present in either set. The other set must have the same length.</summary>
	/// <exception cref="ArgumentNullException">The other set is null.</exception>
	/// <exception cref="ArgumentException">The lengths differ. Neither set is modified.</exception>
	public void UnionWith(BitSet other)
	{
		ValidateOther(other);
		bool changed = false;
		for (int index = 0; index < _words.Length; index++)
		{
			ulong value = _words[index] | other._words[index];
			changed |= _words[index] != value;
			_words[index] = value;
		}
		if (changed) Changed();
	}

	/// <summary>Keeps only flags present in both sets. The other set must have the same length.</summary>
	/// <exception cref="ArgumentNullException">The other set is null.</exception>
	/// <exception cref="ArgumentException">The lengths differ. Neither set is modified.</exception>
	public void IntersectWith(BitSet other)
	{
		ValidateOther(other);
		bool changed = false;
		for (int index = 0; index < _words.Length; index++)
		{
			ulong value = _words[index] & other._words[index];
			changed |= _words[index] != value;
			_words[index] = value;
		}
		if (changed) Changed();
	}

	/// <summary>Clears flags present in the other set. The other set must have the same length.</summary>
	/// <exception cref="ArgumentNullException">The other set is null.</exception>
	/// <exception cref="ArgumentException">The lengths differ. Neither set is modified.</exception>
	public void ExceptWith(BitSet other)
	{
		ValidateOther(other);
		bool changed = false;
		for (int index = 0; index < _words.Length; index++)
		{
			ulong value = _words[index] & ~other._words[index];
			changed |= _words[index] != value;
			_words[index] = value;
		}
		if (changed) Changed();
	}

	/// <summary>Keeps flags present in exactly one set. The other set must have the same length.</summary>
	/// <exception cref="ArgumentNullException">The other set is null.</exception>
	/// <exception cref="ArgumentException">The lengths differ. Neither set is modified.</exception>
	public void XorWith(BitSet other)
	{
		ValidateOther(other);
		bool changed = false;
		for (int index = 0; index < _words.Length; index++)
		{
			ulong value = _words[index] ^ other._words[index];
			changed |= _words[index] != value;
			_words[index] = value;
		}
		if (changed) Changed();
	}

	/// <summary>Finds the first set flag at or after the supplied index, or returns -1 when none remains.</summary>
	/// <param name="startIndex">An inclusive index between zero and Length. Length always returns -1.</param>
	/// <exception cref="ArgumentOutOfRangeException">The index is negative or greater than Length.</exception>
	public int FindNextSetBit(int startIndex = 0)
	{
		if (startIndex < 0 || startIndex > Length) throw new ArgumentOutOfRangeException(nameof(startIndex));
		if (startIndex == Length) return -1;
		int wordIndex = startIndex >> 6;
		ulong word = _words[wordIndex] & (ulong.MaxValue << (startIndex & 63));
		while (word == 0)
		{
			if (++wordIndex == _words.Length) return -1;
			word = _words[wordIndex];
		}
		return (wordIndex << 6) + TrailingZeroCount(word);
	}

	/// <summary>Enumerates set indices in ascending order without boxing or allocation.</summary>
	/// <remarks>Changing flag contents invalidates existing enumerators. No-op writes do not.</remarks>
	public Enumerator GetEnumerator() => new(this);

	private void ValidateIndex(int index)
	{
		if (index < 0 || index >= Length) throw new ArgumentOutOfRangeException(nameof(index));
	}

	private void ValidateOther(BitSet other)
	{
		if (other is null) throw new ArgumentNullException(nameof(other));
		if (other.Length != Length) throw new ArgumentException("Bit sets must have the same length.", nameof(other));
	}

	private void Changed() => _version = unchecked(_version + 1);

	private static int PopCount(ulong value)
	{
#if NET5_0_OR_GREATER
		return System.Numerics.BitOperations.PopCount(value);
#else
		unchecked
		{
			value -= (value >> 1) & 0x5555555555555555UL;
			value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
			value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
			return (int)((value * 0x0101010101010101UL) >> 56);
		}
#endif
	}

	private static int TrailingZeroCount(ulong value)
	{
#if NET5_0_OR_GREATER
		return System.Numerics.BitOperations.TrailingZeroCount(value);
#else
		int count = 0;
		if ((value & uint.MaxValue) == 0) { count += 32; value >>= 32; }
		if ((value & ushort.MaxValue) == 0) { count += 16; value >>= 16; }
		if ((value & byte.MaxValue) == 0) { count += 8; value >>= 8; }
		if ((value & 15) == 0) { count += 4; value >>= 4; }
		if ((value & 3) == 0) { count += 2; value >>= 2; }
		return count + ((value & 1) == 0 ? 1 : 0);
#endif
	}

	/// <summary>A value-type cursor over set indices. Its lifetime requires an unchanged set.</summary>
	public struct Enumerator
	{
		private readonly BitSet? _owner;
		private readonly long _version;
		private int _wordIndex;
		private ulong _pending;
		private int _current;

		internal Enumerator(BitSet owner)
		{
			_owner = owner;
			_version = owner._version;
			_wordIndex = -1;
			_pending = 0;
			_current = -1;
		}

		/// <summary>Gets the current set index.</summary>
		/// <exception cref="InvalidOperationException">The cursor is not positioned or its set changed.</exception>
		public readonly int Current
		{
			get
			{
				ValidateVersion();
				if (_owner is null || _current < 0 || _current >= _owner.Length)
					throw new InvalidOperationException("The enumerator is not positioned on a flag.");
				return _current;
			}
		}

		/// <summary>Advances to the next set index, returning false at the end.</summary>
		/// <exception cref="InvalidOperationException">The set changed after enumeration began.</exception>
		public bool MoveNext()
		{
			ValidateVersion();
			if (_owner is null) return false;
			while (_pending == 0)
			{
				if (_wordIndex >= _owner._words.Length - 1)
				{
					_current = _owner.Length;
					return false;
				}
				_pending = _owner._words[++_wordIndex];
			}
			_current = (_wordIndex << 6) + TrailingZeroCount(_pending);
			_pending &= _pending - 1;
			return true;
		}

		private readonly void ValidateVersion()
		{
			if (_owner is not null && _owner._version != _version)
				throw new InvalidOperationException("The bit set changed during enumeration.");
		}
	}
}
