using C8E.Core;
using C8E.Graphics;
using System.Runtime.CompilerServices;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("""
        C8E - Chip 8 Emulator for Windows
        --------------------------------- 

        Usage: C8E <ROM file> [instructions/s]

        instructions:
        Escape                  : Exit                                                           
        F5                      : Restart
        Keys                    : 1234 / QWER / ASDF / YXCV (also ZXCV)

        version beta 1.0
        """);

    return args.Length == 0 ? 1 : 0;
}


return Run();

int Run ()
{
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
        Console.Error.WriteLine($"C8E: {ex.Message}");
        return 1;
    }
}