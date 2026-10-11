using C8E.Core;
using C8E.Graphics;
using OpenTK.Graphics.OpenGL4;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"ERROR: {name}");
    checks++;
}
Chip8 Run(int steps, params ushort[] code)
{
    var chip = new Chip8(new Random(123));
    chip.LoadRom(code.SelectMany(op => new byte[] { (byte)(op >> 8), (byte)op }).ToArray());
    for (int i = 0; i < steps; i++) chip.Step();
    return chip;
}
void Throws(Action action, string name)
{
    try { action(); }
    catch (Chip8Exception) { checks++; return; }
    throw new Exception($"ERROR: {name}");
}
byte V(Chip8 chip, int index) => chip.Registers.Span[index];
var c = Run(3, 0x60FF, 0x7002, 0x6101);
Check(V(c, 0) == 1 && V(c, 1) == 1, "Load and 8-bit overflow");
c = Run(3, 0x60FF, 0x6101, 0x8014);
Check(V(c, 0) == 0 && V(c, 15) == 1, "Addition with carry");
c = Run(3, 0x6005, 0x6105, 0x8015);
Check(V(c, 0) == 0 && V(c, 15) == 1, "Subtraction with equal operands");
c = Run(3, 0x6009, 0x6105, 0x8017);
Check(V(c, 0) == 252 && V(c, 15) == 0, "Reverse subtraction with borrow");
c = Run(3, 0x6FFF, 0x6101, 0x8F14);
Check(V(c, 15) == 1, "VF as destination register");
c = Run(3, 0x60FF, 0x6103, 0x8016);
Check(V(c, 0) == 1 && V(c, 15) == 1, "Classic right shift from VY");
c = Run(3, 0x6000, 0x6181, 0x801E);
Check(V(c, 0) == 2 && V(c, 15) == 1, "Left shift");
foreach (ushort op in new ushort[] { 0x8011, 0x8012, 0x8013 })
{
    c = Run(4, 0x6F01, 0x600F, 0x61F0, op);
    Check(V(c, 15) == 0 && V(c, 0) == (op == 0x8012 ? 0 : 255), "Logic operations and VF");
}
c = Run(3, 0x6107, 0x8010, 0xA345);
Check(V(c, 0) == 7 && c.Index == 0x345, "Register copy and index");
foreach (ushort skip in new ushort[] { 0x3007, 0x4008, 0x5010, 0x9010 })
{
    c = Run(4, 0x6007, (ushort)(skip == 0x9010 ? 0x6108 : 0x6107), skip, 0x6200, 0x6209);
    Check(V(c, 2) == 9, "Conditional skip");
}
c = Run(4, 0x2206, 0x6209, 0x1204, 0x6107, 0x00EE);
Check(V(c, 1) == 7 && V(c, 2) == 9 && c.ProgramCounter == 0x204, "CALL and RET");
c = Run(2, 0x6004, 0xB200);
Check(c.ProgramCounter == 0x204, "Jump plus V0");
c = Run(1, 0xC10F);
Check(V(c, 1) <= 15, "Random mask");
c = Run(3, 0x60EA, 0xA300, 0xF033);
Check(c.Memory.Span[0x300] == 2 && c.Memory.Span[0x301] == 3 && c.Memory.Span[0x302] == 4, "BCD");
c = Run(7, 0x600A, 0x610B, 0xA300, 0xF155, 0x6000, 0xA300, 0xF165);
Check(V(c, 0) == 10 && V(c, 1) == 11 && c.Index == 0x302, "Store/load with index increment");
c = Run(5, 0x6000, 0x6100, 0xF029, 0xD015, 0xD015, 0x00E0);
Check(c.Display.Span.ToArray().All(p => !p) && V(c, 15) == 1, "XOR and collision");
c = Run(4, 0x603F, 0x611F, 0xA050, 0xD015);
Check(c.Display.Span.ToArray().Count(p => p) == 1 && c.Display.Span[31 * 64 + 63], "Clip sprites at display edges");
c = Run(5, 0x6000, 0x6100, 0xA050, 0xD015, 0x00E0);
Check(c.Display.Span.ToArray().All(p => !p), "Clear display");
c = Run(3, 0x6003, 0xF015, 0xF018);
c.TickTimers(); c.TickTimers();
Check(c.DelayTimer == 1 && c.SoundTimer == 1, "Both timers");
c.TickTimers(); c.TickTimers();
Check(c.DelayTimer == 0 && c.SoundTimer == 0, "Timers without underflow");
c = Run(1, 0xF10A, 0x6209);
c.Step();
Check(c.WaitingForKey && c.ProgramCounter == 0x202, "FX0A blocks CPU");
c.SetKey(10, true); c.Step();
Check(c.WaitingForKey, "FX0A waits for key release");
c.SetKey(10, false); c.Step();
Check(!c.WaitingForKey && V(c, 1) == 10 && V(c, 2) == 9, "Resume after FX0A");
c = Run(1, 0x600A, 0xE09E, 0x6100, 0x6109);
c.SetKey(10, true); c.Step(); c.Step();
Check(V(c, 1) == 9, "EX9E");
c = Run(3, 0x600A, 0xE0A1, 0x6100, 0x6109);
Check(V(c, 1) == 9, "EXA1");
c = Run(3, 0x6002, 0xF015, 0xF107);
Check(V(c, 1) == 2, "Read timer");
c = Run(3, 0xAFFF, 0x6002, 0xF01E);
Check(c.Index == 1, "Index overflow");
c.LoadRom(new byte[] { 0x12, 0x00 });
Check(c.ProgramCounter == 0x200 && c.Index == 0 && c.DelayTimer == 0 && V(c, 0) == 0, "ROM restart");
Throws(() => Run(1, 0x00EE), "Stack underflow");
Throws(() => Run(17, 0x2200), "Stack overflow");
Throws(() => Run(1, 0x5121), "Invalid opcode");
Throws(() => Run(2, 0xAFFF, 0xF133), "BCD memory boundary");
Throws(() => Run(2, 0x1FFF), "Instruction fetch memory boundary");
Throws(() => new Chip8().LoadRom(new byte[3585]), "ROM too large");
Throws(() => new Chip8().LoadRom([]), "Empty ROM");
// Verify persistence independently of render rate and CPU collision behavior.
var display = new Chip8Display();
var rawPixels = new bool[Chip8.Width * Chip8.Height];
rawPixels[0] = true;
display.Observe(rawPixels, 0);
rawPixels[0] = false;
display.Observe(rawPixels, 0.001);
Check(display.Snapshot(0.020).Span[0] && !rawPixels[0], "Transient erase stays visible without changing CPU pixels");
Check(!display.Snapshot(0.034).Span[0], "Persistent erase expires after 33 ms");
rawPixels[1] = true;
display.Observe(rawPixels, 0.040);
Check(display.Snapshot(0.040).Span[1], "New pixels appear immediately");
display.Reset();
Check(display.Snapshot(0).Span.ToArray().All(pixel => !pixel), "Restart clears display persistence");

// A general XOR erase / input handling / redraw / timer loop, independent of person.ch8.
ushort[] movingCode = [0x6000,0x6100,0xA050,0xD015,0xD015,0x6200,0xE2A1,0x7101,0x6200,0xE2A1,0x7001,0x6200,0xE2A1,0x7101,0x6200,0xE2A1,0x7001,0xD015,0x6303,0xF315,0xF307,0x3300,0x1228,0x1208];
var moving = Run(0, movingCode);
var stable = new Chip8Display();
int blankRawFrames = 0, blankPresentedFrames = 0;
const int totalSteps = 3500;
double nextFrame = 1.0 / 60, nextTick = 1.0 / 60;
for (int step = 1; step <= totalSteps; step++)
{
    double time = step / 700.0;
    moving.Step();
    stable.Observe(moving.Display.Span, time);
    while (nextTick <= time) { moving.TickTimers(); nextTick += 1.0 / 60; }
    while (nextFrame <= time)
    {
        if (time > 0.050)
        {
            if (!moving.Display.Span.Contains(true)) blankRawFrames++;
            if (!stable.Snapshot(time).Span.Contains(true)) blankPresentedFrames++;
        }
        nextFrame += 1.0 / 60;
    }
}
Check(blankRawFrames > 0, "Reproduce erase/redraw flicker at 700 instructions/s");
Check(blankPresentedFrames == 0, "Presentation hides all transient blank frames over five seconds");
Console.WriteLine($"Flicker regression: {blankRawFrames} raw blank frames, {blankPresentedFrames} presented blank frames.");
Console.WriteLine($"{checks} core and presentation checks passed.");

if (args.Contains("--window"))
{
    byte[] rom = [0x60,0x00,0x61,0x00,0xA0,0x50,0xD0,0x15,0x12,0x08];
    var chip = new Chip8(); chip.LoadRom(rom);
    using var window = new Chip8Window(chip, rom, "Graphics test");
    window.IsVisible = false;
    int frames = 0, sizesChecked = 0;
    void CheckColor(int x, int y, byte red, byte green, byte blue, string name)
    {
        byte[] pixel = new byte[4];
        GL.ReadPixels(x, y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        Check(Math.Abs(pixel[0] - red) <= 1 && Math.Abs(pixel[1] - green) <= 1 && Math.Abs(pixel[2] - blue) <= 1, name);
    }
    window.RenderFrame += _ =>
    {
        if (++frames < 4) return;
        int width = window.FramebufferSize.X, height = window.FramebufferSize.Y;
        int scale = Math.Min((width - 32) / 64, (height - 32) / 32);
        int left = (width - 64 * scale) / 2, bottom = (height - 32 * scale) / 2;
        GL.ReadBuffer(ReadBufferMode.Front);
        CheckColor(left + scale / 2, bottom + 31 * scale + scale / 2, 255, 204, 0, "Octo gold sprite pixel");
        CheckColor(width / 2, height / 2, 153, 102, 0, "Octo ochre display background");
        CheckColor(left - 1, height / 2, 0, 0, 0, "Black left border");
        CheckColor(left + 64 * scale, height / 2, 0, 0, 0, "Black right border");
        CheckColor(width / 2, bottom - 1, 0, 0, 0, "Black bottom border");
        CheckColor(width / 2, bottom + 32 * scale, 0, 0, 0, "Black top border");
        if (++sizesChecked == 1)
        {
            window.ClientSize = new OpenTK.Mathematics.Vector2i(900, 700);
            frames = 0;
        }
        else window.Close();
    };
    window.Run();
    Console.WriteLine("OpenTK window and pixel output checks passed.");
}
