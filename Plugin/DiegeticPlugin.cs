using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiegeticUIMod
{
	/// <summary>
	/// Diegetic UI: the HUD becomes the things you carry. Needs on masking tape, health and mind on needle dials,
	/// effects as rubber stamps, the time on a cheap LCD watch, money on a receipt that prints each payment,
	/// events as typed slips, equipped items on luggage tags, and a status clipboard while menus are open.
	/// Display only: it reads the game's values and hides the game's own HUD pieces it replaces.
	/// </summary>
	[BepInPlugin(Guid, "Obenseuer Diegetic UI", Version)]
	public class DiegeticPlugin : BaseUnityPlugin
	{
		public const string Guid = "com.juanberta.obenseuer.diegeticui";
		public const string Version = "0.1.0";

		internal static ManualLogSource Log;
		internal static ConfigEntry<bool> Enabled, ShowNeeds, ShowWallet, ShowFeed, ShowHands;
		internal static ConfigEntry<float> Scale, Opacity, EdgeMargin, BottomMargin;

		private static readonly HashSet<string> warned = new HashSet<string>();
		private bool wasEnabled;
		private bool otherHud;

		private void Awake()
		{
			Log = Logger;
			Enabled = Config.Bind("General", "Enabled", true, "Show the diegetic HUD and hide the game's own HUD pieces it replaces. Off: the game's HUD comes back.");
			ShowNeeds = Config.Bind("Parts", "NeedsAndWarnings", true, "Needs strip, health/mind dials and warning stamps (and the status clipboard in menus).");
			ShowWallet = Config.Bind("Parts", "WatchAndWallet", true, "The wristwatch and the receipt wallet.");
			ShowFeed = Config.Bind("Parts", "Notifications", true, "Event notifications as typed slips.");
			ShowHands = Config.Bind("Parts", "EquippedTags", true, "Equipped items on luggage tags in the bottom corners.");
			Scale = Config.Bind("Layout", "Scale", 0.85f, new ConfigDescription("Overall size of the HUD.", new AcceptableValueRange<float>(0.5f, 1.6f)));
			Opacity = Config.Bind("Layout", "Opacity", 1f, new ConfigDescription("Opacity during gameplay (menus are always fully opaque).", new AcceptableValueRange<float>(0.1f, 1f)));
			EdgeMargin = Config.Bind("Layout", "EdgeMargin", 24f, new ConfigDescription("Distance from the screen edges.", new AcceptableValueRange<float>(0f, 120f)));
			BottomMargin = Config.Bind("Layout", "BottomMargin", 34f, new ConfigDescription("Distance of the needs strip from the bottom of the screen.", new AcceptableValueRange<float>(0f, 200f)));

			SceneManager.sceneLoaded += (s, m) => GameData.ResetMoney(4f);
			GameData.ResetMoney(4f);
			Logger.LogInfo("Obenseuer Diegetic UI " + Version + " loaded.");
		}

		private void Start()
		{
			foreach (var kv in BepInEx.Unity.Mono.Bootstrap.UnityChainloader.Instance.Plugins)
			{
				string id = kv.Key ?? "";
				if (id.IndexOf("uioverhaul", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("compacthud", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					otherHud = true;
					Logger.LogWarning("Another HUD replacement is installed (" + id + "). Use only one: remove it or set Diegetic UI's Enabled = false.");
				}
			}
		}

		internal static void WarnOnce(string key, string msg)
		{
			if (warned.Add(key)) Log.LogWarning(msg);
		}

		private void Update()
		{
			if (!Enabled.Value) return;
			try { GameData.Update(); GameData.UpdateSlips(); }
			catch (Exception e) { WarnOnce("update", "Reading the game: " + e); }
		}

		private void LateUpdate()
		{
			if (!Enabled.Value)
			{
				if (wasEnabled) { NativeHud.RestoreAll(); wasEnabled = false; }
				return;
			}
			wasEnabled = true;
			NativeHud.Hide(ShowNeeds.Value, ShowWallet.Value, ShowWallet.Value, ShowHands.Value, ShowFeed.Value);
		}

		private void OnGUI()
		{
			if (!Enabled.Value || !GameData.Ready || Event.current.type != EventType.Repaint) return;
			bool menu = false;
			try { menu = GameUIController.instance != null && GameUIController.instance.IsMenuVisible; } catch (Exception) { }
			if (!menu && !NativeHud.GameplayHudVisible) return;
			GUI.depth = 10;
			Hud.Draw(menu, Opacity.Value);
			if (otherHud && menu)
			{
				GUIStyle s = Art.Text(Art.TypeFont, 14, TextAnchor.UpperLeft);
				Art.Label(new Rect(16, Screen.height - 28, 900, 20), "Diegetic UI: another HUD mod is installed too. Keep only one.", s, new Color(1f, 0.6f, 0.4f));
			}
		}

		private void OnDestroy() { NativeHud.RestoreAll(); }
	}
}
