using System.Collections.Concurrent;

namespace DataMigration.Contracts;

public class DataRecord : Dictionary<string, object?>
{
    public DataRecord(int capacity = 16)
        : base(capacity)
    {
    }

    public T? GetValue<T>(string key) => this.TryGetValue(key, out var val) ? (T?)val : default;
    public void SetValue(string key, object? value) => this[key] = value;

    public void Reset()
    {
        this.Clear();
    }
}

public class DataRecordPool
{
    private readonly ConcurrentQueue<DataRecord> _pool = new();
    private readonly int _initialCapacity;

    public DataRecordPool(int initialCapacity = 16)
    {
        _initialCapacity = initialCapacity;
    }

    public DataRecord Rent()
    {
        if (_pool.TryDequeue(out var record))
        {
            record.Reset();
            return record;
        }
        return new DataRecord(_initialCapacity);
    }

    public void Return(DataRecord record)
    {
        if (record != null)
        {
            record.Reset();
            _pool.Enqueue(record);
        }
    }
}
