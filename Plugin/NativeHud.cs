using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DiegeticUIMod
{
	/// <summary>
	/// Hides the game's own HUD pieces that this mod redraws (needs icons, clock, money, hand items, the event
	/// feed) by shrinking them to zero size every frame, after the game updated them. Their logic keeps running,
	/// so queues, sounds and timers behave as normal, and turning the mod off puts them back exactly.
	/// </summary>
	internal static class NativeHud
	{
		private static readonly Dictionary<Transform, Vector3> hidden = new Dictionary<Transform, Vector3>();
		private static readonly List<ShowTime> clocks = new List<ShowTime>();
		private static float nextClockScan;

		internal static void Hide(bool needs, bool clock, bool money, bool hands, bool feed)
		{
			try
			{
				if (needs && PlayerStats.instance != null)
					foreach (PlayerStatsUI ui in PlayerStats.instance.playerStatsUIs)
					{
						if (ui == null) continue;
						foreach (string f in new[] { "HealthBar", "BleedingBar", "HungerBar", "ThirstBar", "TirednessBar", "BowelBar", "AlcoholNeedBar", "MushroomNeedBar", "SmokesNeedBar", "DepressionBar", "DuckBar", "HygieneBar", "GamblingBar" })
						{
							var bar = Traverse.Create(ui).Field(f).GetValue<PlayerStatUI>();
							if (bar != null) Zero(bar.transform);
						}
					}
				if (clock)
				{
					// every on-screen clock text (gameplay and menus); in-world clocks (timeMesh) stay
					if (Time.unscaledTime >= nextClockScan)
					{
						nextClockScan = Time.unscaledTime + 2f;
						clocks.Clear();
						foreach (ShowTime st in UnityEngine.Object.FindObjectsOfType<ShowTime>(true))
							if (st != null && Traverse.Create(st).Field("timeMesh").GetValue<object>() == null) clocks.Add(st);
					}
					foreach (ShowTime st in clocks) if (st != null) Zero(st.transform);
				}
				if (money && Money.instance != null)
				{
					var panels = Traverse.Create(Money.instance).Field("moneyPanels").GetValue<List<MoneyPanel>>();
					if (panels != null) foreach (MoneyPanel p in panels) if (p != null) Zero(p.transform);
				}
				if (hands && HandItemsUI.instance != null)
				{
					foreach (string f in new[] { "rightHandItemPanel", "leftHandItemPanel" })
					{
						var go = Traverse.Create(HandItemsUI.instance).Field(f).GetValue<GameObject>();
						if (go != null) Zero(go.transform);
					}
				}
				if (feed && Notifications.instance != null)
				{
					var panel = Traverse.Create(Notifications.instance).Field("notificationPanel").GetValue<GameObject>();
					if (panel != null) Zero(panel.transform);
				}
			}
			catch (Exception e) { DiegeticPlugin.WarnOnce("hide", "Hiding the game's HUD: " + e.Message); }
		}

		private static void Zero(Transform t)
		{
			if (t == null) return;
			if (!hidden.ContainsKey(t)) hidden[t] = t.localScale == Vector3.zero ? Vector3.one : t.localScale;
			t.localScale = Vector3.zero;
		}

		internal static void RestoreAll()
		{
			foreach (var kv in hidden) if (kv.Key != null) kv.Key.localScale = kv.Value;
			hidden.Clear();
		}

		// Whether the game currently shows its gameplay HUD (it hides it for loading, death, dialogue, building,
		// photo mode and the Hide HUD key). Judged from the hand-items panel, which sits on the gameplay HUD.
		internal static bool GameplayHudVisible
		{
			get
			{
				try
				{
					if (HandItemsUI.instance == null) return false;
					return EffectivelyVisible(HandItemsUI.instance.transform);
				}
				catch (Exception) { return false; }
			}
		}

		internal static bool EffectivelyVisible(Transform t)
		{
			if (t == null || !t.gameObject.activeInHierarchy) return false;
			float a = 1f;
			for (Transform p = t; p != null; p = p.parent)
			{
				foreach (CanvasGroup cg in p.GetComponents<CanvasGroup>())
				{
					if (!cg.enabled) continue;
					a *= cg.alpha;
					if (cg.ignoreParentGroups) return a > 0.02f;
				}
				var canvas = p.GetComponent<Canvas>();
				if (canvas != null && !canvas.enabled) return false;
			}
			return a > 0.02f;
		}
	}
}
