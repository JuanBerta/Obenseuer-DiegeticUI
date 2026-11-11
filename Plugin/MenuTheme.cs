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
		private static readonly Dictionary<Image, Sprite> sprites = new Dictionary<Image, Sprite>();
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
				if (IsRow(img, c))
				{
					originals[img] = c;
					sprites[img] = img.sprite;
					img.sprite = null;   // flat paper instead of the dark button art
					Color to = Behind(img.transform, img.rectTransform) == Surface.Paper ? ManilaLight : Manila;
					to.a = c.a;
					img.color = to;
					setTo[img] = to;
					surfaces[img] = Surface.Paper;
					continue;
				}
				if (c.a > 0.35f && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.32f && !Coloured(c) && !FullScreen(img.rectTransform))
				{
					float l = Lum(c);
					// by layer: an outer frame is cardboard, anything lying on a themed surface is paper
					s = Behind(img.transform, img.rectTransform) == Surface.None ? Surface.Board : Surface.Paper;
					originals[img] = c;
					Color to = s == Surface.Board ? (l < 0.04f ? CardboardDark : Cardboard) : (l < 0.1f ? Manila : ManilaLight);
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
				Surface under = InsideRow(t.transform) ? Surface.Paper : Behind(t.transform, t.rectTransform);
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

		// The surface right behind something: the nearest visible image among its own, the earlier siblings it lies
		// on, and the same going up the parents. If that nearest image wasn't themed, the answer is None.
		private static Surface Behind(Transform start, RectTransform target)
		{
			for (Transform p = start; p != null; p = p.parent)
			{
				Image own = p.GetComponent<Image>();
				if (own != null && own.transform != target && Visible(own)) return Of(own);
				if (p.parent != null)
					for (int i = p.GetSiblingIndex() - 1; i >= 0; i--)
					{
						Image sib = p.parent.GetChild(i).GetComponent<Image>();
						if (sib != null && Visible(sib) && Overlaps(sib.rectTransform, target)) return Of(sib);
					}
			}
			return Surface.None;
		}

		private static bool Visible(Image img) { return img.enabled && img.gameObject.activeInHierarchy && (img.color.a > 0.3f || surfaces.ContainsKey(img)); }

		private static Surface Of(Image img)
		{
			Surface s;
			return surfaces.TryGetValue(img, out s) ? s : Surface.None;
		}

		// Text that sits inside a row/card we turned into paper always gets ink.
		private static bool InsideRow(Transform t)
		{
			int depth = 0;
			for (Transform p = t.parent; p != null && depth < 4; p = p.parent, depth++)
			{
				Image img = p.GetComponent<Image>();
				Surface s;
				if (img != null && (sprites.ContainsKey(img) || (surfaces.TryGetValue(img, out s) && s == Surface.Paper))) return true;
				if (img != null && surfaces.TryGetValue(img, out s) && s == Surface.Board) return false;
			}
			return false;
		}

		// A list row or card button drawn with the game's grey button art.
		private static bool IsRow(Image img, Color c)
		{
			Selectable sel = img.GetComponent<Selectable>();
			if (sel == null || sel.targetGraphic != img || img.sprite == null) return false;
			if (sel is Scrollbar || sel is Slider || sel is InputField || sel is TMP_InputField || sel is Toggle) return false;
			if (Lum(c) < 0.7f || Coloured(c) || c.a < 0.5f) return false;
			Rect r = img.rectTransform.rect;
			if (r.width < 120f || r.height < 24f || r.height > 140f) return false;
			string n = img.sprite.name.ToLowerInvariant();
			return !n.Contains("icon") && !n.Contains("arrow") && !n.Contains("star");
		}

		// Full-screen layers (dimmers, fades) stay as they are.
		private static bool FullScreen(RectTransform rt)
		{
			var c = new Vector3[4];
			rt.GetWorldCorners(c);
			Canvas canvas = rt.GetComponentInParent<Canvas>();
			Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
			return Mathf.Abs(b.x - a.x) * Mathf.Abs(b.y - a.y) > Screen.width * Screen.height * 0.6f;
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
				// crafting panels: the game colours recipe buttons to show what you can make
				if (p.GetComponent<RecipeButton>() != null || p.GetComponent<RecipeListUI>() != null || p.GetComponent<RecipeInfoUI>() != null) return true;
				// the hover tooltip windows themselves (not things that merely carry tooltip info, like skill cards)
				if (p.GetComponent<ToolTip>() != null || p.GetComponent<TooltipUI>() != null) return true;
			}
			return false;
		}

		// Some pages (the Character skill cards) write their text colours every frame. After the game's own update,
		// put our colour back wherever it simply restored the original; a different colour (a highlight) is left.
		internal static void LateApply(bool menuOpen)
		{
			if (!applied || !menuOpen) return;
			foreach (var kv in setTo)
			{
				Graphic g = kv.Key;
				if (g == null || !g.isActiveAndEnabled) continue;
				Color orig;
				if (g.color != kv.Value && originals.TryGetValue(g, out orig) && Near(g.color, orig)) g.color = kv.Value;
			}
		}

		private static bool Near(Color a, Color b)
		{
			return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f && Mathf.Abs(a.a - b.a) < 0.05f;
		}

		internal static void Restore()
		{
			foreach (var kv in sprites) if (kv.Key != null) kv.Key.sprite = kv.Value;
			sprites.Clear();
			foreach (var kv in originals) if (kv.Key != null) kv.Key.color = kv.Value;
			originals.Clear();
			surfaces.Clear();
			setTo.Clear();
			applied = false;
		}
	}
}
