using C8E.Core;
using C8E.Input;
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace C8E.Graphics;

public sealed class Chip8Window : GameWindow
{
    private readonly IChip8 chip;
    private readonly byte[] rom;
    private readonly int instructionsPerSecond;
    private readonly string romName;
    private double cpuTime, timerTime, displayTime;
    private readonly Chip8Display display = new();
    private readonly Buzzer buzzer = new();
    public Chip8Window(IChip8 chip, byte[] rom, string romName, int instructionsPerSecond = 700)
        : base(new GameWindowSettings { UpdateFrequency = 120 }, new NativeWindowSettings
        {
            ClientSize = new Vector2i(1024, 512), Title = $"C8E – {romName}",
            API = ContextAPI.OpenGL, APIVersion = new Version(3, 3), Profile = ContextProfile.Core
        })
    {
        if (instructionsPerSecond is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(instructionsPerSecond));
        this.chip = chip; this.rom = (byte[])rom.Clone(); this.romName = romName;
        this.instructionsPerSecond = instructionsPerSecond;
    }
    protected override void OnLoad() { base.OnLoad(); VSync = VSyncMode.On; buzzer.Initialize(); }
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        if (KeyboardState.IsKeyDown(Keys.Escape)) { Close(); return; }
        if (KeyboardState.IsKeyPressed(Keys.F5)) { chip.LoadRom(rom); display.Reset(); cpuTime = timerTime = displayTime = 0; }
        Chip8Keyboard.Update(chip, KeyboardState, IsFocused);
        // Bound catch-up after dragging the window or hitting a breakpoint.
        double elapsed = Math.Min(args.Time, 0.1);
        cpuTime += elapsed; timerTime += elapsed; displayTime += elapsed;
        double instructionPeriod = 1.0 / instructionsPerSecond;
        // Interleave timers and instructions so timer writes occur in time order.
        while (cpuTime >= instructionPeriod || timerTime >= 1.0 / 60)
        {
            if (timerTime >= 1.0 / 60 && (cpuTime < instructionPeriod || timerTime - 1.0 / 60 >= cpuTime - instructionPeriod))
            { chip.TickTimers(); timerTime -= 1.0 / 60; }
            else
            {
                chip.Step(); cpuTime -= instructionPeriod;
                display.Observe(chip.Display.Span, displayTime - cpuTime);
            }
        }
        Title = $"C8E – {romName}{(chip.SoundTimer > 0 ? " – ♪" : "")}";
        buzzer.SetPlaying(chip.SoundTimer > 0);
    }
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.Disable(EnableCap.ScissorTest);
        GL.ClearColor(0f, 0f, 0f, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        int width = FramebufferSize.X, height = FramebufferSize.Y;
        if (width > 0 && height > 0)
        {
            // Keep a small black border around the Octo-colored display.
            int padding = Math.Min(16, Math.Max(0, (Math.Min(width, height) - 1) / 2));
            double scale = Math.Min((width - 2 * padding) / 64.0, (height - 2 * padding) / 32.0);
            if (scale >= 1) scale = Math.Floor(scale);
            int left = (width - (int)(64 * scale)) / 2, bottom = (height - (int)(32 * scale)) / 2;
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(left, bottom, (int)(64 * scale), (int)(32 * scale));
            GL.ClearColor(0x99 / 255f, 0x66 / 255f, 0f, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.ClearColor(1f, 0xCC / 255f, 0f, 1);
            var pixels = display.Snapshot(displayTime).Span;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 64; x++)
                    if (pixels[y * 64 + x])
                    {
                        int x0 = left + (int)(x * scale), y0 = bottom + (int)((31 - y) * scale);
                        GL.Scissor(x0, y0, (int)((x + 1) * scale) - (int)(x * scale), (int)((32 - y) * scale) - (int)((31 - y) * scale));
                        GL.Clear(ClearBufferMask.ColorBufferBit);
                    }
            GL.Disable(EnableCap.ScissorTest);
        }
        SwapBuffers();
    }
    protected override void OnUnload()
    {
        buzzer.Dispose();
        base.OnUnload();
    }

    private sealed class Buzzer : IDisposable
    {
        private ALDevice device;
        private ALContext context;
        private int source, buffer;
        private bool playing;
        public void Initialize()
        {
            try
            {
                device = ALC.OpenDevice(null);
                if (device == ALDevice.Null) throw new InvalidOperationException("No audio device available.");
                context = ALC.CreateContext(device, new ALContextAttributes());
                if (context == ALContext.Null || !ALC.MakeContextCurrent(context))
                    throw new InvalidOperationException("Could not create audio context.");
                buffer = AL.GenBuffer(); source = AL.GenSource();
                // 100 ms contains exactly 44 periods of a 440 Hz tone.
                var samples = new short[4410];
                for (int i = 0; i < samples.Length; i++) samples[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100) * 4000);
                AL.BufferData(buffer, ALFormat.Mono16, samples, 44100);
                AL.Source(source, ALSourcei.Buffer, buffer);
                AL.Source(source, ALSourceb.Looping, true);
                if (AL.GetError() != ALError.NoError) throw new InvalidOperationException("Could not create audio buffer.");
            }
            catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException or TypeInitializationException)
            {
                Console.Error.WriteLine($"Sound disabled: {ex.Message} The sound timer remains visible in the window title.");
                Dispose();
            }
        }
        public void SetPlaying(bool enabled)
        {
            if (source == 0 || playing == enabled) return;
            if (enabled) AL.SourcePlay(source); else AL.SourceStop(source);
            playing = enabled;
        }
        public void Dispose()
        {
            if (source != 0) { AL.SourceStop(source); AL.DeleteSource(source); source = 0; }
            if (buffer != 0) { AL.DeleteBuffer(buffer); buffer = 0; }
            if (context != ALContext.Null) { ALC.MakeContextCurrent(ALContext.Null); ALC.DestroyContext(context); context = ALContext.Null; }
            if (device != ALDevice.Null) { ALC.CloseDevice(device); device = ALDevice.Null; }
            playing = false;
        }
    }
}
