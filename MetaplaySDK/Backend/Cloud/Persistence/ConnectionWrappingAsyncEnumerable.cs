// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Persistence;

/// <summary>
/// A wrapper for an <see cref="IAsyncEnumerable{T}"/> that ensures that a <see cref="DbConnection"/> is disposed when the enumeration is complete.
/// Requires that this class is disposed when the enumeration is complete.
/// </summary>
public class ConnectionWrappingAsyncEnumerable<T> : IAsyncEnumerable<T>, IAsyncDisposable
{
    class Enumerator : IAsyncEnumerator<T> {
        readonly ConnectionWrappingAsyncEnumerable<T> _parent;
        readonly IAsyncEnumerator<T>                  _enumerator;

        public Enumerator(ConnectionWrappingAsyncEnumerable<T> parent, CancellationToken token)
        {
            _parent = parent;
            _enumerator = parent._asyncEnumerable.GetAsyncEnumerator(token);
        }

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _enumerator.MoveNextAsync();
        }

        public T Current => _enumerator.Current;
    }

    readonly IAsyncEnumerable<T>  _asyncEnumerable;
    DbConnection                  _connection;

    public ConnectionWrappingAsyncEnumerable(IAsyncEnumerable<T> asyncEnumerable, DbConnection connection)
    {
        _asyncEnumerable = asyncEnumerable;
        _connection      = connection;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
        => new Enumerator(this, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        GC.SuppressFinalize(this);
    }

    ~ConnectionWrappingAsyncEnumerable()
    {
        if (_connection != null)
        {
            Serilog.Log.Error($"{nameof(ConnectionWrappingAsyncEnumerable<T>)} disposed by finalizer. Did you forget to call DisposeAsync?");
            _connection.Dispose();
            _connection = null;
        }
    }
}
