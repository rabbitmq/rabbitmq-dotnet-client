using System;
using System.Buffers;
using System.Runtime.InteropServices;
using RabbitMQ.Util;
using Xunit;

namespace Test.Unit;

public class TestSequenceBuilder
{
    [Fact]
    public void TestSingleMemory()
    {
        // Arrange
        byte[] array = new byte[] { 1, 2, 3, 4, 5, 6 };

        // Act
        SequenceBuilder<byte> builder = new SequenceBuilder<byte>();
        builder.Append(array);

        var sequence = builder.Build();

        // Assert
        Assert.Equal(6, sequence.Length);
        Assert.True(sequence.IsSingleSegment);
        Assert.True(MemoryMarshal.TryGetArray(sequence.First, out var segment));
        Assert.Equal(array, segment.Array);
        Assert.Equal(0, segment.Offset);
        Assert.Equal(6, segment.Count);
    }

    [Fact]
    public void TestMerge()
    {
        // Arrange
        byte[] array = new byte[] { 1, 2, 3, 4, 5, 6 };
        Memory<byte> first = array.AsMemory(0, 3);
        Memory<byte> second = array.AsMemory(3, 3);

        // Act
        SequenceBuilder<byte> builder = new SequenceBuilder<byte>();

        builder.Append(first);
        builder.Append(second);

        var sequence = builder.Build();

        // Assert
        Assert.Equal(6, sequence.Length);
        Assert.True(sequence.IsSingleSegment);
        Assert.True(MemoryMarshal.TryGetArray(sequence.First, out var segment));
        Assert.Equal(array, segment.Array);
        Assert.Equal(0, segment.Offset);
        Assert.Equal(6, segment.Count);
    }

    [Fact]
    public void TestMultipleMemory()
    {
        // Arrange
        byte[] first = new byte[] { 1, 2, 3 };
        byte[] second = new byte[] { 4, 5, 6 };

        // Act
        SequenceBuilder<byte> builder = new SequenceBuilder<byte>();

        builder.Append(first);
        builder.Append(second);

        var sequence = builder.Build();

        // Assert
        Assert.Equal(6, sequence.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, sequence.ToArray());

        var enumerator = sequence.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(MemoryMarshal.TryGetArray(enumerator.Current, out var segment));
        Assert.Equal(first, segment.Array);
        Assert.Equal(0, segment.Offset);
        Assert.Equal(3, segment.Count);

        Assert.True(enumerator.MoveNext());
        Assert.True(MemoryMarshal.TryGetArray(enumerator.Current, out segment));
        Assert.Equal(second, segment.Array);
        Assert.Equal(0, segment.Offset);
        Assert.Equal(3, segment.Count);
    }
}
