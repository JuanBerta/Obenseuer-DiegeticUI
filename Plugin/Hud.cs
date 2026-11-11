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

		internal static void Draw(bool menu, float opacity)
		{
			Art.Init();
			float s = Screen.height / VH * DiegeticPlugin.Scale.Value;
			vw = Screen.width / s;
			Matrix4x4 saved = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
			Art.Alpha = menu ? 1f : opacity;
			try
			{
				float m = DiegeticPlugin.EdgeMargin.Value;
				if (DiegeticPlugin.ShowFeed.Value) Slips(new Vector2(m + 12f, m + 10f));
				if (menu)
				{
					if (DiegeticPlugin.RestyleMenus.Value) { FolderTabs(s); StorageLabel(s); }
					float gutter = vw / 2f + 547f;   // right edge of the game's inventory panel
					float gx = gutter + Mathf.Max(16f, (vw - gutter - 300f) / 2f);
					if (DiegeticPlugin.ShowWallet.Value) CompactWatchAndReceipt(new Vector2(gx, 22f));
					if (DiegeticPlugin.ShowNeeds.Value) Clipboard(new Rect(gx, 250f, 300f, 640f));
				}
				else
				{
					if (DiegeticPlugin.ShowWallet.Value) WatchAndReceipt(new Vector2(vw - m - 28f - 130f, 0f));
					if (DiegeticPlugin.ShowNeeds.Value)
					{
						float bottom = VH - DiegeticPlugin.BottomMargin.Value;
						Stamps(new Vector2(vw / 2f, bottom - 196f));
						Strip(new Vector2(vw / 2f, bottom));
					}
				}
				if (DiegeticPlugin.ShowHands.Value) Tags(m);
			}
			catch (Exception e) { DiegeticPlugin.WarnOnce("draw", "Drawing the HUD: " + e); }
			finally { GUI.matrix = saved; Art.Alpha = 1f; }
		}

		// ---- Survival strip: two dials and five ink gauges on masking tape ----

		private static float Pulse { get { return 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f); } }

		private static void Strip(Vector2 bottomCenter)
		{
			const float dialW = 132f, dialH = 150f, gap = 18f;
			const float gaugeW = 22f, gaugeGap = 30f, tapePad = 22f;
			float tapeW = 5 * gaugeW + 4 * gaugeGap + tapePad * 2 + 20f, tapeH = 150f;
			float total = dialW * 2 + tapeW + gap * 2;
			float x = bottomCenter.x - total / 2f, y = bottomCenter.y - dialH;

			Dial(new Rect(x, y, dialW, dialH), "HEALTH", GameData.Health, Art.Red, -2f);
			Rect tape = new Rect(x + dialW + gap, bottomCenter.y - tapeH, tapeW, tapeH);
			using (new Art.Turn(0.6f, tape.center))
			{
				Art.ShadowFor(tape, 8f, 4f, 0.35f);
				Art.TapeStrip(tape);
				float gx = tape.x + tapePad + 10f;
				Gauge(new Vector2(gx, tape.y + 12f), "FOOD", GameData.Food); gx += gaugeW + gaugeGap;
				Gauge(new Vector2(gx, tape.y + 12f), "WATER", GameData.Water); gx += gaugeW + gaugeGap;
				Gauge(new Vector2(gx, tape.y + 12f), "REST", GameData.Rest); gx += gaugeW + gaugeGap;
				Gauge(new Vector2(gx, tape.y + 12f), "TOILET", GameData.Toilet); gx += gaugeW + gaugeGap;
				Gauge(new Vector2(gx, tape.y + 12f), "CLEAN", GameData.Clean);
			}
			Dial(new Rect(tape.xMax + gap, y, dialW, dialH), "MIND", GameData.Mind, Art.Blue, 2f);
		}

		private static void Gauge(Vector2 top, string label, float v)
		{
			bool critical = v < 10f;
			Color ink = critical ? Art.Red : Art.Ink;
			int n = Mathf.RoundToInt(v);
			GUIStyle num = Art.Text(Art.HandFont, 26, TextAnchor.UpperCenter, true);
			Art.Label(new Rect(top.x - 20f, top.y - 4f, 62f, 30f), n.ToString(), num, ink);
			Rect box = new Rect(top.x, top.y + 28f, 22f, 84f);
			if (critical) Art.Rect(new Rect(box.x - 4f, box.y - 4f, box.width + 8f, box.height + 8f), new Color(Art.Red.r, Art.Red.g, Art.Red.b, 0.15f + 0.3f * Pulse));
			float h = box.height * Mathf.Clamp01(v / 100f);
			Art.Rect(new Rect(box.x, box.yMax - h, box.width, h), Art.Reserve(v));
			Art.Outline(box, 2f, critical ? Art.Red : Art.InkSoft);
			GUIStyle lab = Art.Text(Art.TypeFont, 12, TextAnchor.UpperCenter);
			Art.Label(new Rect(top.x - 25f, box.yMax + 5f, 72f, 16f), label, lab, ink);
		}

		private static void Dial(Rect r, string label, float v, Color accent, float tilt)
		{
			using (new Art.Turn(tilt, r.center))
			{
				Art.PaperSheet(r);
				Vector2 c = new Vector2(r.center.x, r.y + 70f);
				float t = Mathf.Clamp01(v / 100f);
				Art.Arc(c, 44f, 8f, 0f, 1f, new Color32(201, 191, 166, 255));
				if (t > 0.005f) Art.Arc(c, 44f, 8f, 0f, t, v < 10f ? Color.Lerp(accent, Art.Red, Pulse) : accent);
				Art.Line(c, c + Art.Polar(t, 38f), 3f, Art.Ink);
				Art.Circle(c, 5f, Art.Ink);
				GUIStyle lab = Art.Text(Art.TypeFont, 13, TextAnchor.UpperCenter);
				Art.Label(new Rect(r.x, c.y + 10f, r.width, 18f), label, lab, Art.Ink);
				GUIStyle num = Art.Text(Art.HandFont, 30, TextAnchor.UpperCenter, true);
				Art.Label(new Rect(r.x, c.y + 26f, r.width, 34f), Mathf.RoundToInt(v).ToString(), num, v < 10f ? Art.Red : Art.Ink);
			}
		}

		// ---- Warnings: rubber stamps ----

		private static readonly float[] StampTilts = { -6f, 3f, -2f, 4f, -3f, 2f, -5f, 1f };

		private static void Stamps(Vector2 center)
		{
			List<GameData.Warning> list = GameData.Warnings;
			if (list.Count == 0) return;
			GUIStyle st = Art.Text(Art.TypeFont, 20, TextAnchor.MiddleCenter, true);
			var sizes = new List<Vector2>();
			float total = 0f;
			foreach (var w in list)
			{
				Vector2 sz = Art.Measure(StampText(w), st) + new Vector2(26f, 12f);
				sizes.Add(sz);
				total += sz.x + 22f;
			}
			float x = center.x - (total - 22f) / 2f;
			for (int i = 0; i < list.Count; i++)
			{
				var w = list[i];
				Rect r = new Rect(x, center.y - sizes[i].y / 2f, sizes[i].x, sizes[i].y);
				float alpha = w.Severe ? 0.7f + 0.3f * Pulse : 0.88f;
				Color ink = new Color(w.Ink.r, w.Ink.g, w.Ink.b, alpha);
				using (new Art.Turn(StampTilts[i % StampTilts.Length], r.center))
				{
					Art.Rect(r, new Color(0.91f, 0.88f, 0.79f, 0.16f));
					Art.Outline(r, 3f, ink);
					Art.Outline(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), 1f, new Color(ink.r, ink.g, ink.b, alpha * 0.5f));
					Art.Label(r, StampText(w), st, ink);
				}
				x += sizes[i].x + 22f;
			}
		}

		private static string StampText(GameData.Warning w)
		{
			string t = w.Text.ToUpperInvariant();
			if (w.Text == "Police" || w.Text == "Rad" || w.Text == "Fungus") t += " · " + w.Level;
			return t;
		}

		// ---- Wristwatch and receipt wallet ----

		private static void WatchAndReceipt(Vector2 topCenter)
		{
			float cx = topCenter.x;
			// strap
			Art.Rect(new Rect(cx - 46f, 0f, 92f, 46f), Art.Strap);
			Art.Rect(new Rect(cx - 49f, 0f, 3f, 46f), new Color32(26, 20, 15, 255));
			Art.Rect(new Rect(cx + 46f, 0f, 3f, 46f), new Color32(26, 20, 15, 255));
			Rect face = new Rect(cx - 110f, 46f, 220f, 150f);
			Watch(face, 70f);
			Art.Rect(new Rect(cx - 46f, face.yMax, 92f, 22f), Art.Strap);
			Receipt(new Rect(cx - 116f, face.yMax + 14f, 232f, 0f), 1.5f, true);
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
			Art.Rect(new Rect(face.x + 5f, face.y + 5f, face.width - 10f, face.height - 10f), new Color32(42, 42, 39, 255));
			Rect screen = new Rect(face.x + face.width * 0.1f, face.y + face.height * 0.14f, face.width * 0.8f, face.height * 0.72f);
			bool on = GameData.HasClock && GameData.Hours >= 0;
			Art.Rect(screen, on ? Art.Lcd : Art.LcdOff);
			Art.Rect(new Rect(screen.x, screen.y, screen.width, 3f), new Color(0, 0, 0, 0.25f));
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

		private static void Receipt(Rect top, float tilt, bool lines)
		{
			float now = Time.unscaledTime;
			var show = new List<GameData.Entry>();
			if (lines)
				for (int i = GameData.Entries.Count - 1; i >= 0 && show.Count < 4; i--)
					if (now - GameData.Entries[i].Born < 9f) show.Add(GameData.Entries[i]);
			float big = top.width > 200f ? 24f : 18f;
			float h = 16f + 12f + big * 2.3f + (show.Count > 0 ? 18f + show.Count * 20f : 0f) + 8f;
			Rect r = new Rect(top.x, top.y, top.width, h);
			using (new Art.Turn(tilt, r.center))
			{
				Art.ShadowFor(new Rect(r.x, r.y, r.width, r.height + 8f), 10f, 6f, 0.5f);
				Art.Tiled(r, Art.ReceiptTex, Color.white);
				Art.Tiled(new Rect(r.x, r.yMax, r.width, 8f), Art.Zigzag, Color.white);
				float y = r.y + 10f;
				if (top.width > 200f)
				{
					Art.Label(new Rect(r.x, y, r.width, 16f), "— WALLET —", Art.Text(Art.ReceiptFont, 12, TextAnchor.UpperCenter), new Color32(109, 106, 98, 255));
					y += 18f;
				}
				GUIStyle left = Art.Text(Art.ReceiptFont, Mathf.RoundToInt(big), TextAnchor.UpperLeft, true);
				GUIStyle right = Art.Text(Art.ReceiptFont, Mathf.RoundToInt(big), TextAnchor.UpperRight, true);
				Color ink = new Color32(44, 42, 38, 255);
				Color ocInk = Flash(GameData.FlashOC, GameData.LastOCGain, ink), rmInk = Flash(GameData.FlashRM, GameData.LastRMGain, ink);
				Art.Label(new Rect(r.x + 14f, y, 60f, big + 4f), "OC", left, ink);
				Art.Label(new Rect(r.x, y, r.width - 14f, big + 4f), GameData.FormatOC(GameData.OC), right, ocInk);
				y += big + 4f;
				Art.Label(new Rect(r.x + 14f, y, 60f, big + 4f), "RM", left, ink);
				Art.Label(new Rect(r.x, y, r.width - 14f, big + 4f), GameData.FormatRM(GameData.RM), right, rmInk);
				y += big + 8f;
				if (show.Count > 0)
				{
					for (float dx = r.x + 12f; dx < r.xMax - 12f; dx += 9f) Art.Rect(new Rect(dx, y, 5f, 2f), new Color32(185, 180, 168, 255));
					y += 10f;
					GUIStyle l = Art.Text(Art.ReceiptFont, 15, TextAnchor.UpperLeft), rr = Art.Text(Art.ReceiptFont, 15, TextAnchor.UpperRight);
					foreach (var e in show)
					{
						float age = now - e.Born;
						float a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((9f - age) / 1.2f);
						Color c = e.Gain ? new Color32(47, 106, 42, 255) : new Color32(154, 42, 28, 255);
						c.a = a;
						Art.Label(new Rect(r.x + 14f, y, r.width - 28f, 20f), e.Gain ? "+ received" : "− paid", l, c);
						Art.Label(new Rect(r.x, y, r.width - 14f, 20f), GameData.FormatDelta(e), rr, c);
						y += 20f;
					}
				}
			}
		}

		private static Color Flash(float when, bool gain, Color normal)
		{
			float age = Time.unscaledTime - when;
			if (when <= 0f || age > 1.6f) return normal;
			Color f = gain ? new Color32(47, 106, 42, 255) : new Color32(154, 42, 28, 255);
			return Color.Lerp(f, normal, Mathf.Clamp01((age - 0.8f) / 0.8f));
		}

		// ---- Notifications: typed slips, top-left ----

		private static void Slips(Vector2 at)
		{
			float y = at.y, now = Time.unscaledTime;
			GUIStyle head = Art.Text(Art.TypeFont, 13, TextAnchor.UpperLeft);
			GUIStyle body = Art.Text(Art.TypeFont, 19, TextAnchor.UpperLeft, false, true);
			const float w = 430f;
			int i = 0;
			foreach (GameData.Slip s in GameData.Slips)
			{
				float aIn = Mathf.Clamp01((now - s.Born) / 0.22f);
				float aOut = s.Gone >= 0f ? 1f - Mathf.Clamp01((now - s.Gone) / 0.45f) : 1f;
				float a = Mathf.SmoothStep(0f, 1f, aIn) * aOut;
				float slide = (1f - Mathf.SmoothStep(0f, 1f, aIn)) * -40f;
				float bodyH = string.IsNullOrEmpty(s.Body) ? 0f : body.CalcHeight(new GUIContent(s.Body), w - 36f);
				Rect r = new Rect(at.x + slide + (i % 2 == 1 ? 16f : 0f), y, w - (i % 2 == 1 ? 40f : 0f), 14f + 18f + bodyH + 14f);
				float saved = Art.Alpha;
				Art.Alpha = saved * a;
				using (new Art.Turn(s.Tilt, r.center))
				{
					Art.PaperSheet(r);
					if (i == 0) Art.TapeStrip(new Rect(r.x + r.width * 0.36f, r.y - 10f, 90f, 22f));
					Art.Label(new Rect(r.x + 18f, r.y + 12f, r.width - 36f, 16f), Spaced(s.Head), head, s.Ink);
					Art.Label(new Rect(r.x + 18f, r.y + 32f, r.width - 36f, bodyH + 4f), s.Body, body, Art.Ink);
				}
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
			if (GameData.Left.Any) Tag(new Rect(margin + 16f, VH - margin - 96f - 12f, 190f, 96f), GameData.Left, -3f, true);
			if (GameData.Right.Any) Tag(new Rect(vw - margin - 16f - 190f, VH - margin - 96f - 12f, 190f, 96f), GameData.Right, 3f, false);
		}

		private static void Tag(Rect r, GameData.Hand h, float tilt, bool holeRight)
		{
			using (new Art.Turn(tilt, r.center))
			{
				Art.PaperSheet(r);
				Vector2 hole = holeRight ? new Vector2(r.xMax - 18f, r.center.y) : new Vector2(r.x + 18f, r.center.y);
				Art.Circle(hole, 8f, new Color32(185, 169, 138, 255));
				Art.Circle(hole, 5f, new Color32(26, 23, 18, 255));
				float x = holeRight ? r.x + 12f : r.x + 34f;
				Art.Sprite(new Rect(x, r.y + 14f, 64f, 64f), h.Icon, Color.white);
				float tx = x + 72f, tw = r.width - (tx - r.x) - (holeRight ? 30f : 10f);
				// the name on one line, shrunk to fit
				string title = h.Name.ToUpperInvariant();
				int size = 12;
				GUIStyle name = Art.Text(Art.TypeFont, size, TextAnchor.UpperLeft);
				while (size > 8 && Art.Measure(title, name).x > tw) name = Art.Text(Art.TypeFont, --size, TextAnchor.UpperLeft);
				Art.Label(new Rect(tx, r.y + 16f, tw, 16f), title, name, Art.Ink);
				bool low = h.Fill >= 0f && h.Fill < 0.2f;
				if (h.Fill >= 0f)
				{
					if (h.Battery)
					{
						int segs = h.Fill <= 0.001f ? 0 : h.Fill < 0.34f ? 1 : h.Fill < 0.67f ? 2 : 3;
						for (int i = 0; i < 3; i++)
						{
							Rect sr = new Rect(tx + i * 17f, r.y + 38f, 14f, 16f);
							if (i < segs) Art.Rect(sr, low ? Art.Red : Art.Reserve(h.Fill * 100f)); else Art.Outline(sr, 2f, Art.InkSoft);
						}
						Art.Rect(new Rect(tx + 51f, r.y + 42f, 4f, 8f), Art.InkSoft);
					}
					else
					{
						Rect bar = new Rect(tx, r.y + 42f, Mathf.Min(62f, tw), 9f);
						Art.Rect(new Rect(bar.x, bar.y, bar.width * h.Fill, bar.height), low ? Art.Red : Art.Reserve(h.Fill * 100f));
						Art.Outline(bar, 2f, Art.InkSoft);
					}
				}
				if (!string.IsNullOrEmpty(h.Amount))
					Art.Label(new Rect(tx, r.y + 58f, tw, 26f), h.Amount, Art.Text(Art.HandFont, 20, TextAnchor.UpperLeft, true), low ? Art.Red : Art.Ink);
			}
		}

		// ---- Menus: the status clipboard ----

		private static void Clipboard(Rect r)
		{
			Art.ShadowFor(r, 16f, 10f, 0.6f);
			Art.Rect(r, new Color32(122, 90, 52, 255));
			Art.Outline(r, 3f, new Color32(94, 68, 38, 255));
			Rect clip = new Rect(r.center.x - 50f, r.y - 14f, 100f, 42f);
			Art.ShadowFor(clip, 6f, 3f, 0.5f);
			Art.Rect(clip, new Color32(154, 154, 148, 255));
			Art.Rect(new Rect(clip.x, clip.yMax - 6f, clip.width, 6f), new Color32(111, 111, 106, 255));
			Rect sheet = new Rect(r.x + 18f, r.y + 34f, r.width - 36f, r.height - 52f);
			Art.PaperSheet(sheet, false);

			GUIStyle h = Art.Text(Art.TypeFont, 15, TextAnchor.UpperLeft);
			GUIStyle row = Art.Text(Art.TypeFont, 15, TextAnchor.UpperLeft);
			GUIStyle num = Art.Text(Art.HandFont, 26, TextAnchor.UpperRight, true);
			float x = sheet.x + 22f, w = sheet.width - 44f, y = sheet.y + 20f;
			Art.Label(new Rect(x, y, w, 20f), Spaced("CONDITION"), h, Art.Ink);
			y += 22f; Art.Rect(new Rect(x, y, w, 2f), Art.Ink); y += 14f;
			Row(ref y, x, w, "Health", GameData.Health, row, num);
			Row(ref y, x, w, "Mind", GameData.Mind, row, num);
			Row(ref y, x, w, "Food", GameData.Food, row, num);
			Row(ref y, x, w, "Water", GameData.Water, row, num);
			Row(ref y, x, w, "Rest", GameData.Rest, row, num);
			Row(ref y, x, w, "Toilet", GameData.Toilet, row, num);
			Row(ref y, x, w, "Clean", GameData.Clean, row, num);
			y += 16f;
			Art.Label(new Rect(x, y, w, 20f), Spaced("NOTES"), h, Art.Ink);
			y += 22f; Art.Rect(new Rect(x, y, w, 2f), Art.Ink); y += 10f;
			GUIStyle pen = Art.Text(Art.HandFont, 23, TextAnchor.UpperLeft, true, true);
			if (GameData.Warnings.Count == 0)
				Art.Label(new Rect(x, y, w, 28f), "nothing to worry about", pen, new Color(Art.Blue.r, Art.Blue.g, Art.Blue.b, 0.7f));
			foreach (var wn in GameData.Warnings)
			{
				if (y > sheet.yMax - 40f) break;
				string t = wn.Text + (wn.Text == "Police" || wn.Text == "Rad" || wn.Text == "Fungus" ? ": " + wn.Level : "");
				Art.Label(new Rect(x, y, w, 28f), t, pen, wn.Severe ? Art.Red : Art.Blue);
				y += 27f;
			}
		}

		private static void Row(ref float y, float x, float w, string label, float v, GUIStyle row, GUIStyle num)
		{
			bool critical = v < 10f;
			Color ink = critical ? Art.Red : Art.Ink;
			Art.Label(new Rect(x, y + 6f, w, 20f), label, row, ink);
			Art.Label(new Rect(x, y - 2f, w, 30f), Mathf.RoundToInt(v) + (critical ? " !" : ""), num, ink);
			// a short ink bar under the number
			float bw = 64f;
			Art.Rect(new Rect(x + w - bw - 58f, y + 16f, bw * Mathf.Clamp01(v / 100f), 4f), Art.Reserve(v));
			y += 34f;
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
				Art.Tiled(tab, Art.PaperTex, sel ? new Color(0.93f, 0.85f, 0.67f, 1f) : new Color(0.72f, 0.66f, 0.5f, 1f));
				// rounded-looking top corners
				Color bg = new Color32(28, 25, 21, 255);
				Art.Rect(new Rect(tab.x, tab.y, 4f, 3f), bg); Art.Rect(new Rect(tab.x, tab.y, 2f, 6f), bg);
				Art.Rect(new Rect(tab.xMax - 4f, tab.y, 4f, 3f), bg); Art.Rect(new Rect(tab.xMax - 2f, tab.y, 2f, 6f), bg);
				if (sel) Art.Rect(new Rect(tab.x + 6f, tab.yMax - 3f, tab.width - 12f, 3f), new Color32(185, 166, 112, 255));
				string label = p.Text != null ? p.Text.text : p.Name.ToString();
				Art.Label(tab, Spaced((label ?? "").ToUpperInvariant()), sel ? on : off, sel ? Art.Ink : (Color)new Color32(58, 50, 35, 255));
			}
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
			Rect label = new Rect(r.x - 6f, r.center.y - 18f, w, 36f);
			using (new Art.Turn(-1f, label.center))
			{
				Art.PaperSheet(label);
				Art.Label(new Rect(label.x + 14f, label.y, label.width - 20f, label.height), text, st, Art.Ink);
			}
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
