using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using SpireLab.SpireLabCode.Character;
using SpireLab.SpireLabCode.Extensions;

namespace SpireLab.SpireLabCode.Potions;

[Pool(typeof(SpireLabPotionPool))]
public abstract class SpireLabPotion : CustomPotionModel
{
	public override string? CustomPackedImagePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionImagePath();
	public override string? CustomPackedOutlinePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionOutlineImagePath();
}
