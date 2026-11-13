using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiegeticUIMod
{
	/// <summary>Generated textures, fonts and the small drawing toolkit the HUD is built from.</summary>
	internal static class Art
	{
		// ---- Palette (sampled from the game's own menus and world) ----
		internal static readonly Color Paper = new Color32(187, 185, 176, 255);        // drawing-sheet grey, from the game's Tenement page
		internal static readonly Color PaperDark = new Color32(166, 164, 156, 255);
		internal static readonly Color Ink = new Color32(52, 52, 49, 255);              // the drawing's ink (lines and type)
		internal static readonly Color InkSoft = new Color32(70, 70, 66, 255);
		internal static readonly Color InkFaint = new Color32(98, 97, 92, 255);
		internal static readonly Color Rule = new Color32(69, 68, 63, 255);              // the drawing's cell lines
		internal static readonly Color Cream = new Color32(236, 235, 230, 255);         // light text on cardboard
		internal static readonly Color CreamSoft = new Color32(222, 219, 210, 255);
		internal static readonly Color Red = new Color32(129, 28, 28, 255);             // skill-bar blood red
		internal static readonly Color StampRed = new Color32(129, 28, 28, 255);
		internal static readonly Color Oxblood = new Color32(89, 40, 42, 255);          // the game's Sort button
		internal static readonly Color Green = new Color32(115, 130, 35, 255);          // skill-bar olive
		internal static readonly Color Olive = new Color32(115, 130, 35, 255);
		internal static readonly Color Mustard = new Color32(128, 118, 29, 255);
		internal static readonly Color Rust = new Color32(132, 86, 29, 255);            // skill-bar amber
		internal static readonly Color Amber = new Color32(140, 94, 20, 255);           // the game's active tab
		internal static readonly Color AmberDark = new Color32(111, 74, 13, 255);
		internal static readonly Color AmberLight = new Color32(200, 138, 30, 255);
		internal static readonly Color Blue = new Color32(19, 73, 110, 255);            // the game's steel blue
		internal static readonly Color Brown = new Color32(132, 86, 29, 255);
		internal static readonly Color Board = new Color32(102, 85, 58, 255);           // cardboard
		internal static readonly Color Tape = new Color32(143, 141, 134, 245);          // duct tape
		internal static readonly Color Receipt = new Color32(187, 185, 176, 255);
		internal static readonly Color Lcd = new Color32(154, 166, 107, 255);
		internal static readonly Color LcdOff = new Color32(124, 135, 80, 255);
		internal static readonly Color LcdInk = new Color32(29, 36, 16, 255);
		internal static readonly Color Steel = new Color32(53, 52, 49, 255);
		internal static readonly Color SteelLight = new Color32(78, 77, 73, 255);
		internal static readonly Color Strap = new Color32(28, 27, 25, 255);
		internal static readonly Color Well = new Color32(43, 43, 42, 255);             // the dark slot a meter sits in
		internal static readonly Color Stain = new Color32(110, 80, 40, 255);

		// Reserve colour, the game's own skill-bar colours: olive when fine, amber when getting low, red when critical.
		internal static Color Reserve(float v)
		{
			if (v >= 75f) return Olive;
			if (v >= 60f) return Color.Lerp(Mustard, Olive, (v - 60f) / 15f);
			if (v >= 40f) return Color.Lerp(Rust, Mustard, (v - 40f) / 20f);
			if (v >= 20f) return Color.Lerp(Red, Rust, (v - 20f) / 20f);
			return Red;
		}

		// ---- Textures ----
		internal static Texture2D White, PaperTex, ReceiptTex, TapeTex, BoardTex, WearTex, Octagon, Shadow, Disk, Ring, Zigzag;

		internal static void Init()
		{
			if (White != null) return;
			White = Solid(Color.white);
			PaperTex = Mottled(256, Paper, 7);
			WearTex = Specks(256, 13);
			Octagon = MakeOctagon(64);
			ReceiptTex = Noise(64, Receipt, 0.025f, 11);
			TapeTex = Stripes(64, Tape);
			BoardTex = Noise(128, Board, 0.05f, 5);
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

		// Drawing paper: large soft blotches plus fine grain.
		private static Texture2D Mottled(int size, Color baseColor, int seed)
		{
			var t = NewTex(size, size);
			t.wrapMode = TextureWrapMode.Repeat;
			var rnd = new System.Random(seed);
			var px = new Color[size * size];
			float ox = rnd.Next(1000), oy = rnd.Next(1000);
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float blot = (Mathf.PerlinNoise(ox + x * 0.035f, oy + y * 0.035f) - 0.5f) * 0.07f;
					float grain = (float)(rnd.NextDouble() - 0.5) * 0.035f;
					float n = blot + grain;
					px[y * size + x] = new Color(Mathf.Clamp01(baseColor.r + n), Mathf.Clamp01(baseColor.g + n), Mathf.Clamp01(baseColor.b + n * 0.9f), 1f);
				}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		// Wear laid over a printed form: small paper-coloured specks and scuffs that break up lines and type.
		private static Texture2D Specks(int size, int seed)
		{
			var t = NewTex(size, size);
			t.wrapMode = TextureWrapMode.Repeat;
			var rnd = new System.Random(seed);
			var px = new Color[size * size];
			Color clear = new Color(Paper.r, Paper.g, Paper.b, 0f);
			for (int i = 0; i < px.Length; i++) px[i] = clear;
			for (int k = 0; k < 140; k++)
			{
				int cx = rnd.Next(size), cy = rnd.Next(size);
				int len = rnd.Next(1, 5), thick = rnd.Next(1, 3);
				float a = 0.55f + (float)rnd.NextDouble() * 0.4f;
				Color c = new Color(Mathf.Clamp01(Paper.r + 0.06f), Mathf.Clamp01(Paper.g + 0.06f), Mathf.Clamp01(Paper.b + 0.06f), a);
				for (int dx = 0; dx < len; dx++)
					for (int dy = 0; dy < thick; dy++)
						px[((cy + dy) % size) * size + (cx + dx) % size] = c;
			}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		// The game's octagonal badge shape (its Tenement icons).
		private static Texture2D MakeOctagon(int size)
		{
			var t = NewTex(size, size);
			var px = new Color[size * size];
			float r = size / 2f - 1f;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float dx = Mathf.Abs(x + 0.5f - size / 2f), dy = Mathf.Abs(y + 0.5f - size / 2f);
					float d = Mathf.Max(Mathf.Max(dx, dy), (dx + dy) / 1.41421f);
					px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
				}
			t.SetPixels(px);
			t.Apply();
			return t;
		}

		// Duct tape: grey with fine lengthwise threads.
		private static Texture2D Stripes(int size, Color baseColor)
		{
			var t = NewTex(size, size);
			t.wrapMode = TextureWrapMode.Repeat;
			var rnd = new System.Random(3);
			var px = new Color[size * size];
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
				{
					float n = (x % 5 < 2 ? 0.035f : 0f) + (float)(rnd.NextDouble() - 0.5) * 0.04f;
					px[y * size + x] = new Color(Mathf.Clamp01(baseColor.r + n), Mathf.Clamp01(baseColor.g + n), Mathf.Clamp01(baseColor.b + n), baseColor.a);
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

		private static float nextFontTry;
		private static bool gameFontFound;

		private static void LoadFonts()
		{
			TypeFont = Os("Lucida Sans Typewriter", "Courier New", "Lucida Console", "Consolas");
			HandFont = TypeFont;
			ReceiptFont = TypeFont;
			RefreshFonts();
		}

		// The game's menus are set in a typewriter face (a TextMeshPro font). When its source font is loaded, the
		// HUD uses it too, so both read as one; until then a typewriter-like system font stands in.
		internal static void RefreshFonts()
		{
			if (gameFontFound || Time.unscaledTime < nextFontTry) return;
			nextFontTry = Time.unscaledTime + 5f;
			Font f = GameTypewriter();
			if (f == null) return;
			gameFontFound = true;
			TypeFont = HandFont = ReceiptFont = f;
			DiegeticPlugin.Log.LogInfo("Using the game's menu font '" + f.name + "'.");
		}

		private static Font GameTypewriter()
		{
			try
			{
				UIPageButtons ui = UIPageButtons.instance;
				if (ui == null) return null;
				foreach (TMPro.TextMeshProUGUI t in ui.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
				{
					if (t == null || t.font == null) continue;
					var prop = typeof(TMPro.TMP_FontAsset).GetProperty("sourceFontFile");
					Font src = prop != null ? prop.GetValue(t.font, null) as Font : null;
					if (src != null) return src;
					// a static font asset keeps no source font in a build: look for a loaded font of the same name
					string want = t.font.name.ToLowerInvariant().Replace(" sdf", "").Replace("sdf", "").Trim();
					foreach (Font lf in Resources.FindObjectsOfTypeAll<Font>())
						if (lf != null && !string.IsNullOrEmpty(lf.name) && lf.name.ToLowerInvariant().Contains(want)) return lf;
					if (!loggedFont) { loggedFont = true; DiegeticPlugin.Log.LogInfo("The game's menu font '" + t.font.name + "' has no loadable source font; using a system typewriter font."); }
					gameFontFound = true;   // nothing more to find
					return null;
				}
			}
			catch (Exception) { }
			return null;
		}
		private static bool loggedFont;

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
			Tiled(r, PaperTex, Color.white, 1f);
		}

		// A printed form, like the game's Tenement drawing: grey sheet, a ruled border inset from the edge.
		internal static void Form(Rect r, bool shadow = true)
		{
			PaperSheet(r, shadow);
			Outline(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, r.height - 12f), 2f, Rule);
		}

		// The inside of a form (inside its border).
		internal static Rect Inner(Rect r) { return new Rect(r.x + 8f, r.y + 8f, r.width - 16f, r.height - 16f); }

		internal static void HRule(float x, float y, float w) { Rect(new Rect(x, y - 1f, w, 2f), Rule); }
		internal static void VRule(float x, float y, float h) { Rect(new Rect(x - 1f, y, 2f, h), Rule); }

		// Wear over a finished form: specks and scuffs over its lines and type.
		internal static void Wear(Rect r) { Tiled(r, WearTex, Color.white, 1f); }

		internal static void Badge(Vector2 c, float radius, Color fill)
		{
			Tex(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), Octagon, Rule);
			Tex(new Rect(c.x - radius + 3f, c.y - radius + 3f, radius * 2f - 6f, radius * 2f - 6f), Octagon, fill);
		}

		internal static void TapeStrip(Rect r) { Tiled(r, TapeTex, Color.white); }

		// A piece of cardboard: stained, with a faint top edge.
		internal static void Cardboard(Rect r, bool shadow = true)
		{
			if (shadow) ShadowFor(r, 14f, 8f, 0.6f);
			Tiled(r, BoardTex, Color.white);
			Tex(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.45f, r.height * 0.7f, r.height * 0.7f), Disk, new Color(0, 0, 0, 0.12f));
			Tex(new Rect(r.x + r.width * 0.72f, r.y - r.height * 0.1f, r.height * 0.55f, r.height * 0.55f), Disk, new Color(0, 0, 0, 0.09f));
			Rect(new Rect(r.x, r.y, r.width, 1f), new Color(1f, 1f, 1f, 0.1f));
			Rect(new Rect(r.x, r.yMax - 2f, r.width, 2f), new Color(0f, 0f, 0f, 0.2f));
		}

		// A strip of duct tape across a corner.
		internal static void DuctCorner(Vector2 corner, bool left)
		{
			Rect strip = new Rect(corner.x - 26f, corner.y - 9f, 52f, 18f);
			using (new Turn(left ? -35f : 35f, corner)) Tiled(strip, TapeTex, Color.white);
		}

		internal static void CoffeeRing(Vector2 c, float radius)
		{
			Tex(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), Ring, new Color(Stain.r, Stain.g, Stain.b, 0.22f));
		}

		// A small coloured square, the status mark used in labels.
		internal static void Mark(Rect r, Color c) { Rect(r, c); }

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
