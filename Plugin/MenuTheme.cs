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

		private enum Surface { None, Board, Paper, Other }   // Other: a visible image we left as the game made it

		private static readonly Dictionary<Graphic, Color> originals = new Dictionary<Graphic, Color>();
		private static readonly Dictionary<Image, Surface> surfaces = new Dictionary<Image, Surface>();
		private static readonly Dictionary<Graphic, Color> setTo = new Dictionary<Graphic, Color>();
		private static readonly Dictionary<Image, Sprite> sprites = new Dictionary<Image, Sprite>();
		private static float nextScan, burstUntil;
		private static bool wasOpen;
		private static object lastPage, lastForeign;
		private static readonly HashSet<Image> gameArt = new HashSet<Image>();
		private static System.Reflection.FieldInfo currentPageField;
		private static bool applied;

		internal static void Update(bool menuOpen, bool enabled)
		{
			if (!enabled) { if (applied) Restore(); return; }
			if (!menuOpen) { wasOpen = false; return; }
			// right after the menu, a page or a storage opens, theme every frame so new text never shows white
			object page = null, foreign = null;
			try
			{
				if (currentPageField == null) currentPageField = HarmonyLib.AccessTools.Field(typeof(UIPageButtons), "currentPage");
				if (UIPageButtons.instance != null && currentPageField != null) page = currentPageField.GetValue(UIPageButtons.instance);
				if (Inventory.instance != null) foreign = Inventory.instance.ForeignSlots;
			}
			catch (Exception) { }
			if (!wasOpen || !Equals(page, lastPage) || !ReferenceEquals(foreign, lastForeign)) burstUntil = Time.unscaledTime + 0.6f;
			wasOpen = true; lastPage = page; lastForeign = foreign;
			if (Time.unscaledTime < nextScan && Time.unscaledTime >= burstUntil) return;
			nextScan = Time.unscaledTime + 0.5f;
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
				if (img == null || gameArt.Contains(img)) continue;
				Color mine;
				Sprite was;
				if (sprites.TryGetValue(img, out was) && img.sprite != null)
				{
					// the game set its own button art again (the item panel's Buy/Sell/Use button): that button keeps
					// the game's look from now on
					Color o;
					if (originals.TryGetValue(img, out o)) img.color = o;
					sprites.Remove(img); surfaces.Remove(img); setTo.Remove(img); originals.Remove(img);
					gameArt.Add(img);
					continue;
				}
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
					Color to = Behind(img.transform, img.rectTransform) == Surface.Paper ? ManilaLight : Manila;   // a row on paper is a shade lighter
					to.a = c.a;
					img.color = to;
					setTo[img] = to;
					surfaces[img] = Surface.Paper;
					continue;
				}
				if (c.a > 0.3f && c.a < 0.85f && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.32f && !Coloured(c) && !FullScreen(img.rectTransform))
				{
					// a see-through dark layer (the dimming over unselected cards): keep it as a light shadow, not paper,
					// so whatever is under it stays readable
					originals[img] = c;
					Color shade = new Color(0.12f, 0.08f, 0.04f, c.a * 0.3f);
					img.color = shade;
					setTo[img] = shade;
					surfaces[img] = Surface.None;
					continue;
				}
				if (c.a >= 0.85f && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.32f && !Coloured(c) && !FullScreen(img.rectTransform))
				{
					float l = Lum(c);
					// by layer: an outer frame is cardboard, anything lying on a themed surface is paper
					Surface below = Behind(img.transform, img.rectTransform);
					s = below == Surface.Board || below == Surface.Paper ? Surface.Paper : Surface.Board;
					originals[img] = c;
					Color to = s == Surface.Board ? (l < 0.04f ? CardboardDark : Cardboard) : (l < 0.1f ? Manila : ManilaLight);
					to.a = c.a;
					img.color = to;
					setTo[img] = to;
				}
				else if (originals.ContainsKey(img) && !setTo.ContainsKey(img)) { }
				surfaces[img] = s;
			}

			SkillCards(root);

			foreach (TextMeshProUGUI t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
			{
				if (t == null || skillTexts.Contains(t) || !t.isActiveAndEnabled) continue;
				Color c, mineT;
				if (originals.TryGetValue(t, out c))
				{
					// the game set a new colour of its own (a highlight, a state change): that is the new original
					bool ours = setTo.TryGetValue(t, out mineT);
					if (!Near(t.color, c) && !(ours && Near(t.color, mineT))) { c = t.color; originals[t] = c; setTo.Remove(t); }
				}
				else
				{
					if (Skip(t.transform)) { originals[t] = t.color; skippedTexts.Add(t); continue; }
					c = t.color;
					originals[t] = c;
				}
				if (Lum(c) < 0.55f || Coloured(c) || skippedTexts.Contains(t)) continue;   // dark, meaningful or skipped text stays
				// re-judged every scan, since the game flips some panels between light and dark (hover, selection)
				// the nearest surface wins: an untouched game button (Eat, Slaughter...) keeps its own text colour
				Surface under = Behind(t.transform, t.rectTransform);
				if (under == Surface.None && InsideRow(t.transform)) under = Surface.Paper;
				if (under == Surface.Other) under = Surface.None;
				// cardboard under a light coloured plate the game draws on top (the gallery's orange section
				// headers): it reads as a light surface, so ink
				if (under == Surface.Board && OnLightPlate(t)) under = Surface.Paper;
				Color to = under == Surface.None ? c : (under == Surface.Paper ? InkText : CreamText);
				to.a = c.a;
				if (!Near(t.color, to)) t.color = to;
				if (under == Surface.None) setTo.Remove(t); else setTo[t] = to;
			}
		}

		private static System.Reflection.FieldInfo skTitle, skLevel, skXp, skMax;
		private static readonly HashSet<TextMeshProUGUI> skillTexts = new HashSet<TextMeshProUGUI>();
		private static readonly HashSet<TextMeshProUGUI> skippedTexts = new HashSet<TextMeshProUGUI>();

		// The Character page's skill cards (CharacterPanelSkill): name, level and XP in ink on the paper card;
		// the maxed skill keeps cream text on the dark gold plate the game shows for it.
		private static void SkillCards(Transform root)
		{
			if (skTitle == null)
			{
				skTitle = HarmonyLib.AccessTools.Field(typeof(CharacterPanelSkill), "title");
				skLevel = HarmonyLib.AccessTools.Field(typeof(CharacterPanelSkill), "level");
				skXp = HarmonyLib.AccessTools.Field(typeof(CharacterPanelSkill), "xp");
				skMax = HarmonyLib.AccessTools.Field(typeof(CharacterPanelSkill), "maxLevel");
			}
			foreach (CharacterPanelSkill card in root.GetComponentsInChildren<CharacterPanelSkill>(true))
			{
				var max = skMax != null ? skMax.GetValue(card) as GameObject : null;
				bool maxed = max != null && max.activeSelf;
				foreach (var f in new[] { skTitle, skLevel, skXp })
				{
					var t = f != null ? f.GetValue(card) as TextMeshProUGUI : null;
					if (t == null) continue;
					skillTexts.Add(t);
					if (!originals.ContainsKey(t)) originals[t] = t.color;
					Color to = maxed ? originals[t] : InkText;
					to.a = originals[t].a;
					t.color = to;
					setTo[t] = to;
				}
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
				if (own != null && own.transform != target && Visible(own) && !SeeThrough(own)) return Of(own);
				if (p.parent != null)
					for (int i = p.GetSiblingIndex() - 1; i >= 0; i--)
					{
						Image sib = p.parent.GetChild(i).GetComponent<Image>();
						if (sib != null && Visible(sib) && !SeeThrough(sib) && Overlaps(sib.rectTransform, target)) return Of(sib);
					}
			}
			return Surface.None;
		}

		// Our shadow layers are see-through for deciding what lies under something.
		private static bool SeeThrough(Image img)
		{
			// only our own light shadow layers (the dimming over cards): look past them to the card
			Surface su;
			return setTo.ContainsKey(img) && surfaces.TryGetValue(img, out su) && su == Surface.None && img.color.a < 0.4f;
		}

		private static bool OnLightPlate(TextMeshProUGUI t)
		{
			Transform parent = t.transform.parent;
			if (parent == null) return false;
			for (int i = 0; i < parent.childCount; i++)
			{
				Image img = parent.GetChild(i).GetComponent<Image>();
				if (img == null || img.sprite == null || surfaces.ContainsKey(img) && surfaces[img] != Surface.None) continue;
				if (!Visible(img) || !Coloured(img.color) || Lum(img.color) < 0.4f) continue;
				if (Overlaps(img.rectTransform, t.rectTransform)) return true;
			}
			return false;
		}

		private static bool Visible(Image img) { return img.enabled && img.gameObject.activeInHierarchy && (img.color.a > 0.3f || surfaces.ContainsKey(img)); }

		private static Surface Of(Image img)
		{
			Surface s;
			if (surfaces.TryGetValue(img, out s) && s != Surface.None && setTo.ContainsKey(img) && img.color == setTo[img]) return s;
			// a flat panel we left alone (the game recolours some per state, like the gallery's section headers):
			// judge it by the colour it shows now
			if (img.sprite == null && !Coloured(img.color))
			{
				float l = Lum(img.color);
				if (l > 0.6f) return Surface.Paper;
				if (l < 0.32f) return Surface.Board;
			}
			return Surface.Other;
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
				// the Tenement page is already a paper blueprint by design
				if (p.GetComponent<MonoBehaviour>() != null && TenementPage(p)) return true;
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

		// Diagnostics: log every themed/unthemed graphic under the mouse with its colours and our decision.
		internal static void DumpUnderMouse()
		{
			try
			{
				var es = UnityEngine.EventSystems.EventSystem.current;
				if (es == null) { DiegeticPlugin.Log.LogInfo("Theme dump: no EventSystem"); return; }
				Vector2 mouse = Input.mousePosition;
				var sb = new System.Text.StringBuilder("Theme dump at " + mouse + "\n");
				var seen = new HashSet<Graphic>();
				foreach (Graphic g in UnityEngine.Object.FindObjectsOfType<Graphic>())
				{
					if (g == null || !g.isActiveAndEnabled || seen.Contains(g)) continue;
					Canvas c = g.canvas;
					Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
					if (!RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, mouse, cam)) continue;
					seen.Add(g);
					Color orig, mine; Surface su;
					string kind = g is TextMeshProUGUI ? "TMP '" + ((TextMeshProUGUI)g).text.Replace("\n", " ").Substring(0, Math.Min(24, ((TextMeshProUGUI)g).text.Length)) + "'" : g.GetType().Name;
					var img = g as Image;
					sb.Append("  ").Append(Path(g.transform)).Append(" | ").Append(kind)
					  .Append(" | color=").Append(g.color)
					  .Append(" | orig=").Append(originals.TryGetValue(g, out orig) ? orig.ToString() : "-")
					  .Append(" | set=").Append(setTo.TryGetValue(g, out mine) ? mine.ToString() : "-");
					if (img != null) sb.Append(" | surface=").Append(surfaces.TryGetValue(img, out su) ? su.ToString() : "?").Append(" | sprite=").Append(img.sprite != null ? img.sprite.name : "none");
					var tmp = g as TextMeshProUGUI;
					if (tmp != null) sb.Append(" | vertexGradient=").Append(tmp.enableVertexGradient).Append(" | faceColor=").Append(tmp.fontSharedMaterial != null && tmp.fontSharedMaterial.HasProperty("_FaceColor") ? tmp.fontSharedMaterial.GetColor("_FaceColor").ToString() : "?").Append(" | skip=").Append(Skip(g.transform)).Append(" | insideRow=").Append(InsideRow(g.transform));
					sb.Append("\n");
				}
				DiegeticPlugin.Log.LogInfo(sb.ToString());
			}
			catch (Exception e) { DiegeticPlugin.Log.LogWarning("Theme dump: " + e); }
		}

		private static string Path(Transform t)
		{
			var parts = new List<string>();
			for (int i = 0; t != null && i < 7; t = t.parent, i++) parts.Insert(0, t.name);
			return string.Join("/", parts.ToArray());
		}

		private static bool TenementPage(Transform p)
		{
			foreach (MonoBehaviour mb in p.GetComponents<MonoBehaviour>())
				if (mb != null && mb.GetType().Name.StartsWith("Tenement", StringComparison.Ordinal)) return true;
			return false;
		}

		internal static void Restore()
		{
			foreach (var kv in sprites) if (kv.Key != null) kv.Key.sprite = kv.Value;
			sprites.Clear();
			foreach (var kv in originals) if (kv.Key != null) kv.Key.color = kv.Value;
			originals.Clear();
			surfaces.Clear();
			setTo.Clear();
			skillTexts.Clear();
			skippedTexts.Clear();
			gameArt.Clear();
			applied = false;
		}
	}
}
