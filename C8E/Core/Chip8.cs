namespace C8E.Core;

public sealed class Chip8 : IChip8
{
    public const int Width = 64, Height = 32, MemorySize = 4096, RomStart = 0x200;
    private const int FontStart = 0x50;
    private static readonly byte[] Font =
    [
        0xF0,0x90,0x90,0x90,0xF0, 0x20,0x60,0x20,0x20,0x70,
        0xF0,0x10,0xF0,0x80,0xF0, 0xF0,0x10,0xF0,0x10,0xF0,
        0x90,0x90,0xF0,0x10,0x10, 0xF0,0x80,0xF0,0x10,0xF0,
        0xF0,0x80,0xF0,0x90,0xF0, 0xF0,0x10,0x20,0x40,0x40,
        0xF0,0x90,0xF0,0x90,0xF0, 0xF0,0x90,0xF0,0x10,0xF0,
        0xF0,0x90,0xF0,0x90,0x90, 0xE0,0x90,0xE0,0x90,0xE0,
        0xF0,0x80,0x80,0x80,0xF0, 0xE0,0x90,0x90,0x90,0xE0,
        0xF0,0x80,0xF0,0x80,0xF0, 0xF0,0x80,0xF0,0x80,0x80
    ];
    private readonly byte[] memory = new byte[MemorySize], registers = new byte[16];
    private readonly bool[] display = new bool[Width * Height], keys = new bool[16];
    private readonly ushort[] stack = new ushort[16];
    private readonly Random random;
    private int stackPointer, waitingRegister = -1, waitingKey = -1;
    public Chip8(Random? random = null) { this.random = random ?? Random.Shared; Reset(); }
    public ReadOnlyMemory<byte> Memory => memory;
    public ReadOnlyMemory<byte> Registers => registers;
    public ReadOnlyMemory<bool> Display => display;
    public ushort ProgramCounter { get; private set; }
    public ushort Index { get; private set; }
    public byte DelayTimer { get; private set; }
    public byte SoundTimer { get; private set; }
    public bool WaitingForKey => waitingRegister >= 0;

    private void Reset()
    {
        Array.Clear(memory); Array.Clear(registers); Array.Clear(display); Array.Clear(keys); Array.Clear(stack);
        Font.CopyTo(memory, FontStart);
        ProgramCounter = RomStart; Index = 0; DelayTimer = SoundTimer = 0;
        stackPointer = 0; waitingRegister = waitingKey = -1;
    }
    public void LoadRom(ReadOnlySpan<byte> rom)
    {
        if (rom.Length is < 2 or > MemorySize - RomStart)
            throw new Chip8Exception($"ROM muss 2 bis {MemorySize - RomStart} Bytes enthalten.");
        Reset(); rom.CopyTo(memory.AsSpan(RomStart));
    }
    public void SetKey(int key, bool pressed)
    {
        if ((uint)key >= 16) throw new ArgumentOutOfRangeException(nameof(key));
        // Original FX0A waits for a press and subsequent release.
        if (WaitingForKey)
        {
            if (pressed && !keys[key] && waitingKey < 0) waitingKey = key;
            if (!pressed && keys[key] && waitingKey == key)
            {
                registers[waitingRegister] = (byte)key;
                waitingRegister = waitingKey = -1;
            }
        }
        keys[key] = pressed;
    }
    public void TickTimers()
    {
        if (DelayTimer > 0) DelayTimer--;
        if (SoundTimer > 0) SoundTimer--;
    }
    private void CheckMemory(int address, int length)
    {
        if (address < 0 || address > MemorySize - length)
            throw new Chip8Exception($"Speicherzugriff außerhalb von 4 KB: 0x{address:X3}, Länge {length}.");
    }
    public void Step()
    {
        if (WaitingForKey) return;
        CheckMemory(ProgramCounter, 2);
        var address = ProgramCounter;
        var opcode = (ushort)((memory[address] << 8) | memory[address + 1]);
        ProgramCounter += 2;
        int x = (opcode >> 8) & 15, y = (opcode >> 4) & 15, n = opcode & 15, nn = opcode & 255;
        ushort nnn = (ushort)(opcode & 0xFFF);
        byte vx = registers[x], vy = registers[y];
        void Invalid() => throw new Chip8Exception($"Unbekannter Opcode 0x{opcode:X4} bei 0x{address:X3}.");
        switch (opcode >> 12)
        {
            case 0:
                if (opcode == 0x00E0) Array.Clear(display);
                else if (opcode == 0x00EE)
                {
                    if (stackPointer == 0) throw new Chip8Exception("Stack-Unterlauf bei RET.");
                    ProgramCounter = stack[--stackPointer];
                }
                else if (opcode is 0x00FB or 0x00FC or 0x00FD or 0x00FE or 0x00FF || (opcode & 0xFFF0) == 0x00C0) Invalid();
                // Other 0NNN calls targeted the historic host CPU and are ignored.
                break;
            case 1: ProgramCounter = nnn; break;
            case 2:
                if (stackPointer == stack.Length) throw new Chip8Exception("Stack-Überlauf bei CALL.");
                stack[stackPointer++] = ProgramCounter; ProgramCounter = nnn; break;
            case 3: if (vx == nn) ProgramCounter += 2; break;
            case 4: if (vx != nn) ProgramCounter += 2; break;
            case 5: if (n != 0) Invalid(); if (vx == vy) ProgramCounter += 2; break;
            case 6: registers[x] = (byte)nn; break;
            case 7: registers[x] = unchecked((byte)(vx + nn)); break;
            case 8:
                switch (n)
                {
                    case 0: registers[x] = vy; break;
                    case 1: registers[x] = (byte)(vx | vy); registers[15] = 0; break;
                    case 2: registers[x] = (byte)(vx & vy); registers[15] = 0; break;
                    case 3: registers[x] = (byte)(vx ^ vy); registers[15] = 0; break;
                    case 4: registers[x] = unchecked((byte)(vx + vy)); registers[15] = (byte)(vx + vy > 255 ? 1 : 0); break;
                    case 5: registers[x] = unchecked((byte)(vx - vy)); registers[15] = (byte)(vx >= vy ? 1 : 0); break;
                    case 6: registers[x] = (byte)(vy >> 1); registers[15] = (byte)(vy & 1); break;
                    case 7: registers[x] = unchecked((byte)(vy - vx)); registers[15] = (byte)(vy >= vx ? 1 : 0); break;
                    case 14: registers[x] = unchecked((byte)(vy << 1)); registers[15] = (byte)(vy >> 7); break;
                    default: Invalid(); break;
                }
                break;
            case 9: if (n != 0) Invalid(); if (vx != vy) ProgramCounter += 2; break;
            case 10: Index = nnn; break;
            case 11: ProgramCounter = (ushort)(nnn + registers[0]); break;
            case 12: registers[x] = (byte)(random.Next(256) & nn); break;
            case 13:
                if (n == 0) { Invalid(); break; }
                CheckMemory(Index, n);
                registers[15] = 0;
                int originX = vx % Width, originY = vy % Height;
                for (int row = 0; row < n && originY + row < Height; row++)
                    for (int bit = 0; bit < 8 && originX + bit < Width; bit++)
                    {
                        if ((memory[Index + row] & (0x80 >> bit)) == 0) continue;
                        int pixel = (originY + row) * Width + originX + bit;
                        if (display[pixel]) registers[15] = 1;
                        display[pixel] ^= true;
                    }
                break;
            case 14:
                if (nn == 0x9E) { if (vx < 16 && keys[vx]) ProgramCounter += 2; }
                else if (nn == 0xA1) { if (vx >= 16 || !keys[vx]) ProgramCounter += 2; }
                else Invalid();
                break;
            case 15:
                switch (nn)
                {
                    case 0x07: registers[x] = DelayTimer; break;
                    case 0x0A: waitingRegister = x; waitingKey = Array.FindIndex(keys, k => k); break;
                    case 0x15: DelayTimer = vx; break;
                    case 0x18: SoundTimer = vx; break;
                    case 0x1E: Index = (ushort)((Index + vx) & 0xFFF); break;
                    case 0x29: Index = (ushort)(FontStart + (vx & 15) * 5); break;
                    case 0x33:
                        CheckMemory(Index, 3);
                        memory[Index] = (byte)(vx / 100); memory[Index + 1] = (byte)(vx / 10 % 10); memory[Index + 2] = (byte)(vx % 10); break;
                    case 0x55:
                        CheckMemory(Index, x + 1); registers.AsSpan(0, x + 1).CopyTo(memory.AsSpan(Index));
                        Index = (ushort)((Index + x + 1) & 0xFFF); break;
                    case 0x65:
                        CheckMemory(Index, x + 1); memory.AsSpan(Index, x + 1).CopyTo(registers);
                        Index = (ushort)((Index + x + 1) & 0xFFF); break;
                    default: Invalid(); break;
                }
                break;
            default: Invalid(); break;
        }
    }
}
