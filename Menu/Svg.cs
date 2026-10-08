using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Nova.Menu
{
    public static class Svg
    {
        private struct Line
        {
            public Vector2 a;
            public Vector2 b;
            public float width;
            public float alpha;
        }

        public static Texture2D Parse(string source, int size = 64)
        {
            Color32[] pixels = Rasterize(source, size);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SVG icon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        internal static Color32[] Rasterize(string source, int size)
        {
            if (size < 8 || size > 512)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (string.IsNullOrWhiteSpace(source) || source.Length > 262144)
                throw new FormatException("Invalid SVG size.");

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            XElement root;
            using (var text = new StringReader(source))
            using (var reader = XmlReader.Create(text, settings))
                root = XElement.Load(reader);
            if (root.Name.LocalName != "svg")
                throw new FormatException("Expected an SVG element.");

            float[] view = Numbers((string)root.Attribute("viewBox") ?? "0 0 24 24");
            if (view.Length != 4 || view[2] <= 0 || view[3] <= 0)
                throw new FormatException("Invalid SVG viewBox.");

            var lines = new List<Line>();
            Read(root, lines, 2, 1, 0);
            float scale = size / Math.Max(view[2], view[3]);
            var offset = new Vector2((size - view[2] * scale) / 2 - view[0] * scale,
                (size - view[3] * scale) / 2 - view[1] * scale);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 0);

            foreach (Line line in lines)
            {
                if (line.width <= 0 || line.alpha <= 0) continue;
                Vector2 a = line.a * scale + offset;
                Vector2 b = line.b * scale + offset;
                Vector2 delta = b - a;
                float length = delta.sqrMagnitude;
                float radius = line.width * scale / 2;
                int left = Math.Max(0, (int)Math.Floor(Math.Min(a.x, b.x) - radius - 1));
                int right = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(a.x, b.x) + radius + 1));
                int top = Math.Max(0, (int)Math.Floor(Math.Min(a.y, b.y) - radius - 1));
                int bottom = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(a.y, b.y) + radius + 1));
                for (int y = top; y <= bottom; y++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        float t = length > 0 ? Clamp(Vector2.Dot(point - a, delta) / length) : 0;
                        float distance = (point - a - delta * t).magnitude;
                        byte alpha = (byte)(255 * Clamp(radius + 0.5f - distance) * line.alpha);
                        int index = (size - 1 - y) * size + x;
                        if (alpha > pixels[index].a)
                            pixels[index].a = alpha;
                    }
                }
            }
            return pixels;
        }

        private static void Read(XElement element, List<Line> lines, float width, float alpha, int depth)
        {
            if (depth > 16)
                throw new FormatException("SVG nesting limit reached.");
            if (element.Attribute("transform") != null)
                throw new FormatException("SVG transforms are not supported.");
            width = Number(element, "stroke-width", width);
            alpha *= Clamp(Number(element, "opacity", 1)) * Clamp(Number(element, "stroke-opacity", 1));
            if ((string)element.Attribute("stroke") == "none")
                alpha = 0;
            string name = element.Name.LocalName;
            if (name == "svg" || name == "g")
            {
                foreach (XElement child in element.Elements())
                    Read(child, lines, width, alpha, depth + 1);
                return;
            }
            if (name == "title" || name == "desc" || name == "metadata")
                return;

            var path = new Path(lines, width, alpha);
            switch (name)
            {
                case "path":
                    path.Read((string)element.Attribute("d") ?? "");
                    break;
                case "line":
                    path.Move(new Vector2(Number(element, "x1"), Number(element, "y1")));
                    path.To(new Vector2(Number(element, "x2"), Number(element, "y2")));
                    break;
                case "polyline":
                case "polygon":
                    float[] points = Numbers((string)element.Attribute("points") ?? "");
                    if (points.Length % 2 != 0)
                        throw new FormatException("Invalid SVG points.");
                    for (int i = 0; i < points.Length; i += 2)
                    {
                        var point = new Vector2(points[i], points[i + 1]);
                        if (i == 0) path.Move(point);
                        else path.To(point);
                    }
                    if (name == "polygon" && points.Length > 0)
                        path.Close();
                    break;
                case "circle":
                case "ellipse":
                    float cx = Number(element, "cx");
                    float cy = Number(element, "cy");
                    float rx = name == "circle" ? Number(element, "r") : Number(element, "rx");
                    float ry = name == "circle" ? rx : Number(element, "ry");
                    if (rx <= 0 || ry <= 0) break;
                    path.Move(new Vector2(cx + rx, cy));
                    path.Arc(rx, ry, 0, false, true, new Vector2(cx - rx, cy));
                    path.Arc(rx, ry, 0, false, true, new Vector2(cx + rx, cy));
                    path.Close();
                    break;
                case "rect":
                    float x = Number(element, "x");
                    float y = Number(element, "y");
                    float w = Number(element, "width");
                    float h = Number(element, "height");
                    if (w <= 0 || h <= 0) break;
                    float cornerX = Math.Max(0, Number(element, "rx", Number(element, "ry")));
                    float cornerY = Math.Min(h / 2, Math.Max(0, Number(element, "ry", cornerX)));
                    cornerX = Math.Min(w / 2, cornerX);
                    path.Move(new Vector2(x + cornerX, y));
                    path.To(new Vector2(x + w - cornerX, y));
                    path.Arc(cornerX, cornerY, 0, false, true, new Vector2(x + w, y + cornerY));
                    path.To(new Vector2(x + w, y + h - cornerY));
                    path.Arc(cornerX, cornerY, 0, false, true, new Vector2(x + w - cornerX, y + h));
                    path.To(new Vector2(x + cornerX, y + h));
                    path.Arc(cornerX, cornerY, 0, false, true, new Vector2(x, y + h - cornerY));
                    path.To(new Vector2(x, y + cornerY));
                    path.Arc(cornerX, cornerY, 0, false, true, new Vector2(x + cornerX, y));
                    path.Close();
                    break;
                default:
                    throw new FormatException("Unsupported SVG element: " + name);
            }
        }

        private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));

        private static float Number(XElement element, string name, float fallback = 0)
        {
            string value = (string)element.Attribute(name);
            if (value == null) return fallback;
            var reader = new NumbersReader(value);
            float result = reader.Number();
            if (!reader.End) throw new FormatException("Invalid SVG attribute: " + name);
            return result;
        }

        private static float[] Numbers(string source)
        {
            var reader = new NumbersReader(source);
            var values = new List<float>();
            while (!reader.End) values.Add(reader.Number());
            return values.ToArray();
        }

        private sealed class NumbersReader
        {
            private readonly string source;
            private int index;

            public NumbersReader(string source) => this.source = source;

            private void Skip()
            {
                while (index < source.Length && (char.IsWhiteSpace(source[index]) || source[index] == ',')) index++;
            }

            public bool End { get { Skip(); return index >= source.Length; } }
            public bool IsCommand { get { Skip(); return !End && char.IsLetter(source[index]); } }
            public char Command() { Skip(); return source[index++]; }

            public bool Flag()
            {
                Skip();
                if (End || (source[index] != '0' && source[index] != '1'))
                    throw new FormatException("Invalid SVG arc flag.");
                return source[index++] == '1';
            }

            public float Number()
            {
                Skip();
                int start = index;
                if (index < source.Length && (source[index] == '+' || source[index] == '-')) index++;
                while (index < source.Length && char.IsDigit(source[index])) index++;
                if (index < source.Length && source[index] == '.')
                {
                    index++;
                    while (index < source.Length && char.IsDigit(source[index])) index++;
                }
                if (index < source.Length && (source[index] == 'e' || source[index] == 'E'))
                {
                    index++;
                    if (index < source.Length && (source[index] == '+' || source[index] == '-')) index++;
                    while (index < source.Length && char.IsDigit(source[index])) index++;
                }
                if (!float.TryParse(source.Substring(start, index - start), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 100000)
                    throw new FormatException("Invalid SVG number.");
                return value;
            }

            public Vector2 Point() => new Vector2(Number(), Number());
        }

        private sealed class Path
        {
            private readonly List<Line> lines;
            private readonly float width;
            private readonly float alpha;
            private Vector2 point;
            private Vector2 start;
            private Vector2 control;
            private char previous;

            public Path(List<Line> lines, float width, float alpha)
            {
                this.lines = lines;
                this.width = Math.Max(0, width);
                this.alpha = alpha;
            }

            public void Move(Vector2 next) { point = next; start = next; }
            public void Close() => To(start);

            public void To(Vector2 next)
            {
                if (lines.Count >= 32768) throw new FormatException("SVG segment limit reached.");
                lines.Add(new Line { a = point, b = next, width = width, alpha = alpha });
                point = next;
            }

            private void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int depth = 0)
            {
                float length = (d - a).magnitude;
                float distance = length == 0 ? Math.Max((b - a).magnitude, (c - a).magnitude) :
                    Math.Max(Math.Abs((b.x - a.x) * (d.y - a.y) - (b.y - a.y) * (d.x - a.x)),
                        Math.Abs((c.x - a.x) * (d.y - a.y) - (c.y - a.y) * (d.x - a.x))) / length;
                float excess = (b - a).magnitude + (c - b).magnitude + (d - c).magnitude - length;
                if ((distance < 0.025f && excess < 0.025f) || depth >= 10)
                {
                    To(d);
                    return;
                }
                Vector2 ab = (a + b) / 2;
                Vector2 bc = (b + c) / 2;
                Vector2 cd = (c + d) / 2;
                Vector2 abc = (ab + bc) / 2;
                Vector2 bcd = (bc + cd) / 2;
                Vector2 mid = (abc + bcd) / 2;
                Curve(a, ab, abc, mid, depth + 1);
                Curve(mid, bcd, cd, d, depth + 1);
            }

            public void Arc(float radiusX, float radiusY, float rotation, bool large, bool sweep, Vector2 end)
            {
                if (point == end) return;
                double rx = Math.Abs(radiusX);
                double ry = Math.Abs(radiusY);
                if (rx < 0.00001 || ry < 0.00001) { To(end); return; }
                double angle = rotation * Math.PI / 180;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                double dx = (point.x - end.x) / 2.0;
                double dy = (point.y - end.y) / 2.0;
                double x = cos * dx + sin * dy;
                double y = -sin * dx + cos * dy;
                double fit = x * x / (rx * rx) + y * y / (ry * ry);
                if (fit > 1) { rx *= Math.Sqrt(fit); ry *= Math.Sqrt(fit); }
                double denominator = rx * rx * y * y + ry * ry * x * x;
                double factor = denominator == 0 ? 0 : Math.Sqrt(Math.Max(0,
                    (rx * rx * ry * ry - denominator) / denominator));
                if (large == sweep) factor = -factor;
                double cx = factor * rx * y / ry;
                double cy = -factor * ry * x / rx;
                double centerX = cos * cx - sin * cy + (point.x + end.x) / 2.0;
                double centerY = sin * cx + cos * cy + (point.y + end.y) / 2.0;
                double from = Math.Atan2((y - cy) / ry, (x - cx) / rx);
                double to = Math.Atan2((-y - cy) / ry, (-x - cx) / rx);
                double span = to - from;
                if (sweep && span < 0) span += Math.PI * 2;
                if (!sweep && span > 0) span -= Math.PI * 2;
                double step = 2 * Math.Acos(Math.Max(-1, 1 - 0.025 / Math.Max(rx, ry)));
                int count = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(Math.Abs(span) / Math.Max(0.001, step))));
                for (int i = 1; i <= count; i++)
                {
                    double t = from + span * i / count;
                    To(i == count ? end : new Vector2((float)(centerX + cos * rx * Math.Cos(t) - sin * ry * Math.Sin(t)),
                        (float)(centerY + sin * rx * Math.Cos(t) + cos * ry * Math.Sin(t))));
                }
            }

            public void Read(string source)
            {
                var reader = new NumbersReader(source);
                char command = '\0';
                bool moved = false;
                while (!reader.End)
                {
                    if (reader.IsCommand) command = reader.Command();
                    else if (command == '\0') throw new FormatException("Expected an SVG path command.");
                    bool relative = char.IsLower(command);
                    char kind = char.ToUpperInvariant(command);
                    if (!moved && kind != 'M') throw new FormatException("SVG paths must start with M.");
                    Vector2 origin = relative ? point : Vector2.zero;
                    Vector2 next;
                    Vector2 first;
                    Vector2 second;
                    switch (kind)
                    {
                        case 'M':
                            Move(reader.Point() + origin);
                            moved = true;
                            command = relative ? 'l' : 'L';
                            break;
                        case 'L': To(reader.Point() + origin); break;
                        case 'H': To(new Vector2(reader.Number() + origin.x, point.y)); break;
                        case 'V': To(new Vector2(point.x, reader.Number() + origin.y)); break;
                        case 'C':
                            first = reader.Point() + origin;
                            second = reader.Point() + origin;
                            next = reader.Point() + origin;
                            Curve(point, first, second, next);
                            control = second;
                            break;
                        case 'S':
                            first = previous == 'C' || previous == 'S' ? point * 2 - control : point;
                            second = reader.Point() + origin;
                            next = reader.Point() + origin;
                            Curve(point, first, second, next);
                            control = second;
                            break;
                        case 'Q':
                        case 'T':
                            first = kind == 'Q' ? reader.Point() + origin :
                                previous == 'Q' || previous == 'T' ? point * 2 - control : point;
                            next = reader.Point() + origin;
                            Curve(point, point + (first - point) * (2f / 3), next + (first - next) * (2f / 3), next);
                            control = first;
                            break;
                        case 'A':
                            float rx = reader.Number();
                            float ry = reader.Number();
                            float rotation = reader.Number();
                            bool large = reader.Flag();
                            bool sweep = reader.Flag();
                            Arc(rx, ry, rotation, large, sweep, reader.Point() + origin);
                            break;
                        case 'Z': Close(); command = '\0'; break;
                        default: throw new FormatException("Unsupported SVG path command: " + command);
                    }
                    previous = kind;
                }
            }
        }
    }
}
