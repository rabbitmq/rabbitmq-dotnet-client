using System;
using System.Threading;
using RabbitMQ.Client.Framing.Impl;

namespace Test;

public sealed class TrackRentedByteResult : IDisposable
{
    private readonly Connection _connection;
    private readonly SemaphoreSlim _byteTrackingLock;

    internal TrackRentedByteResult(Connection connection, SemaphoreSlim byteTrackingLock)
    {
        _connection = connection;
        _byteTrackingLock = byteTrackingLock;
    }

    public uint RentedBytes => _connection.RentedBytes;

    public void Dispose()
    {
        _byteTrackingLock.Release();
    }
}
