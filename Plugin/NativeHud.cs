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
							var bar = Field(typeof(PlayerStatsUI), f).GetValue(ui) as PlayerStatUI;
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

		private static readonly Dictionary<string, System.Reflection.FieldInfo> fields = new Dictionary<string, System.Reflection.FieldInfo>();
		private static System.Reflection.FieldInfo Field(Type t, string name)
		{
			string k = t.Name + "." + name;
			System.Reflection.FieldInfo f;
			if (!fields.TryGetValue(k, out f)) { f = AccessTools.Field(t, name); fields[k] = f; }
			return f;
		}

		private static void Zero(Transform t)
		{
			if (t == null) return;
			if (!hidden.ContainsKey(t)) hidden[t] = t.localScale == Vector3.zero ? Vector3.one : t.localScale;
			t.localScale = Vector3.zero;
		}

		// ---- The game's look-at text ("Glue / Amount 10/10") sits where the needs strip is: lift it above the strip ----

		private static Transform useTextRoot;
		private static Vector3 useTextHome;
		private static bool useTextLifted;
		private static readonly Vector3[] corners = new Vector3[4];

		/// <summary>Keeps the look-at text's lowest edge above <paramref name="clearPx"/> screen pixels from the bottom.</summary>
		internal static void LiftUseText(bool on, float clearPx)
		{
			try
			{
				if (UseText.instance == null) return;
				if (useTextRoot == null)
				{
					var go = Traverse.Create(UseText.instance).Field("useTextUI").GetValue<GameObject>();
					if (go == null) return;
					useTextRoot = go.transform;
					useTextHome = useTextRoot.localPosition;
				}
				useTextRoot.localPosition = useTextHome;
				useTextLifted = false;
				if (!on) return;
				// lowest screen edge of the text it actually shows (rendered bounds, not the text boxes)
				float low = float.MaxValue, worldPerPx = 1f;
				foreach (TMPro.TextMeshProUGUI t in useTextRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(false))
				{
					if (t == null || string.IsNullOrEmpty(t.text) || !t.isActiveAndEnabled) continue;
					Bounds bd = t.textBounds;
					if (bd.size.y <= 0f) continue;
					Canvas c = t.canvas;
					Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
					Vector3 w0 = t.transform.TransformPoint(new Vector3(bd.center.x, bd.min.y, 0f));
					Vector3 w1 = t.transform.TransformPoint(new Vector3(bd.center.x, bd.max.y, 0f));
					Vector2 p0 = RectTransformUtility.WorldToScreenPoint(cam, w0), p1 = RectTransformUtility.WorldToScreenPoint(cam, w1);
					if (p0.y < low)
					{
						low = p0.y;
						worldPerPx = (p1.y - p0.y) > 0.5f ? Vector3.Distance(w0, w1) / (p1.y - p0.y) : 1f;
					}
				}
				if (low == float.MaxValue || low >= clearPx) return;
				useTextRoot.position += useTextRoot.up * ((clearPx - low) * worldPerPx);
				useTextLifted = true;
			}
			catch (Exception e) { DiegeticPlugin.WarnOnce("usetext", "Moving the look-at text: " + e.Message); }
		}

		internal static void RestoreAll()
		{
			if (useTextRoot != null && useTextLifted) { useTextRoot.localPosition = useTextHome; useTextLifted = false; }
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
