using C8Emu.Core;
using C8Emu.Graphics;
using OpenTK.Graphics.OpenGL4;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FEHLER: {name}");
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
    throw new Exception($"FEHLER: {name}");
}
byte V(Chip8 chip, int index) => chip.Registers.Span[index];
var c = Run(3, 0x60FF, 0x7002, 0x6101);
Check(V(c, 0) == 1 && V(c, 1) == 1, "Load und 8-Bit-Ueberlauf");
c = Run(3, 0x60FF, 0x6101, 0x8014);
Check(V(c, 0) == 0 && V(c, 15) == 1, "Addition mit Carry");
c = Run(3, 0x6005, 0x6105, 0x8015);
Check(V(c, 0) == 0 && V(c, 15) == 1, "Subtraktion Gleichheit");
c = Run(3, 0x6009, 0x6105, 0x8017);
Check(V(c, 0) == 252 && V(c, 15) == 0, "Reverse Subtraktion mit Borrow");
c = Run(3, 0x6FFF, 0x6101, 0x8F14);
Check(V(c, 15) == 1, "VF als Zielregister");
c = Run(3, 0x60FF, 0x6103, 0x8016);
Check(V(c, 0) == 1 && V(c, 15) == 1, "Klassischer Shift rechts aus VY");
c = Run(3, 0x6000, 0x6181, 0x801E);
Check(V(c, 0) == 2 && V(c, 15) == 1, "Shift links");
foreach (ushort op in new ushort[] { 0x8011, 0x8012, 0x8013 })
{
    c = Run(4, 0x6F01, 0x600F, 0x61F0, op);
    Check(V(c, 15) == 0 && V(c, 0) == (op == 0x8012 ? 0 : 255), "Logik und VF");
}
c = Run(3, 0x6107, 0x8010, 0xA345);
Check(V(c, 0) == 7 && c.Index == 0x345, "Registerkopie und Index");
foreach (ushort skip in new ushort[] { 0x3007, 0x4008, 0x5010, 0x9010 })
{
    c = Run(4, 0x6007, (ushort)(skip == 0x9010 ? 0x6108 : 0x6107), skip, 0x6200, 0x6209);
    Check(V(c, 2) == 9, "Bedingtes Ueberspringen");
}
c = Run(4, 0x2206, 0x6209, 0x1204, 0x6107, 0x00EE);
Check(V(c, 1) == 7 && V(c, 2) == 9 && c.ProgramCounter == 0x204, "CALL und RET");
c = Run(2, 0x6004, 0xB200);
Check(c.ProgramCounter == 0x204, "Sprung plus V0");
c = Run(1, 0xC10F);
Check(V(c, 1) <= 15, "Zufallsmaske");
c = Run(3, 0x60EA, 0xA300, 0xF033);
Check(c.Memory.Span[0x300] == 2 && c.Memory.Span[0x301] == 3 && c.Memory.Span[0x302] == 4, "BCD");
c = Run(7, 0x600A, 0x610B, 0xA300, 0xF155, 0x6000, 0xA300, 0xF165);
Check(V(c, 0) == 10 && V(c, 1) == 11 && c.Index == 0x302, "Speichern/Laden mit Indexfortschritt");
c = Run(5, 0x6000, 0x6100, 0xF029, 0xD015, 0xD015, 0x00E0);
Check(c.Display.Span.ToArray().All(p => !p) && V(c, 15) == 1, "XOR und Kollision");
c = Run(4, 0x603F, 0x611F, 0xA050, 0xD015);
Check(c.Display.Span.ToArray().Count(p => p) == 1 && c.Display.Span[31 * 64 + 63], "Sprites am Rand abschneiden");
c = Run(5, 0x6000, 0x6100, 0xA050, 0xD015, 0x00E0);
Check(c.Display.Span.ToArray().All(p => !p), "Display loeschen");
c = Run(3, 0x6003, 0xF015, 0xF018);
c.TickTimers(); c.TickTimers();
Check(c.DelayTimer == 1 && c.SoundTimer == 1, "Beide Timer");
c.TickTimers(); c.TickTimers();
Check(c.DelayTimer == 0 && c.SoundTimer == 0, "Timer ohne Unterlauf");
c = Run(1, 0xF10A, 0x6209);
c.Step();
Check(c.WaitingForKey && c.ProgramCounter == 0x202, "FX0A blockiert CPU");
c.SetKey(10, true); c.Step();
Check(c.WaitingForKey, "FX0A wartet auf Loslassen");
c.SetKey(10, false); c.Step();
Check(!c.WaitingForKey && V(c, 1) == 10 && V(c, 2) == 9, "FX0A fortsetzen");
c = Run(1, 0x600A, 0xE09E, 0x6100, 0x6109);
c.SetKey(10, true); c.Step(); c.Step();
Check(V(c, 1) == 9, "EX9E");
c = Run(3, 0x600A, 0xE0A1, 0x6100, 0x6109);
Check(V(c, 1) == 9, "EXA1");
c = Run(3, 0x6002, 0xF015, 0xF107);
Check(V(c, 1) == 2, "Timer lesen");
c = Run(3, 0xAFFF, 0x6002, 0xF01E);
Check(c.Index == 1, "Index-Ueberlauf");
c.LoadRom(new byte[] { 0x12, 0x00 });
Check(c.ProgramCounter == 0x200 && c.Index == 0 && c.DelayTimer == 0 && V(c, 0) == 0, "ROM-Neustart");
Throws(() => Run(1, 0x00EE), "Stack-Unterlauf");
Throws(() => Run(17, 0x2200), "Stack-Ueberlauf");
Throws(() => Run(1, 0x5121), "Ungueltiger Opcode");
Throws(() => Run(2, 0xAFFF, 0xF133), "Speichergrenze BCD");
Throws(() => Run(2, 0x1FFF), "Speichergrenze Fetch");
Throws(() => new Chip8().LoadRom(new byte[3585]), "ROM zu gross");
Throws(() => new Chip8().LoadRom([]), "ROM leer");
Console.WriteLine($"{checks} Core-Pruefungen bestanden.");

if (args.Contains("--window"))
{
    byte[] rom = [0x60,0x00,0x61,0x00,0xA0,0x50,0xD0,0x15,0x12,0x08];
    var chip = new Chip8(); chip.LoadRom(rom);
    using var window = new Chip8Window(chip, rom, "Grafiktest");
    window.IsVisible = false;
    int frames = 0;
    window.RenderFrame += _ =>
    {
        if (++frames < 4) return;
        byte[] pixel = new byte[4];
        GL.ReadBuffer(ReadBufferMode.Front);
        GL.ReadPixels(8, window.FramebufferSize.Y - 8, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
        Check(pixel[1] > 200 && pixel[0] > 100, "OpenGL zeigt Sprite oben links");
        window.Close();
    };
    window.Run();
    Console.WriteLine("OpenTK-Fenster und Pixel-Ausgabe bestanden.");
}
