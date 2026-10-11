using C8Emu.Core;
using C8Emu.Graphics;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("C8Emu – CHIP-8\nAufruf: C8Emu <ROM-Datei> [Instruktionen/s]\nStandard: 700. Escape: Beenden. F5: Neustart.\nTasten: 1234 / QWER / ASDF / YXCV (auch ZXCV).");
    return args.Length == 0 ? 1 : 0;
}
try
{
    int speed = 700;
    if (args.Length > 2 || (args.Length == 2 && (!int.TryParse(args[1], out speed) || speed is < 1 or > 100000)))
        throw new ArgumentException("Geschwindigkeit muss zwischen 1 und 100000 Instruktionen/s liegen.");
    var rom = File.ReadAllBytes(args[0]);
    var chip = new Chip8();
    chip.LoadRom(rom);
    using var window = new Chip8Window(chip, rom, Path.GetFileName(args[0]), speed);
    window.Run();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"C8Emu: {ex.Message}");
    return 1;
}
