#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using Qud.UI;
using XRL.World.Parts;

namespace BetterXPReadout
{
	[HarmonyPatch(typeof(PlayerStatusBar))]
	[HarmonyPatch(nameof(PlayerStatusBar.Update))]
	[HarmonyPatch(new Type[] { })]
	public class PlayerStatusBar_Update
	{
		public static void Detour(PlayerStatusBar self)
		{
			var stats = AccessTools.FieldRefAccess<PlayerStatusBar, Dictionary<string, string>>(self, "playerStats");
			ref bool dirty = ref AccessTools.FieldRefAccess<PlayerStatusBar, bool>(self, "playerStatsDirty");
			lock (stats) {
				self.TopLeftSecondaryStatBlock
					.GetComponent<StatusBarStatBlock>()
					.UpdateStats(stats);
				int level = int.Parse(stats["Level"]);
				int xpCurrent = int.Parse(stats["XP"]);
				int xpLevelCurrent = Leveler.GetXPForLevel(level);
				int xpLevelNext = Leveler.GetXPForLevel(level + 1);
				self.XPBar.text.SetText($"LVL: {level} Exp: {xpCurrent - xpLevelCurrent} / {xpLevelNext - xpLevelCurrent}");
				self.XPBar.BarStart = 0;
				self.XPBar.BarEnd = xpLevelNext - xpLevelCurrent;
				self.XPBar.BarValue = xpCurrent - xpLevelCurrent;
				self.XPBar.UpdateBar();
				dirty = false;
			}
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var cached = instructions.ToList();
			var matcher = new CodeMatcher(cached);
			int start;
			int stop;

			matcher.MatchEndForward(new CodeMatch[] {
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldfld, AccessTools.Field(
					type: typeof(PlayerStatusBar),
					name: "playerStatsDirty"
				)),
				new(OpCodes.Brfalse),
			});
			if (matcher.IsValid) {
				start = matcher.Pos + 1;
			} else {
				Logger.buildLog.Error("Failed to locate stat block update start position!");
				return cached;
			}

			matcher.MatchEndForward(new CodeMatch[] {
				new(OpCodes.Ldloc_1),
				new(OpCodes.Brfalse_S),
				new(OpCodes.Ldloc_2),
				new(OpCodes.Call, AccessTools.Method(
					type: typeof(Monitor),
					name: nameof(Monitor.Exit),
					parameters: new Type[] { typeof(object) }
				)),
				new(OpCodes.Endfinally),
			});
			if (matcher.IsValid) {
				stop = matcher.Pos + 1;
			} else {
				Logger.buildLog.Error("Failed to locate stat block update end position!");
				return cached;
			}

			var result = new CodeMatcher(cached)
				.Advance(start + 1)
				.RemoveInstructions(stop - start)
				.Insert(new CodeInstruction[] {
					new(OpCodes.Ldarg_0),
					new(OpCodes.Call, AccessTools.Method(
						type: typeof(PlayerStatusBar_Update),
						name: nameof(Detour),
						parameters: new Type[] { typeof(PlayerStatusBar) }
					))
				})
				.Instructions();

			return result;
		}
	}
}
