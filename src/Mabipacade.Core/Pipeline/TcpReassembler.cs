using Mabipacade.Core.Model;

namespace Mabipacade.Core.Pipeline;

internal sealed class TcpReassembler
{
    private readonly List<byte> _buffer = new();
    private uint? _nextSeq;
    private uint? _bufferStartSeq;
    private readonly List<TcpFrame> _pending = new();

    public ReadOnlySpan<byte> GetBuffer() => _buffer.ToArray();

    public void Feed(TcpFrame frame)
    {
        if (frame.Payload.Length == 0) return;

        var frameEnd = frame.SequenceNumber + (uint)frame.Payload.Length;

        if (_nextSeq is null)
        {
            // No data committed yet — commit this frame as anchor
            AppendInOrder(frame);
            DrainPending();
            return;
        }

        // Frame entirely before buffer start: check if it fills before the start
        if (_bufferStartSeq is not null && frame.SequenceNumber < _bufferStartSeq)
        {
            if (frameEnd == _bufferStartSeq)
            {
                // Prepend fills the gap exactly before the current buffer
                _buffer.InsertRange(0, frame.Payload);
                _bufferStartSeq = frame.SequenceNumber;
                return;
            }
            // Partial overlap or entirely before — drop
            return;
        }

        // Entirely within committed range (duplicate / stale)
        if (frameEnd <= _nextSeq) return;

        // Exact in-order continuation
        if (frame.SequenceNumber == _nextSeq)
        {
            AppendInOrder(frame);
            DrainPending();
            return;
        }

        // Drop duplicate pending entries (same seq already queued)
        if (_pending.Any(p => p.SequenceNumber == frame.SequenceNumber)) return;

        // Future/gap segment: queue it
        _pending.Add(frame);
        _pending.Sort((a, b) => a.SequenceNumber.CompareTo(b.SequenceNumber));
        DrainPending();
    }

    public void Consume(int count)
    {
        if (count <= 0) return;
        if (count > _buffer.Count) count = _buffer.Count;
        _buffer.RemoveRange(0, count);
        if (_bufferStartSeq is not null)
            _bufferStartSeq = _bufferStartSeq.Value + (uint)count;
    }

    public void Reset()
    {
        _buffer.Clear();
        _pending.Clear();
        _nextSeq = null;
        _bufferStartSeq = null;
    }

    private void AppendInOrder(TcpFrame frame)
    {
        if (_bufferStartSeq is null)
            _bufferStartSeq = frame.SequenceNumber;
        _buffer.AddRange(frame.Payload);
        _nextSeq = frame.SequenceNumber + (uint)frame.Payload.Length;
    }

    private void DrainPending()
    {
        bool progress;
        do
        {
            progress = false;
            for (int i = 0; i < _pending.Count; i++)
            {
                var f = _pending[i];
                if (f.SequenceNumber == _nextSeq)
                {
                    AppendInOrder(f);
                    _pending.RemoveAt(i);
                    progress = true;
                    break;
                }
            }
        } while (progress);
    }
}
