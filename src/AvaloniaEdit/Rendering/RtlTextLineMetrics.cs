// [vizar-rtl] See PATCHES.md.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AvaloniaEdit.Rendering
{
	/// <summary>
	/// [vizar-rtl] Character geometry of a <see cref="TextLine"/> laid out right to left, built from its runs (visual
	/// order) and glyph clusters. Avalonia 12's own hit-testing (<c>GetDistanceFromCharacterHit</c>,
	/// <c>GetCharacterHitFromDistance</c>, <c>GetTextBounds</c>) is wrong or throws for many RTL paragraphs, so the
	/// RTL mode of the text view uses this instead. Coordinates are TextLine layout space (x = 0 is the left edge).
	/// </summary>
	public sealed class RtlTextLineMetrics
	{
		private static readonly ConditionalWeakTable<TextLine, RtlTextLineMetrics> Cache = new();

		private readonly int _first;
		private readonly double[] _left;
		private readonly double[] _right;
		private readonly bool[] _rtl;
		private readonly double _endX;

		private RtlTextLineMetrics(TextLine line)
		{
			_first = line.FirstTextSourceIndex;
			var runs = new List<(TextRun Run, int Level, double X, double Width)>();
			double x = line.Start;
			foreach (var run in line.TextRuns)
			{
				double width = run is DrawableTextRun d ? d.Size.Width : 0;
				if (run is TextEndOfLine)
					continue;
				int level = run is ShapedTextRun s ? s.BidiLevel : 1;
				runs.Add((run, level, x, width));
				x += width;
			}

			// Undo the UBA L2 reordering (visual -> logical): reverse maximal sequences with level >= k, k ascending.
			var order = new List<int>();
			for (int i = 0; i < runs.Count; i++)
				order.Add(i);
			int max = 0, minOdd = int.MaxValue;
			foreach (var r in runs)
			{
				max = Math.Max(max, r.Level);
				if ((r.Level & 1) == 1)
					minOdd = Math.Min(minOdd, r.Level);
			}
			for (int k = minOdd; k <= max; k++)
			{
				int i = 0;
				while (i < order.Count)
				{
					if (runs[order[i]].Level < k) { i++; continue; }
					int j = i;
					while (j < order.Count && runs[order[j]].Level >= k)
						j++;
					order.Reverse(i, j - i);
					i = j;
				}
			}

			int length = 0;
			foreach (var r in runs)
				length += r.Run.Length;
			_left = new double[length];
			_right = new double[length];
			_rtl = new bool[length];
			_endX = line.Start;

			int offset = 0;
			foreach (int index in order)
			{
				var (run, level, rx, width) = runs[index];
				bool rtl = (level & 1) == 1;
				if (run is ShapedTextRun shaped && shaped.ShapedBuffer.Length > 0)
					FillShaped(shaped, offset, rx, rtl);
				else
					for (int c = 0; c < run.Length; c++)
						Set(offset + c, rx, rx + (c == 0 ? width : 0), rtl);
				offset += run.Length;
			}
		}

		private void Set(int i, double left, double right, bool rtl)
		{
			_left[i] = Math.Min(left, right);
			_right[i] = Math.Max(left, right);
			_rtl[i] = rtl;
		}

		private void FillShaped(ShapedTextRun run, int offset, double x, bool rtl)
		{
			var buffer = run.ShapedBuffer;
			int n = run.Length;
			var minX = new double[n];
			var maxX = new double[n];
			var has = new bool[n];
			foreach (var g in buffer)
			{
				int c = Math.Clamp(g.GlyphCluster, 0, n - 1);
				if (!has[c]) { minX[c] = x; maxX[c] = x + g.GlyphAdvance; has[c] = true; }
				else { minX[c] = Math.Min(minX[c], x); maxX[c] = Math.Max(maxX[c], x + g.GlyphAdvance); }
				x += g.GlyphAdvance;
			}
			// Spread each cluster over its characters (ligatures), in reading order.
			int start = 0;
			while (start < n)
			{
				int end = start + 1;
				while (end < n && !has[end])
					end++;
				double l = has[start] ? minX[start] : x, r = has[start] ? maxX[start] : x;
				int count = end - start;
				for (int k = 0; k < count; k++)
				{
					double a = l + (r - l) * k / count, b = l + (r - l) * (k + 1) / count;
					if (rtl)
						Set(offset + start + k, r - (b - l), r - (a - l), true);
					else
						Set(offset + start + k, a, b, false);
				}
				start = end;
			}
		}

		/// <summary>Gets (and caches) the metrics of a line.</summary>
		public static RtlTextLineMetrics Get(TextLine line)
		{
			if (line == null)
				throw new ArgumentNullException(nameof(line));
			return Cache.GetValue(line, l => new RtlTextLineMetrics(l));
		}

		/// <summary>The x of a caret before the character (its leading edge), or of the trailing edge.</summary>
		public double GetDistanceFromCharacterHit(CharacterHit hit)
		{
			int i = hit.FirstCharacterIndex - _first;
			if (hit.TrailingLength > 0)
				i++;
			if (_left.Length == 0)
				return _endX;
			if (i <= 0)
				return Leading(0);
			if (i >= _left.Length)
				return Trailing(_left.Length - 1);
			return Leading(i);
		}

		private double Trailing(int i) => _rtl[i] ? _left[i] : _right[i];

		private double Leading(int i) => _rtl[i] ? _right[i] : _left[i];

		/// <summary>The character hit at x (leading or trailing half).</summary>
		public CharacterHit GetCharacterHitFromDistance(double x)
		{
			int best = -1;
			double bestDistance = double.MaxValue;
			for (int i = 0; i < _left.Length; i++)
			{
				if (_right[i] <= _left[i])
					continue;
				double d = x < _left[i] ? _left[i] - x : x > _right[i] ? x - _right[i] : 0;
				if (d < bestDistance) { bestDistance = d; best = i; }
			}
			if (best < 0)
				return new CharacterHit(_first, 0);
			double mid = (_left[best] + _right[best]) / 2;
			bool trailing = _rtl[best] ? x < mid : x >= mid;
			return new CharacterHit(_first + best, trailing ? 1 : 0);
		}

		/// <summary>Rectangles covering the characters [firstIndex, firstIndex + length).</summary>
		public IReadOnlyList<Rect> GetTextBounds(int firstIndex, int length, double height)
		{
			var spans = new List<(double L, double R)>();
			int from = Math.Max(0, firstIndex - _first), to = Math.Min(_left.Length, firstIndex - _first + length);
			for (int i = from; i < to; i++)
				spans.Add((_left[i], _right[i]));
			spans.Sort((a, b) => a.L.CompareTo(b.L));
			var rects = new List<Rect>();
			foreach (var (l, r) in spans)
			{
				if (rects.Count > 0 && l <= rects[^1].Right + 0.01)
					rects[^1] = new Rect(rects[^1].Left, 0, Math.Max(rects[^1].Right, r) - rects[^1].Left, height);
				else
					rects.Add(new Rect(l, 0, r - l, height));
			}
			return rects;
		}
	}
}
