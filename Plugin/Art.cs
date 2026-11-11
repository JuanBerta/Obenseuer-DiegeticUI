using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiegeticUIMod
{
	/// <summary>Generated textures, fonts and the small drawing toolkit the HUD is built from.</summary>
	internal static class Art
	{
		// ---- Palette ----
		internal static readonly Color Paper = new Color32(233, 224, 201, 255);
		internal static readonly Color PaperDark = new Color32(217, 199, 156, 255);
		internal static readonly Color Ink = new Color32(43, 36, 24, 255);
		internal static readonly Color InkSoft = new Color32(59, 49, 34, 255);
		internal static readonly Color Red = new Color32(154, 42, 28, 255);
		internal static readonly Color StampRed = new Color32(163, 35, 26, 255);
		internal static readonly Color Green = new Color32(63, 107, 46, 255);
		internal static readonly Color Olive = new Color32(125, 138, 34, 255);
		internal static readonly Color Mustard = new Color32(138, 122, 34, 255);
		internal static readonly Color Rust = new Color32(160, 90, 28, 255);
		internal static readonly Color Blue = new Color32(42, 74, 122, 255);
		internal static readonly Color Brown = new Color32(107, 74, 20, 255);
		internal static readonly Color Tape = new Color32(221, 203, 156, 215);
		internal static readonly Color Receipt = new Color32(241, 238, 230, 255);
		internal static readonly Color Lcd = new Color32(154, 167, 138, 255);
		internal static readonly Color LcdOff = new Color32(111, 122, 98, 255);
		internal static readonly Color LcdInk = new Color32(31, 38, 26, 255);
		internal static readonly Color Steel = new Color32(59, 59, 56, 255);
		internal static readonly Color SteelLight = new Color32(85, 85, 79, 255);
		internal static readonly Color Strap = new Color32(42, 33, 25, 255);

		// Reserve colour: green when full, through olive and mustard, to rust and red near empty.
		internal static Color Reserve(float v)
		{
			if (v >= 70f) return Green;
			if (v >= 50f) return Color.Lerp(Olive, Green, (v - 50f) / 20f);
			if (v >= 30f) return Color.Lerp(Mustard, Olive, (v - 30f) / 20f);
			if (v >= 10f) return Color.Lerp(Rust, Mustard, (v - 10f) / 20f);
			return Red;
		}

		// ---- Textures ----
		internal static Texture2D White, PaperTex, ReceiptTex, TapeTex, Shadow, Disk, Ring, Zigzag;

		internal static void Init()
		{
			if (White != null) return;
			White = Solid(Color.white);
			PaperTex = Noise(128, Paper, 0.06f, 7);
			ReceiptTex = Noise(64, Receipt, 0.025f, 11);
			TapeTex = Noise(64, Tape, 0.05f, 3);
			Shadow = MakeShadow(48);
			Disk = MakeDisk(64, false);
			Ring = MakeDisk(64, true);
			Zigzag = MakeZigzag(32, 8);
			LoadFonts();
		}

		private static Texture2D NewTex(int w, int h)
		{
			var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
			return t;
		}

		private static Texture2D Solid(Color c)
		{
			var t = NewTex(2, 2);
			t.SetPixels(new[] { c, c, c, c });
			t.Apply();
			return t;
		}

		private static Texture2D Noise(int size, Color baseColor, float amount, int seed)
		{
			var t = NewTex(size, size);
			t.wrapMode = TextureWrapMode.Repeat;
			var rnd = new System.Random(seed);
			var px = new Color[size * size];
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float n = (float)(rnd.NextDouble() - 0.5) * 2f * amount;
					// a few long fibres
					if (rnd.Next(90) == 0) n -= amount * 1.5f;
					px[y * size + x] = new Color(Mathf.Clamp01(baseColor.r + n), Mathf.Clamp01(baseColor.g + n), Mathf.Clamp01(baseColor.b + n * 1.2f), baseColor.a);
				}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		private static Texture2D MakeShadow(int size)
		{
			var t = NewTex(size, size);
			var px = new Color[size * size];
			float edge = size * 0.42f;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - edge));
					float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - edge));
					float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;
					float a = Mathf.Clamp01(1f - d);
					px[y * size + x] = new Color(0, 0, 0, a * a);
				}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		private static Texture2D MakeDisk(int size, bool ring)
		{
			var t = NewTex(size, size);
			var px = new Color[size * size];
			float r = size / 2f - 1f;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float d = Mathf.Sqrt((x + 0.5f - size / 2f) * (x + 0.5f - size / 2f) + (y + 0.5f - size / 2f) * (y + 0.5f - size / 2f));
					float a = Mathf.Clamp01(r - d + 0.5f);
					if (ring) a *= Mathf.Clamp01(d - (r - size * 0.12f) + 0.5f);
					px[y * size + x] = new Color(1, 1, 1, a);
				}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		// Torn bottom edge of a receipt: a row of small teeth, tiled horizontally.
		private static Texture2D MakeZigzag(int w, int h)
		{
			var t = NewTex(w, h);
			t.wrapMode = TextureWrapMode.Repeat;
			var px = new Color[w * h];
			for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
				{
					float tooth = Mathf.Abs((x % 8) - 4f) / 4f;   // 0 at the tip, 1 at the valley
					float limit = h * (1f - tooth);               // filled from the top down to this depth
					float fromTop = h - 1 - y;
					px[y * w + x] = fromTop < limit ? Receipt : new Color(0, 0, 0, 0);
				}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		// ---- Fonts ----
		internal static Font TypeFont, HandFont, ReceiptFont;

		private static void LoadFonts()
		{
			TypeFont = GameFont(new[] { "typewriter", "special", "elite", "courier", "mono" }) ?? Os("Courier New", "Lucida Console", "Consolas");
			HandFont = Os("Ink Free", "Segoe Print", "Comic Sans MS", "Segoe Script") ?? TypeFont;
			ReceiptFont = Os("Courier New", "Lucida Console", "Consolas") ?? TypeFont;
		}

		private static Font Os(params string[] names)
		{
			try
			{
				string[] installed = Font.GetOSInstalledFontNames();
				var have = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
				foreach (string n in names)
					if (have.Contains(n)) return Font.CreateDynamicFontFromOSFont(n, 32);
			}
			catch (Exception) { }
			return null;
		}

		// A legacy Font shipped with the game whose name suggests a typewriter face (IMGUI can't use TMP fonts).
		private static Font GameFont(string[] hints)
		{
			try
			{
				foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
				{
					if (f == null || string.IsNullOrEmpty(f.name)) continue;
					string n = f.name.ToLowerInvariant();
					foreach (string h in hints) if (n.Contains(h)) { DiegeticPlugin.Log.LogInfo("Using the game's font '" + f.name + "' for typed text."); return f; }
				}
			}
			catch (Exception) { }
			return null;
		}

		private static readonly Dictionary<long, GUIStyle> styles = new Dictionary<long, GUIStyle>();

		internal static GUIStyle Text(Font font, int size, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
		{
			long key = ((long)(font != null ? font.GetInstanceID() : 0) << 20) ^ (size << 8) ^ ((int)anchor << 2) ^ (bold ? 1 : 0) ^ (wrap ? 2 : 0);
			GUIStyle s;
			if (styles.TryGetValue(key, out s) && s != null) return s;
			s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = anchor, wordWrap = wrap, richText = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
			if (bold) s.fontStyle = FontStyle.Bold;
			styles[key] = s;
			return s;
		}

		// ---- Drawing ----
		internal static float Alpha = 1f;   // multiplies every colour (fade in/out, opacity setting)

		internal static void Rect(Rect r, Color c)
		{
			Color old = GUI.color;
			c.a *= Alpha;
			GUI.color = c;
			GUI.DrawTexture(r, White);
			GUI.color = old;
		}

		internal static void Tex(Rect r, Texture t, Color tint)
		{
			Color old = GUI.color;
			tint.a *= Alpha;
			GUI.color = tint;
			GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
			GUI.color = old;
		}

		internal static void Tiled(Rect r, Texture2D t, Color tint, float scale = 1f)
		{
			Color old = GUI.color;
			tint.a *= Alpha;
			GUI.color = tint;
			GUI.DrawTextureWithTexCoords(r, t, new Rect(0, 0, r.width / (t.width * scale), r.height / (t.height * scale)), true);
			GUI.color = old;
		}

		internal static void ShadowFor(Rect r, float spread = 12f, float dy = 6f, float strength = 0.55f)
		{
			Tex(new Rect(r.x - spread, r.y - spread + dy, r.width + spread * 2f, r.height + spread * 2f), Shadow, new Color(0, 0, 0, strength));
		}

		internal static void PaperSheet(Rect r, bool shadow = true)
		{
			if (shadow) ShadowFor(r);
			Tiled(r, PaperTex, Color.white);
			// a soft darker rim, like the edge of old paper
			Color rim = new Color(0.47f, 0.35f, 0.16f, 0.10f);
			Rect(new Rect(r.x, r.y, r.width, 3f), rim);
			Rect(new Rect(r.x, r.yMax - 3f, r.width, 3f), rim);
			Rect(new Rect(r.x, r.y, 3f, r.height), rim);
			Rect(new Rect(r.xMax - 3f, r.y, 3f, r.height), rim);
		}

		internal static void TapeStrip(Rect r) { Tiled(r, TapeTex, Color.white); }

		internal static void Outline(Rect r, float t, Color c)
		{
			Rect(new Rect(r.x, r.y, r.width, t), c);
			Rect(new Rect(r.x, r.yMax - t, r.width, t), c);
			Rect(new Rect(r.x, r.y, t, r.height), c);
			Rect(new Rect(r.xMax - t, r.y, t, r.height), c);
		}

		internal static void Label(Rect r, string text, GUIStyle style, Color c)
		{
			if (string.IsNullOrEmpty(text)) return;
			Color old = style.normal.textColor;
			c.a *= Alpha;
			style.normal.textColor = c;
			GUI.Label(r, text, style);
			style.normal.textColor = old;
		}

		internal static Vector2 Measure(string text, GUIStyle style) { return style.CalcSize(new GUIContent(text)); }

		internal static void Circle(Vector2 center, float radius, Color c) { Tex(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Disk, c); }

		// A thick line from a to b (rotated rectangle).
		internal static void Line(Vector2 a, Vector2 b, float width, Color c)
		{
			Vector2 d = b - a;
			float len = d.magnitude;
			if (len < 0.01f) return;
			float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
			Matrix4x4 m = GUI.matrix;
			GUI.matrix = m * RotationAbout(ang, a);
			Rect(new Rect(a.x, a.y - width / 2f, len, width), c);
			GUI.matrix = m;
		}

		// Semicircle gauge arc from the left (0) to the right (1), centred on c.
		internal static void Arc(Vector2 c, float radius, float width, float from, float to, Color col)
		{
			int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) * 36f));
			for (int i = 0; i < steps; i++)
			{
				float t0 = Mathf.Lerp(from, to, i / (float)steps), t1 = Mathf.Lerp(from, to, (i + 1) / (float)steps);
				Vector2 p0 = c + Polar(t0, radius), p1 = c + Polar(t1, radius);
				Line(p0, p1 + (p1 - p0).normalized * 1.2f, width, col);
			}
		}

		internal static Vector2 Polar(float t, float r)
		{
			float a = Mathf.PI * (1f - t);   // t=0 left, t=1 right, over the top
			return new Vector2(Mathf.Cos(a) * r, -Mathf.Sin(a) * r);
		}

		// A rotation about a point in the HUD's own (virtual, unscaled) coordinates. GUIUtility.RotateAroundPivot
		// takes its pivot in screen pixels, which lands in the wrong place once the HUD is scaled.
		internal static Matrix4x4 RotationAbout(float degrees, Vector2 pivot)
		{
			return Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f), Quaternion.Euler(0f, 0f, degrees), Vector3.one) * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0f), Quaternion.identity, Vector3.one);
		}

		// Rotate everything drawn inside the using block.
		internal struct Turn : IDisposable
		{
			private readonly Matrix4x4 saved;
			internal Turn(float degrees, Vector2 pivot) { saved = GUI.matrix; if (Mathf.Abs(degrees) > 0.01f) GUI.matrix = saved * RotationAbout(degrees, pivot); }
			public void Dispose() { GUI.matrix = saved; }
		}

		// ---- Seven-segment LCD digits ----
		private static readonly int[] Segs = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

		// Draws "HH:MM" (or dashes for -1) in an LCD style. Returns the width used.
		internal static float LcdTime(Vector2 at, float h, int hours, int minutes, Color on, Color ghost)
		{
			float w = h * 0.52f, gap = h * 0.16f, x = at.x;
			string s = hours < 0 ? "--:--" : (hours.ToString("00") + ":" + minutes.ToString("00"));
			foreach (char ch in s)
			{
				if (ch == ':')
				{
					float d = h * 0.11f;
					if (hours >= 0)
					{
						Rect(new Rect(x + gap * 0.2f, at.y + h * 0.3f, d, d), on);
						Rect(new Rect(x + gap * 0.2f, at.y + h * 0.66f, d, d), on);
					}
					x += d + gap * 0.9f;
					continue;
				}
				int mask = ch == '-' ? 0x40 : Segs[ch - '0'];
				Digit(new Rect(x, at.y, w, h), 0x7F, ghost);
				Digit(new Rect(x, at.y, w, h), mask, on);
				x += w + gap;
			}
			return x - at.x - gap;
		}

		private static void Digit(Rect r, int mask, Color c)
		{
			float t = r.height * 0.11f, hh = r.height / 2f;
			if ((mask & 0x01) != 0) Rect(new Rect(r.x + t, r.y, r.width - 2 * t, t), c);                      // a top
			if ((mask & 0x02) != 0) Rect(new Rect(r.xMax - t, r.y + t, t, hh - 1.5f * t), c);                  // b
			if ((mask & 0x04) != 0) Rect(new Rect(r.xMax - t, r.y + hh + 0.5f * t, t, hh - 1.5f * t), c);     // c
			if ((mask & 0x08) != 0) Rect(new Rect(r.x + t, r.yMax - t, r.width - 2 * t, t), c);                // d bottom
			if ((mask & 0x10) != 0) Rect(new Rect(r.x, r.y + hh + 0.5f * t, t, hh - 1.5f * t), c);            // e
			if ((mask & 0x20) != 0) Rect(new Rect(r.x, r.y + t, t, hh - 1.5f * t), c);                         // f
			if ((mask & 0x40) != 0) Rect(new Rect(r.x + t, r.y + hh - t / 2f, r.width - 2 * t, t), c);         // g middle
		}

		// ---- Sprites from the game (item icons) ----
		internal static void Sprite(Rect r, Sprite sp, Color tint)
		{
			if (sp == null || sp.texture == null) return;
			Rect tr = sp.textureRect;
			var uv = new Rect(tr.x / sp.texture.width, tr.y / sp.texture.height, tr.width / sp.texture.width, tr.height / sp.texture.height);
			// keep the aspect ratio inside r
			float aspect = tr.width / Mathf.Max(1f, tr.height);
			Rect fit = r;
			if (aspect > r.width / r.height) { float h = r.width / aspect; fit = new Rect(r.x, r.y + (r.height - h) / 2f, r.width, h); }
			else { float w = r.height * aspect; fit = new Rect(r.x + (r.width - w) / 2f, r.y, w, r.height); }
			Color old = GUI.color;
			tint.a *= Alpha;
			GUI.color = tint;
			GUI.DrawTextureWithTexCoords(fit, sp.texture, uv, true);
			GUI.color = old;
		}
	}
}
