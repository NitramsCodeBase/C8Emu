namespace C8E.Core;

public interface IChip8
{
    ReadOnlyMemory<byte> Memory { get; }
    ReadOnlyMemory<byte> Registers { get; }
    ReadOnlyMemory<bool> Display { get; }
    ushort ProgramCounter { get; }
    ushort Index { get; }
    byte DelayTimer { get; }
    byte SoundTimer { get; }
    bool WaitingForKey { get; }
    void LoadRom(ReadOnlySpan<byte> rom);
    void Step();
    void TickTimers();
    void SetKey(int key, bool pressed);
}
