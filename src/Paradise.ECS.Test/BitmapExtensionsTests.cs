namespace Paradise.ECS.Test;

public sealed class BitmapExtensionsTests
{
    [Test]
    public async Task CountConsecutiveSetBits_EmptyBitmap_ReturnsZero()
    {
        var bitmap = new ulong[] { 0 };
        int result = bitmap.CountConsecutiveSetBits(0, 64);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CountConsecutiveSetBits_AllBitsSet_ReturnsMaxIndex()
    {
        var bitmap = new ulong[] { ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(0, 64);
        await Assert.That(result).IsEqualTo(64);
    }

    [Test]
    public async Task CountConsecutiveSetBits_AllBitsSet_ClampsToMaxIndex()
    {
        var bitmap = new ulong[] { ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(0, 32);
        await Assert.That(result).IsEqualTo(32);
    }

    [Test]
    public async Task CountConsecutiveSetBits_FirstBitUnset_ReturnsZero()
    {
        var bitmap = new ulong[] { 0b1111_1110 }; // Bit 0 is unset
        int result = bitmap.CountConsecutiveSetBits(0, 64);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CountConsecutiveSetBits_FirstFourBitsSet_ReturnsFour()
    {
        var bitmap = new ulong[] { 0b0000_1111 }; // Bits 0-3 set
        int result = bitmap.CountConsecutiveSetBits(0, 64);
        await Assert.That(result).IsEqualTo(4);
    }

    [Test]
    public async Task CountConsecutiveSetBits_StartFromMiddle_CountsFromThere()
    {
        var bitmap = new ulong[] { 0b1111_0000 }; // Bits 4-7 set
        int result = bitmap.CountConsecutiveSetBits(4, 64);
        await Assert.That(result).IsEqualTo(4);
    }

    [Test]
    public async Task CountConsecutiveSetBits_StartFromUnsetBit_ReturnsZero()
    {
        var bitmap = new ulong[] { 0b0000_1111 }; // Bits 0-3 set
        int result = bitmap.CountConsecutiveSetBits(4, 64);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CountConsecutiveSetBits_SpansMultipleWords_CountsAcrossWords()
    {
        // First word: all bits set, Second word: first 10 bits set
        var bitmap = new ulong[] { ulong.MaxValue, 0b11_1111_1111 };
        int result = bitmap.CountConsecutiveSetBits(0, 128);
        await Assert.That(result).IsEqualTo(74); // 64 + 10
    }

    [Test]
    public async Task CountConsecutiveSetBits_StartsInSecondWord_CountsCorrectly()
    {
        var bitmap = new ulong[] { 0, ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(64, 128);
        await Assert.That(result).IsEqualTo(64);
    }

    [Test]
    public async Task CountConsecutiveSetBits_StartIndexBeyondBitmap_ReturnsZero()
    {
        var bitmap = new ulong[] { ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(64, 128);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CountConsecutiveSetBits_MaxIndexEqualsStartIndex_ReturnsZero()
    {
        var bitmap = new ulong[] { ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(10, 10);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CountConsecutiveSetBits_SingleBitSet_ReturnsOne()
    {
        var bitmap = new ulong[] { 1UL }; // Only bit 0 set
        int result = bitmap.CountConsecutiveSetBits(0, 64);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task CountConsecutiveSetBits_AlternatingBits_ReturnsOne()
    {
        var bitmap = new ulong[] { 0b0101_0101 }; // Alternating bits
        int result = bitmap.CountConsecutiveSetBits(0, 64);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task CountConsecutiveSetBits_HighBitsSet_CountsCorrectly()
    {
        // Bits 60-63 set (high nibble)
        var bitmap = new ulong[] { 0xF000_0000_0000_0000 };
        int result = bitmap.CountConsecutiveSetBits(60, 64);
        await Assert.That(result).IsEqualTo(4);
    }

    [Test]
    public async Task CountConsecutiveSetBits_CrossWordBoundary_CountsCorrectly()
    {
        // Last 4 bits of word 0, first 4 bits of word 1
        var bitmap = new ulong[] { 0xF000_0000_0000_0000, 0x0000_0000_0000_000F };
        int result = bitmap.CountConsecutiveSetBits(60, 128);
        await Assert.That(result).IsEqualTo(8); // 4 + 4
    }

    [Test]
    public async Task CountConsecutiveSetBits_LargeBitmap_CountsCorrectly()
    {
        // 4 words, all set
        var bitmap = new ulong[] { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(0, 256);
        await Assert.That(result).IsEqualTo(256);
    }

    [Test]
    public async Task CountConsecutiveSetBits_LargeBitmap_StopsAtFirstZero()
    {
        // Word 0: all set, Word 1: bit 0 unset, rest set
        var bitmap = new ulong[] { ulong.MaxValue, ulong.MaxValue - 1 };
        int result = bitmap.CountConsecutiveSetBits(0, 128);
        await Assert.That(result).IsEqualTo(64); // Stops at bit 64 (first bit of word 1)
    }

    [Test]
    public async Task CountConsecutiveSetBits_PartialLastWord_ClampsCorrectly()
    {
        var bitmap = new ulong[] { ulong.MaxValue, ulong.MaxValue };
        int result = bitmap.CountConsecutiveSetBits(0, 100);
        await Assert.That(result).IsEqualTo(100);
    }

    [Test]
    public async Task CountConsecutiveSetBits_StartMidWord_WithGap_StopsAtGap()
    {
        // Bits 10-19 set, bit 20 unset
        var bitmap = new ulong[] { 0b0000_1111_1111_1100_0000_0000 };
        int result = bitmap.CountConsecutiveSetBits(10, 64);
        await Assert.That(result).IsEqualTo(10);
    }
}
