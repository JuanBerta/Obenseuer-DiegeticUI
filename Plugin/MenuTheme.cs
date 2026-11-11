using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiegeticUIMod
{
	/// <summary>
	/// Gives the game's menu pages the desk look: dark frames become cardboard, dark rows and cards become manila
	/// paper, and the light text on them becomes ink. Only colours change (the game's own panels, buttons and
	/// layout stay), item slots/icons/tooltips are left alone, and every original colour is put back when the
	/// theme is turned off.
	/// </summary>
	internal static class MenuTheme
	{
		private static readonly Color Cardboard = new Color32(92, 70, 44, 255);
		private static readonly Color CardboardDark = new Color32(66, 50, 32, 255);
		private static readonly Color Manila = new Color32(214, 196, 152, 255);
		private static readonly Color ManilaLight = new Color32(232, 220, 188, 255);
		private static readonly Color InkText = new Color32(43, 36, 24, 255);
		private static readonly Color CreamText = new Color32(240, 228, 205, 255);

		private enum Surface { None, Board, Paper }

		private static readonly Dictionary<Graphic, Color> originals = new Dictionary<Graphic, Color>();
		private static readonly Dictionary<Image, Surface> surfaces = new Dictionary<Image, Surface>();
		private static readonly Dictionary<Graphic, Color> setTo = new Dictionary<Graphic, Color>();
		private static float nextScan;
		private static bool applied;

		internal static void Update(bool menuOpen, bool enabled)
		{
			if (!enabled) { if (applied) Restore(); return; }
			if (!menuOpen || Time.unscaledTime < nextScan) return;
			nextScan = Time.unscaledTime + 0.5f;   // new rows (notes, tasks) appear as pages open
			try { Scan(); } catch (Exception e) { DiegeticPlugin.WarnOnce("theme", "Menu theme: " + e.Message); }
		}

		private static float Lum(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }

		private static void Scan()
		{
			UIPageButtons ui = UIPageButtons.instance;
			if (ui == null) return;
			Transform root = ui.transform.parent != null ? ui.transform.parent : ui.transform;
			applied = true;

			foreach (Image img in root.GetComponentsInChildren<Image>(true))
			{
				if (img == null) continue;
				Color mine;
				if (surfaces.ContainsKey(img))
				{
					if (!setTo.TryGetValue(img, out mine) || img.color == mine) continue;
					surfaces.Remove(img);   // the game recoloured it: theme its new colour
				}
				else if (Skip(img.transform)) continue;
				Color c = img.color;
				Surface s = Surface.None;
				if (c.a > 0.35f && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.32f && !Coloured(c))
				{
					float l = Lum(c);
					s = l < 0.075f ? Surface.Board : Surface.Paper;
					originals[img] = c;
					Color to = s == Surface.Board ? (l < 0.04f ? CardboardDark : Cardboard) : (l < 0.14f ? Manila : ManilaLight);
					to.a = c.a;
					img.color = to;
					setTo[img] = to;
				}
				else if (originals.ContainsKey(img) && !setTo.ContainsKey(img)) { }
				surfaces[img] = s;
			}

			foreach (TextMeshProUGUI t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
			{
				if (t == null) continue;
				Color mineT;
				if (originals.ContainsKey(t))
				{
					if (!setTo.TryGetValue(t, out mineT) || t.color == mineT) continue;
					originals.Remove(t);
				}
				else if (Skip(t.transform)) continue;
				Color c = t.color;
				if (Lum(c) < 0.55f || Coloured(c)) { originals[t] = c; continue; }   // remember as handled, unchanged
				Surface under = Behind(t.transform);
				if (under == Surface.None) { originals[t] = c; continue; }
				originals[t] = c;
				Color to = under == Surface.Paper ? InkText : CreamText;
				to.a = c.a;
				t.color = to;
				setTo[t] = to;
			}
		}

		// Text colours that carry meaning (red warnings, green ticks, gold levels) keep their colour.
		private static bool Coloured(Color c)
		{
			float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
			return max - min > 0.18f;
		}

		// The surface right behind a text: the nearest themed image among its parents (or their earlier siblings).
		private static Surface Behind(Transform t)
		{
			for (Transform p = t; p != null; p = p.parent)
			{
				Image own = p.GetComponent<Image>();
				Surface s;
				if (own != null && surfaces.TryGetValue(own, out s) && s != Surface.None) return s;
				if (p.parent != null)
					for (int i = p.GetSiblingIndex() - 1; i >= 0; i--)
					{
						Image sib = p.parent.GetChild(i).GetComponent<Image>();
						if (sib != null && surfaces.TryGetValue(sib, out s) && s != Surface.None && Overlaps(sib.rectTransform, t as RectTransform)) return s;
					}
			}
			return Surface.None;
		}

		private static bool Overlaps(RectTransform a, RectTransform b)
		{
			if (a == null || b == null) return false;
			var ca = new Vector3[4]; var cb = new Vector3[4];
			a.GetWorldCorners(ca); b.GetWorldCorners(cb);
			Vector3 mid = (cb[0] + cb[2]) * 0.5f;
			return mid.x >= ca[0].x && mid.x <= ca[2].x && mid.y >= ca[0].y && mid.y <= ca[2].y;
		}

		// Item slots, item art and tooltips keep the game's look; so does anything another mod builds.
		private static bool Skip(Transform t)
		{
			for (Transform p = t; p != null; p = p.parent)
			{
				if (p.GetComponent<Slot>() != null || p.GetComponent<ItemData>() != null) return true;
				string n = p.name;
				if (n.IndexOf("tooltip", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			return false;
		}

		internal static void Restore()
		{
			foreach (var kv in originals) if (kv.Key != null) kv.Key.color = kv.Value;
			originals.Clear();
			surfaces.Clear();
			setTo.Clear();
			applied = false;
		}
	}
}
