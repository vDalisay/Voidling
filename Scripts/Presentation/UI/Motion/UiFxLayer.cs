using Godot;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// A canvas above every menu for effects that must outlive the control that caused them: a burst
/// from a Buy button that has just been rebuilt, sprouts flying to a wallet, floating "+N". It never
/// takes input. Coordinates are the UI's own (640x360), so a control's global rect maps 1:1.
/// Effects start their motion in <c>_Ready</c>, so they work even on the frame the layer is made.
/// </summary>
public static class UiFxLayer
{
    private const string LayerName = "UiFxLayer";
    private static CanvasLayer? _layer;

    public static Control For(Node anyNode)
    {
        if (_layer == null || !GodotObject.IsInstanceValid(_layer) || _layer.IsQueuedForDeletion())
        {
            var root = anyNode.GetTree().Root;
            _layer = root.GetNodeOrNull<CanvasLayer>(LayerName);
            if (_layer == null)
            {
                _layer = new CanvasLayer { Name = LayerName, Layer = 60 };
                var canvas = new Control { Name = "Canvas", MouseFilter = Control.MouseFilterEnum.Ignore };
                canvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _layer.AddChild(canvas);
                root.CallDeferred(Node.MethodName.AddChild, _layer);
            }
        }
        return _layer.GetNode<Control>("Canvas");
    }
}
