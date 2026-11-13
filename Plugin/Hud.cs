using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiegeticUIMod
{
	/// <summary>Draws the HUD. Everything is laid out on a 1080-pixel-tall virtual screen and scaled.</summary>
	internal static class Hud
	{
		private const float VH = 1080f;
		private static float vw;   // virtual width at the current aspect ratio
		private static float vh;   // virtual height: 1080 at Scale 1, taller when the HUD is scaled down

		internal static void Draw(bool menu, float opacity)
		{
			Art.Init();
			Art.RefreshFonts();
			float s = Screen.height / VH * DiegeticPlugin.Scale.Value;
			vw = Screen.width / s;
			vh = Screen.height / s;   // anchor to the real screen edges at any Scale
			Matrix4x4 saved = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
			Art.Alpha = menu ? 1f : opacity;
			try
			{
				float m = DiegeticPlugin.EdgeMargin.Value;
				if (DiegeticPlugin.ShowFeed.Value) Slips(new Vector2(m + 12f, m + 10f), menu ? vh - m - 130f : -1f);
				if (menu)
				{
					if (DiegeticPlugin.RestyleMenus.Value) { FolderTabs(s); StorageLabel(s); }
					float gutter = vw / 2f + 547f / DiegeticPlugin.Scale.Value;   // right edge of the game's inventory panel (it doesn't follow our Scale)
					float gx = gutter + Mathf.Max(16f, (vw - gutter - 300f) / 2f);
					// the game's item tooltips go on top: a piece they would overlap steps aside
					List<Rect> tips = Tooltips(s);
					Popups(s, tips);
					if (DiegeticPlugin.ShowWallet.Value && !Covered(tips, new Rect(gx - 10f, 12f, 320f, 230f))) CompactWatchAndReceipt(new Vector2(gx, 22f));
					if (DiegeticPlugin.ShowNeeds.Value && !Covered(tips, new Rect(gx - 10f, 240f, 320f, 660f))) Clipboard(new Rect(gx, 250f, 300f, 640f));
				}
				else
				{
					var popups = new List<Rect>();
					Popups(s, popups);
					Vector2 wr = new Vector2(vw - m - 12f - WalletW, m + 10f);
					if (DiegeticPlugin.ShowWallet.Value && !Covered(popups, new Rect(wr.x - 10f, 0f, WalletW + 20f, m + 360f))) WatchAndReceipt(wr);
					if (DiegeticPlugin.ShowNeeds.Value)
					{
						float bottom = vh - DiegeticPlugin.BottomMargin.Value;
						Stamps(new Vector2(vw / 2f, bottom - BoardH - 26f));
						Strip(new Vector2(vw / 2f, bottom));
					}
				}
				if (DiegeticPlugin.ShowHands.Value) Tags(m);
			}
			catch (Exception e) { DiegeticPlugin.WarnOnce("draw", "Drawing the HUD: " + e); }
			finally { GUI.matrix = saved; Art.Alpha = 1f; }
		}

		// ---- Tooltips (virtual-screen rects of the game's open hover tooltips) ----

		private static readonly List<TooltipUI> tooltipUIs = new List<TooltipUI>();
		private static readonly List<Rect> tipRects = new List<Rect>();
		private static float nextTipScan;
		private static System.Reflection.FieldInfo tipField;

		private static List<Rect> Tooltips(float s)
		{
			tipRects.Clear();
			try
			{
				if (Time.unscaledTime >= nextTipScan)
				{
					nextTipScan = Time.unscaledTime + 2f;
					tooltipUIs.Clear();
					tooltipUIs.AddRange(UnityEngine.Object.FindObjectsOfType<TooltipUI>());
					if (tipField == null) tipField = HarmonyLib.AccessTools.Field(typeof(TooltipUI), "tooltip");
				}
				var corners = new Vector3[4];
				foreach (TooltipUI ui in tooltipUIs)
				{
					if (ui == null || tipField == null) continue;
					var go = tipField.GetValue(ui) as GameObject;
					if (go == null || !go.activeInHierarchy) continue;
					var rt = go.transform as RectTransform;
					if (rt == null) continue;
					rt.GetWorldCorners(corners);
					Canvas canvas = rt.GetComponentInParent<Canvas>();
					Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
					Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
					tipRects.Add(new Rect(Mathf.Min(a.x, b.x) / s, (Screen.height - Mathf.Max(a.y, b.y)) / s, Mathf.Abs(b.x - a.x) / s, Mathf.Abs(b.y - a.y) / s));
				}
			}
			catch (Exception) { }
			return tipRects;
		}

		// The game's skill level-up popup (right side): pieces it would overlap step aside while it shows.
		private static readonly List<UnityEngine.UI.Graphic> popupGraphics = new List<UnityEngine.UI.Graphic>();
		private static UnityEngine.Object popupOwner;
		private static float nextPopupScan;

		private static void Popups(float s, List<Rect> into)
		{
			try
			{
				PlayerSkillIncreaseUI ui = PlayerSkillIncreaseUI.instance;
				if (ui == null || !ui.isActiveAndEnabled) return;
				if (!ReferenceEquals(ui, popupOwner) || Time.unscaledTime >= nextPopupScan)
				{
					popupOwner = ui;
					nextPopupScan = Time.unscaledTime + 0.5f;
					popupGraphics.Clear();
					ui.GetComponentsInChildren(false, popupGraphics);
				}
				var corners = new Vector3[4];
				bool any = false;
				float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
				foreach (UnityEngine.UI.Graphic g in popupGraphics)
				{
					if (g == null || !g.enabled || !g.gameObject.activeInHierarchy) continue;
					if (g.color.a * g.canvasRenderer.GetInheritedAlpha() < 0.05f) continue;
					RectTransform rt = g.rectTransform;
					rt.GetWorldCorners(corners);
					Canvas canvas = g.canvas;
					Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
					Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
					float w = Mathf.Abs(b.x - a.x), h = Mathf.Abs(b.y - a.y);
					if (w < 2f || h < 2f || w > Screen.width * 0.8f) continue;   // skip empty and full-screen pieces
					any = true;
					x0 = Mathf.Min(x0, Mathf.Min(a.x, b.x)); x1 = Mathf.Max(x1, Mathf.Max(a.x, b.x));
					y0 = Mathf.Min(y0, Screen.height - Mathf.Max(a.y, b.y)); y1 = Mathf.Max(y1, Screen.height - Mathf.Min(a.y, b.y));
				}
				if (any) into.Add(new Rect(x0 / s, y0 / s, (x1 - x0) / s, (y1 - y0) / s));
			}
			catch (Exception) { }
		}

		private static bool Covered(List<Rect> tips, Rect r)
		{
			foreach (Rect t in tips) if (t.Overlaps(r)) return true;
			return false;
		}

		// ---- Survival strip: two dials and five ink gauges on masking tape ----

		private static float Pulse { get { return 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f); } }

		private const float BoardW = 900f, BoardH = 132f, DialCell = 150f;

		// The needs as a drawing's title block: a ruled strip, a cell per reading, label over value.
		private static void Strip(Vector2 bottomCenter)
		{
			Rect board = new Rect(bottomCenter.x - BoardW / 2f, bottomCenter.y - BoardH, BoardW, BoardH);
			Art.Form(board);
			Rect inner = Art.Inner(board);
			Dial(new Rect(inner.x, inner.y, DialCell, inner.height), "HEALTH", GameData.Health, Art.Red);
			Art.VRule(inner.x + DialCell, inner.y, inner.height);
			Dial(new Rect(inner.xMax - DialCell, inner.y, DialCell, inner.height), "MIND", GameData.Mind, Art.Blue);
			Art.VRule(inner.xMax - DialCell, inner.y, inner.height);
			string[] names = { "FOOD", "WATER", "REST", "TOILET", "CLEAN" };
			float[] vals = { GameData.Food, GameData.Water, GameData.Rest, GameData.Toilet, GameData.Clean };
			float left = inner.x + DialCell, cw = (inner.width - DialCell * 2f) / names.Length;
			for (int i = 0; i < names.Length; i++)
			{
				Rect cell = new Rect(left + cw * i, inner.y, cw, inner.height);
				if (i > 0) Art.VRule(cell.x, cell.y, cell.height);
				Gauge(cell, names[i], vals[i]);
			}
			Art.Wear(board);
		}

		// A need's cell: the name, the value, and a ruled bar along the bottom.
		private static void Gauge(Rect cell, string label, float v)
		{
			bool critical = v < 10f;
			if (critical) Art.Rect(new Rect(cell.x + 1f, cell.y, cell.width - 2f, cell.height), new Color(Art.Red.r, Art.Red.g, Art.Red.b, 0.10f + 0.18f * Pulse));
			GUIStyle lab = Art.Text(Art.TypeFont, 14, TextAnchor.UpperCenter);
			Art.Label(new Rect(cell.x, cell.y + 12f, cell.width, 18f), Spaced(label), lab, Art.Ink);
			GUIStyle num = Art.Text(Art.TypeFont, 34, TextAnchor.UpperCenter, true);
			Art.Label(new Rect(cell.x, cell.y + 34f, cell.width, 40f), Mathf.RoundToInt(v).ToString(), num, critical ? Art.Red : Art.Ink);
			Rect bar = new Rect(cell.x + 16f, cell.yMax - 26f, cell.width - 32f, 12f);
			Art.Outline(bar, 2f, Art.Rule);
			Art.Rect(new Rect(bar.x + 3f, bar.y + 3f, (bar.width - 6f) * Mathf.Clamp01(v / 100f), bar.height - 6f), Art.Reserve(v));
		}

		// Health or mind: an octagon badge holding the value, with a ring of ink showing how full it is.
		private static void Dial(Rect cell, string label, float v, Color accent)
		{
			GUIStyle lab = Art.Text(Art.TypeFont, 14, TextAnchor.UpperCenter);
			Art.Label(new Rect(cell.x, cell.y + 12f, cell.width, 18f), Spaced(label), lab, Art.Ink);
			Vector2 c = new Vector2(cell.center.x, cell.y + 72f);
			float t = Mathf.Clamp01(v / 100f);
			Art.Arc(c, 38f, 5f, 0f, 1f, new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.18f));
			if (t > 0.005f) Art.Arc(c, 38f, 5f, 0f, t, v < 10f ? Color.Lerp(accent, Art.Red, Pulse) : accent);
			Art.Badge(c + new Vector2(0f, 6f), 26f, Art.InkSoft);
			GUIStyle num = Art.Text(Art.TypeFont, 24, TextAnchor.MiddleCenter, true);
			Art.Label(new Rect(c.x - 30f, c.y - 10f, 60f, 32f), Mathf.RoundToInt(v).ToString(), num, v < 10f ? (Color)new Color32(240, 150, 140, 255) : Art.Paper);
		}

		// ---- Warnings: small ruled tickets above the strip ----

		private static void Stamps(Vector2 center)
		{
			List<GameData.Warning> list = GameData.Warnings;
			if (list.Count == 0) return;
			GUIStyle st = Art.Text(Art.TypeFont, 15, TextAnchor.MiddleLeft, true);
			const float gap = 10f, h = 40f;
			var widths = new List<float>();
			float total = 0f;
			foreach (var w in list)
			{
				float wd = Art.Measure(Spaced(StampText(w)), st).x + 54f;
				widths.Add(wd);
				total += wd + gap;
			}
			float x = center.x - (total - gap) / 2f;
			for (int i = 0; i < list.Count; i++)
			{
				var w = list[i];
				Rect r = new Rect(x, center.y - h / 2f, widths[i], h);
				float pulse = w.Severe ? 0.55f + 0.45f * Pulse : 1f;
				Art.Form(r);
				Rect inner = Art.Inner(r);
				Art.Rect(new Rect(inner.x, inner.y, 22f, inner.height), new Color(w.Ink.r, w.Ink.g, w.Ink.b, pulse));
				Art.VRule(inner.x + 22f, inner.y, inner.height);
				Art.Label(new Rect(inner.x + 32f, inner.y, inner.width - 34f, inner.height), Spaced(StampText(w)), st, w.Severe ? Art.Red : Art.Ink);
				Art.Wear(r);
				x += widths[i] + gap;
			}
		}

		private static string StampText(GameData.Warning w)
		{
			string t = w.Text.ToUpperInvariant();
			if (w.Text == "Police" || w.Text == "Rad" || w.Text == "Fungus") t += " " + w.Level;
			return t;
		}

		// ---- Wristwatch and receipt wallet ----

		private const float WalletW = 276f;

		// Top right: the watch, and the wallet receipt under it, one aligned column.
		private static void WatchAndReceipt(Vector2 topLeft)
		{
			Rect face = new Rect(topLeft.x, topLeft.y, WalletW, 132f);
			Watch(face, 70f);
			Receipt(new Rect(topLeft.x, face.yMax + 20f, WalletW, 0f), 0.4f, true);
		}

		private static void CompactWatchAndReceipt(Vector2 topLeft)
		{
			Watch(new Rect(topLeft.x, topLeft.y, 132f, 92f), 40f);
			Receipt(new Rect(topLeft.x + 146f, topLeft.y, 150f, 0f), 2f, false);
		}

		private static void Watch(Rect face, float digitH)
		{
			Art.ShadowFor(face, 14f, 8f, 0.6f);
			Art.Rect(face, Art.SteelLight);
			Art.Rect(new Rect(face.x + 4f, face.y + 4f, face.width - 8f, face.height - 8f), Art.Steel);
			Art.Rect(new Rect(face.x + 4f, face.y + 4f, face.width - 8f, 1f), new Color(1f, 1f, 1f, 0.08f));
			Rect screen = new Rect(face.x + face.width * 0.1f, face.y + face.height * 0.14f, face.width * 0.8f, face.height * 0.72f);
			bool on = GameData.HasClock && GameData.Hours >= 0;
			Art.Rect(screen, on ? Art.Lcd : Art.LcdOff);
			Art.Rect(new Rect(screen.x, screen.y, screen.width, 3f), new Color(0, 0, 0, 0.25f));
			// a scratch across the cheap plastic window
			Art.Line(new Vector2(screen.x + screen.width * 0.62f, screen.y + 2f), new Vector2(screen.x + screen.width * 0.48f, screen.yMax - 2f), 1f, new Color(1f, 1f, 1f, 0.16f));
			Color ghost = new Color(Art.LcdInk.r, Art.LcdInk.g, Art.LcdInk.b, on ? 0.08f : 0.12f);
			float small = Mathf.Max(11f, screen.height * 0.17f);
			if (on && GameData.HasCalendar && !string.IsNullOrEmpty(GameData.Weekday))
			{
				GUIStyle day = Art.Text(Art.ReceiptFont, Mathf.RoundToInt(small), TextAnchor.UpperCenter, true);
				Art.Label(new Rect(screen.x, screen.y + 4f, screen.width, small + 4f), GameData.Weekday.ToUpperInvariant(), day, Art.LcdInk);
			}
			// fit the four digits and the colon inside the screen
			const float perH = 0.52f * 4f + 0.16f * 3f + 0.11f + 0.16f * 0.9f;
			digitH = Mathf.Min(digitH, screen.width * 0.86f / perH, screen.height * (on && GameData.HasCalendar ? 0.6f : 0.78f));
			float width = digitH * perH;
			Vector2 at = new Vector2(screen.center.x - width / 2f, screen.yMax - digitH - screen.height * 0.1f);
			Art.LcdTime(at, digitH, on ? GameData.Hours : -1, GameData.Minutes, on ? Art.LcdInk : new Color(Art.LcdInk.r, Art.LcdInk.g, Art.LcdInk.b, 0.35f), ghost);
		}

		// The wallet as a small ledger form: OC and RM rows, and the latest takings and payments under them.
		private static void Receipt(Rect top, float tilt, bool lines)
		{
			float now = Time.unscaledTime;
			var show = new List<GameData.Entry>();
			if (lines)
				for (int i = GameData.Entries.Count - 1; i >= 0 && show.Count < 4; i--)
					if (now - GameData.Entries[i].Born < 9f) show.Add(GameData.Entries[i]);
			bool big = top.width > 200f;
			float rowH = big ? 36f : 28f, headH = big ? 28f : 0f;
			float h = 16f + headH + rowH * 2f + show.Count * 24f;
			Rect r = new Rect(top.x, top.y, top.width, h);
			Art.Form(r);
			Rect inner = Art.Inner(r);
			float y = inner.y;
			GUIStyle head = Art.Text(Art.TypeFont, 13, TextAnchor.MiddleCenter);
			if (big)
			{
				Art.Label(new Rect(inner.x, y, inner.width, headH), Spaced("WALLET"), head, Art.Ink);
				y += headH; Art.HRule(inner.x, y, inner.width);
			}
			float split = inner.x + (big ? 64f : 46f);
			GUIStyle cur = Art.Text(Art.TypeFont, big ? 16 : 14, TextAnchor.MiddleCenter);
			GUIStyle val = Art.Text(Art.TypeFont, big ? 24 : 18, TextAnchor.MiddleRight, true);
			Color ink = Art.Ink;
			Color ocInk = Flash(GameData.FlashOC, GameData.LastOCGain, ink), rmInk = Flash(GameData.FlashRM, GameData.LastRMGain, ink);
			Art.Label(new Rect(inner.x, y, split - inner.x, rowH), "OC", cur, ink);
			Art.Label(new Rect(split, y, inner.xMax - split - 12f, rowH), GameData.FormatOC(GameData.OC), val, ocInk);
			y += rowH; Art.HRule(inner.x, y, inner.width);
			Art.Label(new Rect(inner.x, y, split - inner.x, rowH), "RM", cur, ink);
			Art.Label(new Rect(split, y, inner.xMax - split - 12f, rowH), GameData.FormatRM(GameData.RM), val, rmInk);
			Art.VRule(split, inner.y + headH, rowH * 2f);
			y += rowH;
			if (show.Count > 0)
			{
				Art.HRule(inner.x, y, inner.width);
				GUIStyle l = Art.Text(Art.TypeFont, 14, TextAnchor.MiddleLeft), rr = Art.Text(Art.TypeFont, 14, TextAnchor.MiddleRight);
				foreach (var e in show)
				{
					float age = now - e.Born;
					float a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((9f - age) / 1.2f);
					Color c = e.Gain ? (Color)Art.Olive : (Color)Art.Red;
					c.a = a;
					Art.Label(new Rect(inner.x + 10f, y, inner.width - 20f, 24f), e.Gain ? "+ received" : "- paid", l, c);
					Art.Label(new Rect(inner.x, y, inner.width - 12f, 24f), GameData.FormatDelta(e), rr, c);
					y += 24f;
				}
			}
			Art.Wear(r);
		}

		private static Color Flash(float when, bool gain, Color normal)
		{
			float age = Time.unscaledTime - when;
			if (when <= 0f || age > 1.6f) return normal;
			Color f = gain ? (Color32)Art.Olive : (Color32)Art.Red;
			return Color.Lerp(f, normal, Mathf.Clamp01((age - 0.8f) / 0.8f));
		}

		// ---- Notifications: typed slips, top-left ----

		// bottomY >= 0: stack the slips upwards so the newest sits just above that line (used in menus).
		private static void Slips(Vector2 at, float bottomY)
		{
			float y = at.y, now = Time.unscaledTime;
			if (bottomY >= 0f)
			{
				float total = 0f;
				GUIStyle bm = Art.Text(Art.TypeFont, 18, TextAnchor.UpperLeft, false, true);
				foreach (GameData.Slip sl in GameData.Slips)
					total += 64f + (string.IsNullOrEmpty(sl.Body) ? 0f : bm.CalcHeight(new GUIContent(sl.Body), 430f - 36f)) + 14f;
				y = Mathf.Max(at.y, bottomY - total);
			}
			GUIStyle head = Art.Text(Art.TypeFont, 14, TextAnchor.UpperLeft);
			GUIStyle body = Art.Text(Art.TypeFont, 18, TextAnchor.UpperLeft, false, true);
			const float w = 430f;
			int i = 0;
			foreach (GameData.Slip s in GameData.Slips)
			{
				float aIn = Mathf.Clamp01((now - s.Born) / 0.22f);
				float aOut = s.Gone >= 0f ? 1f - Mathf.Clamp01((now - s.Gone) / 0.45f) : 1f;
				float a = Mathf.SmoothStep(0f, 1f, aIn) * aOut;
				float slide = (1f - Mathf.SmoothStep(0f, 1f, aIn)) * -40f;
				float bodyH = string.IsNullOrEmpty(s.Body) ? 0f : body.CalcHeight(new GUIContent(s.Body), w - 40f);
				Rect r = new Rect(at.x + slide, y, w, 8f + 30f + 8f + bodyH + 18f);
				float saved = Art.Alpha;
				Art.Alpha = saved * a;
				Art.Form(r);
				Rect inner = Art.Inner(r);
				Art.Rect(new Rect(inner.x, inner.y, 14f, 30f), s.Ink);
				Art.VRule(inner.x + 14f, inner.y, 30f);
				Art.Label(new Rect(inner.x + 24f, inner.y + 7f, inner.width - 30f, 18f), Spaced(s.Head), head, Art.Ink);
				Art.HRule(inner.x, inner.y + 30f, inner.width);
				Art.Label(new Rect(inner.x + 12f, inner.y + 38f, inner.width - 24f, bodyH + 4f), s.Body, body, Art.Ink);
				Art.Wear(r);
				Art.Alpha = saved;
				y += r.height + 14f;
				i++;
			}
		}

		private static string Spaced(string s)
		{
			// letter-spaced headings, like typed capitals
			var sb = new System.Text.StringBuilder(s.Length * 2);
			foreach (char c in s) { sb.Append(c); if (c != ' ') sb.Append(' '); }
			return sb.ToString();
		}

		// ---- Equipped: luggage tags ----

		private static void Tags(float margin)
		{
			if (GameData.Left.Any) Tag(new Rect(margin + 12f, vh - margin - 96f - 12f, 260f, 96f), GameData.Left, 0f, false);
			if (GameData.Right.Any) Tag(new Rect(vw - margin - 12f - 260f, vh - margin - 96f - 12f, 260f, 96f), GameData.Right, 0f, true);
		}

		// An equipped item as a ruled card: the item in a cell, its name over its amount and a bar.
		private static void Tag(Rect r, GameData.Hand h, float tilt, bool iconRight)
		{
			Art.Form(r);
			Rect inner = Art.Inner(r);
			Rect icon = iconRight ? new Rect(inner.xMax - inner.height, inner.y, inner.height, inner.height) : new Rect(inner.x, inner.y, inner.height, inner.height);
			Art.VRule(iconRight ? icon.x : icon.xMax, inner.y, inner.height);
			Art.Sprite(new Rect(icon.x + 8f, icon.y + 8f, icon.width - 16f, icon.height - 16f), h.Icon, Color.white);
			float tx = (iconRight ? inner.x : icon.xMax) + 12f, tw = inner.width - icon.width - 24f;
			string title = h.Name.ToUpperInvariant();
			int size = 13;
			GUIStyle name = Art.Text(Art.TypeFont, size, TextAnchor.UpperLeft);
			while (size > 8 && Art.Measure(title, name).x > tw) name = Art.Text(Art.TypeFont, --size, TextAnchor.UpperLeft);
			Art.Label(new Rect(tx, inner.y + 8f, tw, 16f), title, name, Art.Ink);
			Art.HRule(iconRight ? inner.x : icon.xMax, inner.y + 30f, inner.width - icon.width);
			bool low = h.Fill >= 0f && h.Fill < 0.2f;
			if (!string.IsNullOrEmpty(h.Amount))
				Art.Label(new Rect(tx, inner.y + 34f, tw, 26f), h.Amount, Art.Text(Art.TypeFont, 20, TextAnchor.UpperLeft, true), low ? Art.Red : Art.Ink);
			if (h.Fill >= 0f)
			{
				Rect bar = new Rect(tx, inner.yMax - 18f, tw, 10f);
				Art.Outline(bar, 2f, Art.Rule);
				Art.Rect(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.Clamp01(h.Fill), bar.height - 4f), low ? Art.Red : Art.Reserve(h.Fill * 100f));
			}
			Art.Wear(r);
		}

		// ---- Menus: the status clipboard ----

		// The status sheet: a ruled form held by a clip, CONDITION and NOTES tables.
		private static void Clipboard(Rect r)
		{
			Art.Form(r);
			Rect clip = new Rect(r.center.x - 56f, r.y - 16f, 112f, 30f);
			Art.ShadowFor(clip, 6f, 3f, 0.5f);
			Art.Rect(clip, new Color32(130, 130, 128, 255));
			Art.Rect(new Rect(clip.x, clip.yMax - 8f, clip.width, 8f), new Color32(79, 79, 77, 255));
			Rect inner = Art.Inner(r);
			GUIStyle h = Art.Text(Art.TypeFont, 15, TextAnchor.MiddleCenter);
			GUIStyle row = Art.Text(Art.TypeFont, 15, TextAnchor.MiddleLeft);
			GUIStyle num = Art.Text(Art.TypeFont, 20, TextAnchor.MiddleCenter, true);
			float x = inner.x, w = inner.width, y = inner.y + 20f;
			Art.Label(new Rect(x, y - 20f, w, 36f), Spaced("CONDITION"), h, Art.Ink);
			y += 16f; Art.HRule(x, y, w);
			float top = y;
			string[] names = { "Health", "Mind", "Food", "Water", "Rest", "Toilet", "Clean" };
			float[] vals = { GameData.Health, GameData.Mind, GameData.Food, GameData.Water, GameData.Rest, GameData.Toilet, GameData.Clean };
			for (int i = 0; i < names.Length; i++) Row(ref y, x, w, names[i], vals[i], row, num);
			Art.VRule(x + w - 58f, top, y - top);
			Art.Label(new Rect(x, y + 4f, w, 36f), Spaced("NOTES"), h, Art.Ink);
			y += 40f; Art.HRule(x, y, w); y += 6f;
			GUIStyle note = Art.Text(Art.TypeFont, 16, TextAnchor.MiddleLeft, false, true);
			if (GameData.Warnings.Count == 0)
				Art.Label(new Rect(x + 12f, y, w - 24f, 28f), "Nothing to worry about.", note, Art.InkFaint);
			foreach (var wn in GameData.Warnings)
			{
				if (y > inner.yMax - 34f) break;
				string t = wn.Text + (wn.Text == "Police" || wn.Text == "Rad" || wn.Text == "Fungus" ? ": " + wn.Level : "");
				Art.Rect(new Rect(x + 12f, y + 10f, 9f, 9f), wn.Ink);
				Art.Label(new Rect(x + 30f, y, w - 40f, 28f), t, note, wn.Severe ? Art.Red : Art.Ink);
				y += 28f;
				Art.HRule(x, y, w);
			}
			Art.Wear(r);
		}

		// A ruled row: name | bar | value
		private static void Row(ref float y, float x, float w, string label, float v, GUIStyle row, GUIStyle num)
		{
			bool critical = v < 10f;
			Color ink = critical ? Art.Red : Art.Ink;
			const float rh = 38f;
			Art.Label(new Rect(x + 12f, y, 90f, rh), label, row, ink);
			Rect bar = new Rect(x + 96f, y + rh / 2f - 5f, w - 96f - 72f, 10f);
			Art.Outline(bar, 2f, Art.Rule);
			Art.Rect(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.Clamp01(v / 100f), bar.height - 4f), Art.Reserve(v));
			Art.Label(new Rect(x + w - 58f, y, 58f, rh), Mathf.RoundToInt(v) + (critical ? "!" : ""), num, ink);
			y += rh;
			Art.HRule(x, y, w);
		}

		// ---- Menus: manila folder tabs and the storage box label (drawn over the game's own, which still take the clicks) ----

		// A UI element's rectangle on our virtual (scaled) screen.
		private static bool VirtualRect(RectTransform rt, float scale, out Rect r)
		{
			r = default(Rect);
			if (rt == null) return false;
			var c = new Vector3[4];
			rt.GetWorldCorners(c);
			Canvas canvas = rt.GetComponentInParent<Canvas>();
			Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
			r = new Rect(Mathf.Min(a.x, b.x) / scale, (Screen.height - Mathf.Max(a.y, b.y)) / scale, Mathf.Abs(b.x - a.x) / scale, Mathf.Abs(b.y - a.y) / scale);
			return r.width > 4f && r.height > 4f;
		}

		private static System.Reflection.FieldInfo pagesField, storageNameField;

		private static void FolderTabs(float scale)
		{
			UIPageButtons ui = UIPageButtons.instance;
			if (ui == null) return;
			if (pagesField == null) pagesField = HarmonyLib.AccessTools.Field(typeof(UIPageButtons), "MenuPages");
			var pages = pagesField != null ? pagesField.GetValue(ui) as MenuPage[] : null;
			if (pages == null) return;
			GUIStyle on = Art.Text(Art.TypeFont, 17, TextAnchor.MiddleCenter, true), off = Art.Text(Art.TypeFont, 15, TextAnchor.MiddleCenter);
			foreach (MenuPage p in pages)
			{
				if (p == null || p.Button == null || !NativeHud.EffectivelyVisible(p.Button.transform)) continue;
				Rect r;
				if (!VirtualRect(p.Button.transform as RectTransform, scale, out r)) continue;
				bool sel = p.Name == ui.currentPage;
				// the tab grows up from the bottom of the game's button
				float h = sel ? r.height + 8f : r.height - 2f;
				Rect tab = new Rect(r.x + 2f, r.yMax - h, r.width - 4f, h);
				Art.ShadowFor(tab, 6f, 3f, 0.45f);
				if (sel)
				{
					// the game's own amber for the open page
					Art.Rect(tab, Art.AmberDark);
					Art.Rect(new Rect(tab.x, tab.y, tab.width, tab.height * 0.55f), Art.Amber);
					Art.Rect(new Rect(tab.x, tab.y, tab.width, 1f), new Color(1f, 1f, 1f, 0.18f));
				}
				else { Art.Tiled(tab, Art.PaperTex, Color.white); Art.Outline(new Rect(tab.x + 3f, tab.y + 3f, tab.width - 6f, tab.height - 3f), 1f, new Color(Art.Rule.r, Art.Rule.g, Art.Rule.b, 0.6f)); }
				string label = PageTitle(p.Name);
				Art.Label(tab, Spaced((label ?? "").ToUpperInvariant()), sel ? on : off, sel ? Art.Cream : Art.InkSoft);
			}
		}

		private static string PageTitle(MenuPageNames n)
		{
			switch (n)
			{
				case MenuPageNames.Inventory: return "Inventory";
				case MenuPageNames.Character: return "Character";
				case MenuPageNames.Tenement: return "Tenement";
				case MenuPageNames.Map: return "Map";
				case MenuPageNames.Tasks: return "Tasks";
				case MenuPageNames.Notes: return "Notes";
				case MenuPageNames.Items: return "Items";
			}
			return n.ToString();
		}

		private static void StorageLabel(float scale)
		{
			StoragePanelUI panel = Inventory.instance != null ? Inventory.instance.storagePanelUI : null;
			if (panel == null || !NativeHud.EffectivelyVisible(panel.transform)) return;
			if (storageNameField == null) storageNameField = HarmonyLib.AccessTools.Field(typeof(StoragePanelUI), "storageName");
			var tmp = storageNameField != null ? storageNameField.GetValue(panel) as TMPro.TextMeshProUGUI : null;
			if (tmp == null || !tmp.isActiveAndEnabled || string.IsNullOrEmpty(tmp.text)) return;
			Rect r;
			if (!VirtualRect(tmp.rectTransform, scale, out r)) return;
			string text = BoxLabel(tmp.text);
			GUIStyle st = Art.Text(Art.TypeFont, 17, TextAnchor.MiddleLeft, true);
			float w = Mathf.Min(Mathf.Max(r.width, Art.Measure(text, st).x + 28f), r.width + 60f);
			Rect label = new Rect(r.x - 6f, r.center.y - 22f, w + 30f, 44f);
			Art.Form(label);
			Rect inner = Art.Inner(label);
			Art.Rect(new Rect(inner.x, inner.y, 14f, inner.height), Art.Amber);
			Art.VRule(inner.x + 14f, inner.y, inner.height);
			Art.Label(new Rect(inner.x + 24f, inner.y, inner.width - 28f, inner.height), text, st, Art.Ink);
			Art.Wear(label);
		}

		// "Bank Inventory — Page 2/4" -> "BANK · box 2 of 4"; other storages keep their name.
		private static string BoxLabel(string raw)
		{
			string t = System.Text.RegularExpressions.Regex.Replace(raw, "<[^>]+>", "").Trim();
			var m = System.Text.RegularExpressions.Regex.Match(t, @"^(.*?)\s*[\u2014-]\s*Page\s*(\d+)\s*/\s*(\d+)\s*$");
			if (m.Success)
			{
				string name = m.Groups[1].Value.Replace("Inventory", "").Trim();
				return name.ToUpperInvariant() + " \u00b7 box " + m.Groups[2].Value + " of " + m.Groups[3].Value;
			}
			var f = System.Text.RegularExpressions.Regex.Match(t, @"^(.*?)\s*[\u2014-]\s*(\d+)\s*found");
			if (f.Success) return f.Groups[1].Value.Replace("Inventory", "").Trim().ToUpperInvariant() + " \u00b7 " + f.Groups[2].Value + " found";
			return t.ToUpperInvariant();
		}
	}
}
