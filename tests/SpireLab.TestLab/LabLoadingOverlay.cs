using Godot;

namespace SpireLab.TestLab;

internal sealed class LabLoadingOverlay : IDisposable
{
    private readonly Window _window;
    private readonly bool _previousInputDisabled;
    private readonly CanvasLayer _layer = new() { Layer = 80 };
    private bool _disposed;

    public LabLoadingOverlay(Window window)
    {
        _window = window;
        _previousInputDisabled = window.GuiDisableInput;
        window.GuiDisableInput = true;
        window.AddChild(_layer);

        var background = new ColorRect { Color = new Color("17151c"), MouseFilter = Control.MouseFilterEnum.Ignore };
        _layer.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var label = new Label
        {
            Text = "正在进入魔女测试战斗…",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 28);
        background.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _layer.Visible = false;
        _layer.QueueFree();
        _window.GuiDisableInput = _previousInputDisabled;
    }
}
