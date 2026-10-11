using C8Emu.Core;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace C8Emu.Input;

public static class Chip8Keyboard
{
    private static readonly Keys[] Mapping =
    [Keys.X, Keys.D1, Keys.D2, Keys.D3, Keys.Q, Keys.W, Keys.E, Keys.A,
     Keys.S, Keys.D, Keys.Z, Keys.C, Keys.D4, Keys.R, Keys.F, Keys.V];

    public static void Update(IChip8 chip, KeyboardState state, bool focused)
    {
        for (int key = 0; key < Mapping.Length; key++)
            chip.SetKey(key, focused && (state.IsKeyDown(Mapping[key]) || (key == 10 && state.IsKeyDown(Keys.Y))));
    }
}
