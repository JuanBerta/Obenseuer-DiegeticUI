using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace DiegeticUIMod
{
	/// <summary>Everything the HUD shows, read from the game once a frame. Display only: nothing is changed.</summary>
	internal static class GameData
	{
		internal static bool Ready;
		internal static float Health, Mind, Food, Water, Rest, Toilet, Clean;   // reserves, 0..100, higher is better

		internal struct Warning { public string Text; public int Level; public bool Severe; public Color Ink; }
		internal static readonly List<Warning> Warnings = new List<Warning>();

		internal static bool HasClock, HasCalendar;
		internal static int Hours = -1, Minutes;
		internal static string Weekday = "";

		internal static int OC; internal static float RM;

		internal struct Hand { public bool Any; public string Name; public Sprite Icon; public float Fill; public bool Battery; public string Amount; }
		internal static Hand Left, Right;

		private static float nextSlow;
		private static float crimeValue;

		internal static void Update()
		{
			Ready = false;
			PlayerStats ps = PlayerStats.instance;
			if (ps == null || ps.health == null || ps.hungerAndThirst == null) return;
			try
			{
				Health = Mathf.Clamp(ps.health.currentStatus, 0f, 100f);
				Mind = Mathf.Clamp(100f - ps.mentalHealth.effectiveMentalHealth, 0f, 100f);
				Food = Mathf.Clamp(100f - ps.hungerAndThirst.Hunger, 0f, 100f);
				Water = Mathf.Clamp(100f - ps.hungerAndThirst.Thirst, 0f, 100f);
				Rest = Mathf.Clamp(100f - ps.tiredness.currentStatus, 0f, 100f);
				Toilet = Mathf.Clamp(100f - Mathf.Max(ps.hungerAndThirst.Bowel, ps.hungerAndThirst.Bladder), 0f, 100f);
				Clean = Mathf.Clamp(ps.hygiene.Hygiene, 0f, 100f);
				Ready = true;

				if (Time.unscaledTime >= nextSlow)
				{
					nextSlow = Time.unscaledTime + 1f;
					try { crimeValue = Crime.instance != null ? Crime.instance.CountCrimes() : 0f; } catch (Exception) { crimeValue = 0f; }
				}
				Warnings.Clear();
				if (ps.health.Bleeding >= 1f) Add("Bleeding", ps.health.Bleeding, ps.health.Bleeding > 50f, Art.StampRed);
				if (Crime.instance != null && (crimeValue > 0f || Crime.instance.ArrestInProgress))
					Add(Crime.instance.ArrestInProgress ? "Arrest!" : "Police", crimeValue, Crime.instance.ArrestInProgress, Art.StampRed);
				if (ps.radiation != null && ps.radiation.Dose > 0.5f) Add("Rad", ps.radiation.Dose / 10f, ps.radiation.Dose > ps.radiation.DoseDamageThreshold, new Color32(47, 90, 42, 255));
				if (ps.mushrooms != null && ps.mushrooms.SMVProgression >= 1f) Add("Fungus", ps.mushrooms.SMVProgression, ps.mushrooms.SMVProgression > 50f, Art.Brown);
				if (ps.addictions != null)
				{
					if (ps.addictions.AlcoholNeed >= 33f) Add("Needs a drink", ps.addictions.AlcoholNeed, ps.addictions.AlcoholNeed > 90f, Art.Brown);
					if (ps.addictions.SmokingNeed >= 33f) Add("Craving smoke", ps.addictions.SmokingNeed, ps.addictions.SmokingNeed > 90f, Art.Brown);
					if (ps.addictions.MushroomNeed >= 33f) Add("Mushroom itch", ps.addictions.MushroomNeed, ps.addictions.MushroomNeed > 90f, Art.Brown);
					if (ps.addictions.GamblingNeed >= 33f) Add("Itching to bet", ps.addictions.GamblingNeed, ps.addictions.GamblingNeed > 90f, Art.Brown);
				}
				if (ps.duck != null && ps.duck.currentStatus >= ps.duck.negativeEffects && ps.duck.currentStatus > 0f)
					Add("Duck!", ps.duck.currentStatus, ps.duck.currentStatus > 90f, Art.Brown);
			}
			catch (Exception) { }

			ReadTime();
			ReadMoney();
			ReadHands();
		}

		private static void Add(string text, float level, bool severe, Color ink)
		{
			Warnings.Add(new Warning { Text = text, Level = Mathf.RoundToInt(level), Severe = severe, Ink = ink });
		}

		// The game's own rule: the time shows only with a watch equipped (in menus: a clock or charged phone carried).
		private static void ReadTime()
		{
			HasClock = HasCalendar = false;
			try
			{
				if (TimeOfDayAzure.instance == null) return;
				bool menu = GameUIController.instance != null && GameUIController.instance.InventoryIsVisible();
				if (menu && Inventory.instance != null)
				{
					bool cal = Inventory.instance.CheckForCategoryItem("Clock Calendar", ignoreBackpack: true);
					bool clock = cal || Inventory.instance.CheckForCategoryItem("Clock", ignoreBackpack: true);
					if (!clock)
					{
						List<SlotController> phones = Inventory.instance.GetAllSlotsWithItemWithMeta<ItemConnectivityData>();
						if (phones != null)
							foreach (SlotController sc in phones)
							{
								ItemAmmodata ammo = ItemMetaUtilities.GetMetaOfType<ItemAmmodata>(sc.itemStack.Meta);
								if (ammo != null && ammo.ammo > 0f) { clock = cal = true; break; }
							}
					}
					HasClock = clock; HasCalendar = cal;
				}
				else if (WatchSlotController.instance != null)
				{
					HasClock = WatchSlotController.instance.ClockEquipped;
					HasCalendar = WatchSlotController.instance.CalendarEquipped;
				}
				Hours = Mathf.FloorToInt(TimeOfDayAzure.instance.CurrentHours);
				Minutes = Mathf.FloorToInt(TimeOfDayAzure.instance.CurrentMinutes);
				Weekday = TimeOfDayAzure.instance.GetDayOfWeekString() ?? "";
			}
			catch (Exception) { HasClock = false; }
		}

		// ---- Money and the receipt lines ----
		internal class Entry { public bool Gain; public string Currency; public float Amount; public float Born; }
		internal static readonly List<Entry> Entries = new List<Entry>();
		internal static float FlashOC, FlashRM;   // time of the last change, for the balance flash
		internal static bool LastOCGain, LastRMGain;
		private static bool moneyKnown;
		private static float quietUntil;

		internal static void ResetMoney(float quietSeconds)
		{
			moneyKnown = false;
			quietUntil = Time.unscaledTime + quietSeconds;
		}

		private static void ReadMoney()
		{
			Money m = Money.instance;
			if (m == null) { moneyKnown = false; return; }
			int oc = m.OCMoneyAmount;
			float rm = m.RMoneyAmount;
			if (!moneyKnown || Time.unscaledTime < quietUntil)
			{
				OC = oc; RM = rm; moneyKnown = true;
				return;
			}
			if (oc != OC)
			{
				Push(oc > OC, "OC", Mathf.Abs(oc - OC));
				LastOCGain = oc > OC; FlashOC = Time.unscaledTime; OC = oc;
			}
			if (Mathf.Abs(rm - RM) >= 0.05f)
			{
				Push(rm > RM, "RM", Mathf.Abs(rm - RM));
				LastRMGain = rm > RM; FlashRM = Time.unscaledTime; RM = rm;
			}
		}

		private static void Push(bool gain, string cur, float amount)
		{
			// Several payments in the same moment of the same kind merge into one line.
			if (Entries.Count > 0)
			{
				Entry last = Entries[Entries.Count - 1];
				if (last.Currency == cur && last.Gain == gain && Time.unscaledTime - last.Born < 0.4f) { last.Amount += amount; return; }
			}
			Entries.Add(new Entry { Gain = gain, Currency = cur, Amount = amount, Born = Time.unscaledTime });
			while (Entries.Count > 12) Entries.RemoveAt(0);
		}

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
		internal static string FormatOC(float v) { return Mathf.FloorToInt(v).ToString("#,0", Inv).Replace(",", " "); }
		internal static string FormatRM(float v) { return Mathf.FloorToInt(v + 0.0001f).ToString("#,0", Inv).Replace(",", " "); }
		internal static string FormatDelta(Entry e)
		{
			if (e.Currency == "OC") return Mathf.RoundToInt(e.Amount).ToString("#,0", Inv).Replace(",", " ") + " OC";
			float v = Mathf.Round(e.Amount * 10f) / 10f;
			return (Mathf.Abs(v - Mathf.Round(v)) < 0.01f ? v.ToString("#,0", Inv) : v.ToString("#,0.0", Inv)).Replace(",", " ").Replace(".", ",") + " RM";
		}

		// ---- Equipped items (the same slots the game's corner icons use) ----
		private static void ReadHands()
		{
			try
			{
				if (Inventory.instance == null || Inventory.instance.CharacterSlots == null || Inventory.instance.CharacterSlots.Length < 8) { Left.Any = Right.Any = false; return; }
				Right = ReadHand(Inventory.instance.CharacterSlots[6]);
				Left = ReadHand(Inventory.instance.CharacterSlots[7]);
			}
			catch (Exception) { Left.Any = Right.Any = false; }
		}

		private static Hand ReadHand(SlotController slot)
		{
			var h = new Hand();
			ItemStack st = slot != null ? slot.itemStack : null;
			if (st == null || st.itemReference == null || st.itemReference.ID < 1) return h;
			Item item = st.itemReference.Item;
			if (item == null) return h;
			h.Any = true;
			h.Name = item.Title ?? "";
			try { h.Icon = item.Appearance != null ? item.Appearance.GetSprite(st.Meta) : null; } catch (Exception) { }
			h.Fill = -1f;
			h.Amount = item.Stackable == 1 ? "" : st.itemAmount + " / " + item.Stackable;
			if (st.Meta != null)
				foreach (object o in st.Meta)
				{
					var ammo = o as ItemAmmodata;
					if (ammo != null && ammo.maxAmmo > 0f) { h.Fill = Mathf.Clamp01(ammo.ammo / ammo.maxAmmo); h.Battery = true; h.Amount = Mathf.RoundToInt(h.Fill * 100f) + "%"; }
					var per = o as ItemPerishabledata;
					if (per != null) { h.Fill = Mathf.Clamp01(per.integrity); h.Battery = false; h.Amount = Mathf.RoundToInt(h.Fill * 100f) + "%"; }
				}
			return h;
		}

		// ---- Notifications: read the game's visible list; the game keeps its own queue, timing and sounds ----
		internal class Slip { public Notification Source; public string Head; public string Body; public Color Ink; public float Born; public float Gone = -1f; public float Tilt; }
		internal static readonly List<Slip> Slips = new List<Slip>();
		private static readonly Regex Tags = new Regex("<[^>]+>");
		private static int tiltSeed;

		internal static void UpdateSlips()
		{
			List<Notification> live = Notifications.instance != null ? Notifications.instance.visibleNotifications : null;
			float now = Time.unscaledTime;
			// leaving
			foreach (Slip s in Slips)
				if (s.Gone < 0f && (live == null || !live.Contains(s.Source))) s.Gone = now;
			Slips.RemoveAll(s => s.Gone >= 0f && now - s.Gone > 0.5f);
			if (live == null) return;
			foreach (Notification n in live)
			{
				if (n == null || Slips.Exists(s => s.Source == n)) continue;
				string head = StripTags(n.type), body = StripTags(n.text);
				Slips.Add(new Slip { Source = n, Head = head.ToUpperInvariant().TrimEnd('!', ':', ' '), Body = body, Ink = Category(head), Born = now, Tilt = ((tiltSeed++ % 5) - 2) * 0.6f });
			}
		}

		private static string StripTags(string s) { return string.IsNullOrEmpty(s) ? "" : Tags.Replace(s, "").Trim(); }

		private static Color Category(string head)
		{
			string h = head.ToLowerInvariant();
			if (h.Contains("task") || h.Contains("objective")) return new Color32(122, 43, 28, 255);
			if (h.Contains("crime") || h.Contains("condition") || h.Contains("evict") || h.Contains("expire") || h.Contains("corrupt")) return Art.StampRed;
			if (h.Contains("money") || h.Contains("rent") || h.Contains("utilit") || h.Contains("invoice") || h.Contains("collateral") || h.Contains("paid")) return new Color32(107, 83, 32, 255);
			if (h.Contains("mail") || h.Contains("package") || h.Contains("subscri")) return new Color32(74, 42, 122, 255);
			if (h.Contains("skill")) return Art.Blue;
			if (h.Contains("renovation") || h.Contains("room") || h.Contains("resident")) return new Color32(107, 83, 32, 255);
			if (h.Contains("item") || h.Contains("recipe") || h.Contains("furniture") || h.Contains("note") || h.Contains("discover")) return new Color32(47, 90, 42, 255);
			return new Color32(80, 74, 62, 255);
		}
	}
}
