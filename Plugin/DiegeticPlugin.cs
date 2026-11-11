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
		public const string Version = "1.0.2";

		internal static ManualLogSource Log;
		internal static ConfigEntry<bool> Enabled, ShowNeeds, ShowWallet, ShowFeed, ShowHands, RestyleMenus, PaperMenus, ThemeBank, ThemeLootMap;
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
			RestyleMenus = Config.Bind("Parts", "MenuRestyle", true, "Menus: manila folder tabs and a paper label on storage headers (the game's own buttons still take the clicks).");
			PaperMenus = Config.Bind("Parts", "MenuPaperTheme", true, "Menus: dark frames become cardboard, rows and cards become manila paper with ink text (colours only; item slots and icons keep the game's look).");
			ThemeBank = Config.Bind("Other mods", "BankInventoryPaperLook", true, "If Bank Inventory is installed: its search box and crafting/bank buttons use the paper look too.");
			ThemeLootMap = Config.Bind("Other mods", "LootMapPaperLook", true, "If Loot Map is installed: its panel on the map page uses the paper look too.");
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
			bool menuOpen = false;
			try { menuOpen = GameUIController.instance != null && GameUIController.instance.IsMenuVisible; } catch (Exception) { }
			MenuTheme.Update(menuOpen, Enabled.Value && PaperMenus.Value);
			if (menuOpen && Input.GetKeyDown(KeyCode.F10) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) MenuTheme.DumpUnderMouse();
			// Other mods read these flags and switch their own panels to the paper look (no hard dependency either way).
			AppDomain.CurrentDomain.SetData("Obenseuer.DiegeticTheme.BankInventory", Enabled.Value && ThemeBank.Value);
			AppDomain.CurrentDomain.SetData("Obenseuer.DiegeticTheme.LootMap", Enabled.Value && ThemeLootMap.Value);
			if (!Enabled.Value) return;
			try { GameData.Update(); GameData.UpdateSlips(); }
			catch (Exception e) { WarnOnce("update", "Reading the game: " + e); }
		}

		private void LateUpdate()
		{
			try
			{
				bool menuOpen = GameUIController.instance != null && GameUIController.instance.IsMenuVisible;
				if (Enabled.Value && PaperMenus.Value) MenuTheme.LateApply(menuOpen);
			}
			catch (Exception) { }
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
			if (Blocked(menu)) return;
			if (!menu && !NativeHud.GameplayHudVisible) return;
			GUI.depth = 10;
			Hud.Draw(menu, Opacity.Value);
			if (otherHud && menu)
			{
				GUIStyle s = Art.Text(Art.TypeFont, 14, TextAnchor.UpperLeft);
				Art.Label(new Rect(16, Screen.height - 28, 900, 20), "Diegetic UI: another HUD mod is installed too. Keep only one.", s, new Color(1f, 0.6f, 0.4f));
			}
		}

		private void OnDestroy() { NativeHud.RestoreAll(); MenuTheme.Restore(); }

		// Screens the HUD must stay off: loading screens and the pause menu (it keeps the gameplay HUD "visible").
		private static float nextBlockLog;
		private static bool Blocked(bool menu)
		{
			string why = null;
			try
			{
				if (SaveController.Loading) why = "loading";
				else if (LoadingScreenShowing()) why = "loading screen";
				else if (!menu && GameController.instance != null && GameController.instance.GameIsPaused && PauseMenuShowing()) why = "paused";
				else if (!menu && ConversationActive()) why = "conversation";
			}
			catch (Exception) { }
			if (why != null && Time.unscaledTime >= nextBlockLog)
			{
				nextBlockLog = Time.unscaledTime + 30f;
				Log.LogInfo("HUD hidden: " + why);
			}
			return why != null;
		}

		// The loading screen's canvas stays enabled between loads; it is showing only while its screens are opaque.
		private static bool LoadingScreenShowing()
		{
			LoadingScreen ls = LoadingScreen.instance;
			if (ls == null || !ls.Active || !ls.gameObject.activeInHierarchy) return false;
			bool showing = false;
			string detail = "";
			foreach (string f in new[] { "normalScreen", "whiteScreen" })
			{
				var cg = HarmonyLib.Traverse.Create(ls).Field(f).GetValue<CanvasGroup>();
				if (cg == null) continue;
				bool vis = NativeHud.EffectivelyVisible(cg.transform);
				detail += f + " alpha=" + cg.alpha.ToString("0.00") + " visible=" + vis + "; ";
				if (vis) showing = true;
			}
			if (!loggedLoading) { loggedLoading = true; Log.LogInfo("Loading screen check: " + detail + "levelLoadDone=" + ls.LevelLoadDone); }
			return showing && !ls.LevelLoadDone;
		}

		private static bool loggedLoading;
		private static System.Reflection.PropertyInfo convProp;
		private static bool convLooked;

		// A conversation (people, phone calls) is on screen: the dialogue options sit where the needs strip is.
		private static bool ConversationActive()
		{
			if (!convLooked)
			{
				convLooked = true;
				Type dm = Type.GetType("PixelCrushers.DialogueSystem.DialogueManager, Assembly-CSharp-firstpass");
				convProp = dm != null ? dm.GetProperty("isConversationActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) : null;
				if (convProp == null) Log.LogWarning("Couldn't find the dialogue system; the HUD won't hide during conversations.");
			}
			try { return convProp != null && (bool)convProp.GetValue(null, null); } catch (Exception) { return false; }
		}
		private static PauseMenu pause;
		private static bool PauseMenuShowing()
		{
			if (pause == null) pause = UnityEngine.Object.FindObjectOfType<PauseMenu>();
			return pause == null || NativeHud.EffectivelyVisible(pause.transform);
		}
	}
}
