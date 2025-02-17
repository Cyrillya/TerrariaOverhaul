﻿using System;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using TerrariaOverhaul.Content.Buffs;
using TerrariaOverhaul.Core.Configuration;

namespace TerrariaOverhaul.Common.Magic;

public class PlayerManaRebalance : ModPlayer
{
	public static readonly ConfigEntry<bool> EnableManaRegenerationRework = new(ConfigSide.Both, true, "Magic");
	// Both-sided just in case it causes network issues. May not be true.
	public static readonly ConfigEntry<bool> EnableManaRegenerationIcon = new(ConfigSide.Both, true, "Magic");

	public static float BaseManaRegen => 7f; //20f
	public static float ManaRegenBonusMultiplier => 0.4f; // 'Mana Regeneration Band' adds whooping 25 regen per second. This exists to battle disbalance from values like that.
	public static float MinRegenVelocity => 1f;
	public static float MaxRegenVelocity => 15f;
	public static float MaxRegenVelocityMultiplier => 5f;

	private static bool IsEnabled => EnableManaRegenerationRework;

	public float VelocityManaRegenIntensity { get; private set; }
	public float VelocityManaRegenMultiplier { get; private set; } = 1f;

	public override void Load()
	{
		// This IL edit completely replaces silly vanilla mana regeneration logic.
		// Forces a constant regeneration value.
		IL_Player.UpdateManaRegen += static context => {
			var il = new ILCursor(context);

			// manaRegenCount += manaRegen;
			il.GotoNext(
				MoveType.Before,
				i => i.Match(OpCodes.Ldarg_0),
				i => i.Match(OpCodes.Ldarg_0),
				i => i.MatchLdfld(typeof(Player), nameof(Player.manaRegenCount)),
				i => i.Match(OpCodes.Ldarg_0),
				i => i.MatchLdfld(typeof(Player), nameof(Player.manaRegen)),
				i => i.Match(OpCodes.Add),
				i => i.MatchStfld(typeof(Player), nameof(Player.manaRegenCount))
			);

			il.GotoNext();
			il.EmitDelegate<Action<Player>>(static p => {
				if (IsEnabled) {
					var instance = p.GetModPlayer<PlayerManaRebalance>();
					float manaRegen = BaseManaRegen;

					// The vanilla "Staying still doubles mana regen" feature. I think it's stupid, as it encourages boring play.
					/*
					if (p.velocity.Y == 0f && Math.Abs(p.velocity.X) < 2f && p.itemAnimation <= 0 && !p.controlUseItem && p.controlLeft == p.controlRight) {
						p.manaRegen *= 2;

						if (p.statMana < p.statManaMax2) {
							p.AddBuff(ModContent.BuffType<ManaAbsorption>(), 2);
						}
					}
					*/

					// Instead of the above, let's do the opposite and speed up mana regen from MOVING!
					if (p.velocity != Vector2.Zero) {
						instance.VelocityManaRegenIntensity = Math.Min(1f, Math.Max(0f, p.velocity.Length() - MinRegenVelocity) / (MinRegenVelocity + MaxRegenVelocity));
						instance.VelocityManaRegenMultiplier = 1f + instance.VelocityManaRegenIntensity * (MaxRegenVelocityMultiplier - 1f);

						manaRegen *= instance.VelocityManaRegenMultiplier;
						
						if (EnableManaRegenerationIcon && instance.VelocityManaRegenMultiplier > 1f && p.statMana < p.statManaMax2) {
							p.AddBuff(ModContent.BuffType<ManaAbsorption>(), 30);
						}
					}

					manaRegen += p.manaRegenBonus * ManaRegenBonusMultiplier;

					// Mana regen buff
					if (p.manaRegenBuff) {
						manaRegen *= 1.5f; // '*= 2;' in vanilla.
					}

					// Stop regeneration when firing a magic weapon.
					// This is removed, for various reasons...
					/*
					if (p.itemAnimation > 0 && p.HeldItem.mana > 0) {
						manaRegen = 0f;
					}
					*/

					p.manaRegen = (int)Math.Floor(manaRegen);
				}
			});
			il.Emit(OpCodes.Ldarg_0);
		};
	}
}
