namespace NovaControlCenter.Infrastructure.Hid;

public sealed record ByteOffsetSummary(int Offset, byte Min, byte Max, int DistinctValues, byte First, byte Last);

/// <summary>
/// Acumula estatísticas por posição de byte dos relatórios recebidos.
/// Usado para achar bytes "lentos" (variam pouco ao longo de minutos), candidatos a nível de bateria.
/// </summary>
public sealed class ReportByteStatistics
{
    private readonly Dictionary<int, Accumulator> _offsets = [];

    public int ReportCount { get; private set; }

    public void Add(ReadOnlySpan<byte> report)
    {
        ReportCount++;
        for (var i = 0; i < report.Length; i++)
        {
            if (!_offsets.TryGetValue(i, out var acc))
            {
                acc = new Accumulator(report[i]);
                _offsets[i] = acc;
            }
            acc.Add(report[i]);
        }
    }

    public IReadOnlyList<ByteOffsetSummary> Summarize() =>
        _offsets.OrderBy(kv => kv.Key)
            .Select(kv => kv.Value.ToSummary(kv.Key))
            .ToList();

    private sealed class Accumulator(byte firstValue)
    {
        private readonly bool[] _seen = new bool[256];
        private readonly byte _first = firstValue;
        private byte _min = firstValue;
        private byte _max = firstValue;
        private byte _last = firstValue;
        private int _distinct;

        public void Add(byte value)
        {
            if (!_seen[value]) { _seen[value] = true; _distinct++; }
            if (value < _min) _min = value;
            if (value > _max) _max = value;
            _last = value;
        }

        public ByteOffsetSummary ToSummary(int offset) => new(offset, _min, _max, _distinct, _first, _last);
    }
}
