using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace SpireLab.Visuals;

public partial class WitchMerchantVisual : NMerchantCharacter
{
    public override void _Ready()
    {
        // The base method initializes a Spine sprite. This scene uses Godot animation.
        GetNode<AnimationPlayer>("AnimationPlayer").Play("idle");
    }
}
