using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Extension methods for bitmap operations on ulong arrays.
/// </summary>
public static class BitmapExtensions
{
    private const int BitsPerWord = 64;
    private const int BitsPerWordShift = 6;  // log2(64)
    private const int BitsPerWordMask = BitsPerWord - 1;

    /// <summary>
    /// Counts consecutive set bits (1s) starting from <paramref name="startIndex"/>.
    /// Uses <see cref="BitOperations.TrailingZeroCount(ulong)"/> to process 64 bits at a time.
    /// </summary>
    /// <param name="bitmap">The bitmap array to scan.</param>
    /// <param name="startIndex">The bit index to start counting from.</param>
    /// <param name="maxIndex">The exclusive upper bound for counting.</param>
    /// <returns>The number of consecutive set bits starting from <paramref name="startIndex"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountConsecutiveSetBits(this ulong[] bitmap, int startIndex, int maxIndex)
    {
        int count = 0;
        int index = startIndex;

        while (index < maxIndex)
        {
            int wordIndex = index >> BitsPerWordShift;
            int bitIndex = index & BitsPerWordMask;

            if (wordIndex >= bitmap.Length)
                break;

            ulong word = Volatile.Read(ref bitmap[wordIndex]);

            // Shift right to align our starting bit to position 0
            ulong shifted = word >> bitIndex;

            // Find first 0 bit (first unset bit)
            // ~shifted inverts: set bits (1) become 0, unset (0) become 1
            // TrailingZeroCount finds position of first 1 in inverted = first 0 in original
            int consecutiveInWord = BitOperations.TrailingZeroCount(~shifted);

            // Clamp to remaining bits in this word
            int remainingBitsInWord = BitsPerWord - bitIndex;
            if (consecutiveInWord > remainingBitsInWord)
                consecutiveInWord = remainingBitsInWord;

            // Clamp to maxIndex
            int remainingToMax = maxIndex - index;
            if (consecutiveInWord > remainingToMax)
                consecutiveInWord = remainingToMax;

            count += consecutiveInWord;
            index += consecutiveInWord;

            // If we didn't consume all remaining bits in the word, we hit a 0 (unset bit)
            if (consecutiveInWord < remainingBitsInWord)
                break;
        }

        return count;
    }
}
