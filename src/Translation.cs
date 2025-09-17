using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MelonLoader;

namespace LWitWMod
{
    internal sealed class Translation
    {
        // ===== ЛОГИ =====
        public static bool LogMissesToConsole = false;
        private readonly HashSet<string> _sessionMisses = new(StringComparer.Ordinal);
        private readonly HashSet<string> _missFileCache = new(StringComparer.Ordinal);

        // ===== ХРАНИЛКИ =====
        private readonly Dictionary<string, string> _byText = new(StringComparer.Ordinal);
        private readonly List<(Regex rx, string value)> _patternsStrict  = new();
        private readonly List<(Regex rx, string value)> _patternsNumeric = new();

        // ===== РЕГЕКСЫ =====
        private static readonly Regex PlaceholderRx     = new(@"(\{\{[^}]+\}\}|\(\{[^}]+\}\)|\*[^*]+\*|\{\$[^}]+\})", RegexOptions.Compiled);
        private static readonly Regex ReDashAllToMinus  = new("[‐-‒–—−]", RegexOptions.Compiled);
        private static readonly Regex ReTrailingDash    = new(@"\s*[-]+\s*$", RegexOptions.Compiled);
        private static readonly Regex ReSpaces          = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ReTrailingBr      = new(@"(?:<br\s*/?>)+\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ReNoParse         = new(@"</?noparse>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string _path;
        private readonly string _missLog;

        public int Count => _byText.Count + _patternsStrict.Count + _patternsNumeric.Count;

        public Translation(string path)
        {
            _path = path;
            _missLog = Path.Combine(Path.GetDirectoryName(_path) ?? ".", "misses.txt");

            try
            {
                if (File.Exists(_missLog))
                {
                    foreach (var line in File.ReadAllLines(_missLog))
                        if (!string.IsNullOrWhiteSpace(line))
                            _missFileCache.Add(line);
                }
            }
            catch { /* ignore */ }

            LoadOrCreate();
        }

        private static string Normalize(string s, bool stripListPrefix = true)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";

            s = s.Normalize(NormalizationForm.FormC);     // NFC для диакритики
            s = ReNoParse.Replace(s, "");
            s = ReDashAllToMinus.Replace(s, "-");
            s = s.Replace('\u00A0', ' ').Replace('\u2009', ' ').Replace('\u202F', ' ');
            s = s.Replace("\u00AD", "");                  // soft hyphen
            s = ReTrailingBr.Replace(s, "");
            s = s.Trim();

            if (stripListPrefix && s.Length >= 1 && (s[0] == '-' || s[0] == '•'))
            {
                int cut = 1;
                while (cut < s.Length && char.IsWhiteSpace(s[cut])) cut++;
                s = s.Substring(cut).TrimStart();
            }

            s = ReSpaces.Replace(s, " ");
            s = ReTrailingDash.Replace(s, "").TrimEnd();
            return s;
        }

        private static bool IsNullish(string v) =>
            string.IsNullOrEmpty(v) || v.Equals("null", StringComparison.OrdinalIgnoreCase);

        private static Regex BuildRegexFromTemplate(string key)
        {
            int slot = 0;
            string tmp = key.Normalize(NormalizationForm.FormC);
            tmp = PlaceholderRx.Replace(tmp, _ => "\uE000" + (slot++) + "\uE001");

            tmp = ReDashAllToMinus.Replace(tmp, "-");
            tmp = tmp.Replace('\u00A0', ' ')
                     .Replace('\u2009', ' ')
                     .Replace('\u202F', ' ')
                     .Replace("\u00AD", "");

            tmp = Regex.Escape(tmp);
            tmp = Regex.Replace(tmp, @"\s+", @"\s+");
            tmp = Regex.Replace(tmp, @"\\\)", @"\.?\)");

            for (int i = 0; i < slot; i++)
                tmp = tmp.Replace(@"\uE000" + i + @"\uE001", "(.+?)");

            string pattern = @"^\s*(?:-\s*)?" + tmp + @"(?:\s*(?:<br\s*/?>)+)?\.?\s*$";
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private static Regex BuildRegexNumericLoose(string key)
        {
            string tmp = key.Normalize(NormalizationForm.FormC);

            tmp = ReDashAllToMinus.Replace(tmp, "-");
            tmp = tmp.Replace('\u00A0', ' ')
                     .Replace('\u2009', ' ')
                     .Replace('\u202F', ' ')
                     .Replace("\u00AD", "");

            tmp = Regex.Escape(tmp);
            tmp = Regex.Replace(tmp, @"\s+", @"\s+");
            tmp = Regex.Replace(tmp, @"\\\(\s*\d+\s*\\/\s*\d+\.?\s*\\\)", @"\((\d+/\d+\.?)\)");
            tmp = Regex.Replace(tmp, @"x\d+", @"x(\d+)");
            tmp = Regex.Replace(tmp, @"(?<!\\d)\\d+(?!\\d)", @"(\d+)");
            tmp = Regex.Replace(tmp, @"\\\)", @"\.?\)");

            string pattern = @"^\s*" + tmp + @"\s*(?:<br\s*/?>)?\s*$";
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private void LoadOrCreate()
        {
            if (!File.Exists(_path))
                File.WriteAllText(_path, "{}", Encoding.UTF8);

            try
            {
                var json = File.ReadAllText(_path, Encoding.UTF8);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (dict == null) return;

                foreach (var kv in dict)
                {
                    var key = kv.Key;
                    var val = kv.Value;
                    if (IsNullish(val)) continue;

                    bool hasPlaceholders = PlaceholderRx.IsMatch(key);
                    string keyNorm = Normalize(key);

                    if (hasPlaceholders)
                    {
                        _patternsStrict.Add((BuildRegexFromTemplate(key), val));
                    }
                    else
                    {
                        _byText[keyNorm] = val;

                        bool looksNumeric =
                            Regex.IsMatch(key, @"\(\s*\d+\s*/\s*\d+\.?\s*\)") ||
                            Regex.IsMatch(key, @"(?<!\d)\d+(?!\d)") ||
                            Regex.IsMatch(key, @"x\d+");

                        if (looksNumeric)
                            _patternsNumeric.Add((BuildRegexNumericLoose(key), val));
                    }
                }

                MelonLogger.Msg($"[Translation] Loaded: exact={_byText.Count}, strict={_patternsStrict.Count}, numeric={_patternsNumeric.Count}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[Translation] ru.json parse error: {e}");
            }
        }

        // --- Новый шаг: перевод запятых-разделённых списков с суффиксами (+N)/xN ---
        private bool TryTranslateCommaList(string srcNorm, out string result)
        {
            result = null;

            // Быстрый выход: нет запятой — нет смысла
            if (srcNorm.IndexOf(',') < 0) return false;

            var parts = srcNorm.Split(',');
            var outParts = new string[parts.Length];
            bool anyTranslated = false;

            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();

                // Срезать локальные хвосты вида "(+2)" или "(x5)" и одиночный "-"/точку
                string suffix = "";
                var mSuf = Regex.Match(p, @"\s*(\((?:\+\d+|x\d+)\))\s*[-‐–—]?\s*\.?\s*$", RegexOptions.IgnoreCase);
                if (mSuf.Success)
                {
                    suffix = mSuf.Groups[1].Value; // "(+2)" или "(x5)"
                    p = p.Substring(0, mSuf.Index).TrimEnd();
                }

                string pNorm = Normalize(p);

                if (_byText.TryGetValue(pNorm, out var translatedBase) || _byText.TryGetValue(p, out translatedBase))
                {
                    outParts[i] = string.IsNullOrEmpty(suffix) ? translatedBase : $"{translatedBase}{suffix}";
                    anyTranslated = true;
                }
                else
                {
                    outParts[i] = parts[i].Trim(); // исходник без изменений
                }
            }

            if (anyTranslated)
            {
                result = string.Join(", ", outParts);
                return true;
            }
            return false;
        }

        public bool TryTranslate(string src, out string translated)
        {
            translated = null;
            if (string.IsNullOrEmpty(src)) return false;

            // 0) Exact raw
            if (_byText.TryGetValue(src, out var rawHit))
            {
                translated = rawHit;
                return true;
            }

            string srcNorm = Normalize(src);

            // Fallback #1: "… (x5) -"
            var uiStrip = Regex.Replace(srcNorm, @"\s*\(x\d+\)\s*[-‐-–—]?\s*$", "");
            if (!ReferenceEquals(uiStrip, srcNorm) && _byText.TryGetValue(uiStrip, out var uiVal))
            {
                translated = uiVal;
                return true;
            }

            // Fallback #2: "… (+2)" (с возможной пунктуацией)
            var plusStrip = Regex.Replace(srcNorm, @"\s*\(\s*[+\-−]?\d+\s*\)\s*[-‐–—]?\s*[,\.]?\s*$", "");
            if (!ReferenceEquals(plusStrip, srcNorm) && _byText.TryGetValue(plusStrip, out var plusVal))
            {
                translated = plusVal;
                return true;
            }

            // Fallback #3: Списки "A(+2), B(+2)" → перевод каждого элемента
            if (TryTranslateCommaList(srcNorm, out var listVal))
            {
                translated = listVal;
                return true;
            }

            // 1) Exact normalized
            if (_byText.TryGetValue(srcNorm, out var v))
            {
                translated = v;
                return true;
            }

            // 2) Строгие шаблоны
            foreach (var entry in _patternsStrict)
            {
                var m = entry.rx.Match(srcNorm);
                if (!m.Success) m = entry.rx.Match(src);
                if (!m.Success) continue;

                int gi = 1;
                string result = PlaceholderRx.Replace(entry.value, _ =>
                {
                    if (gi <= m.Groups.Count - 1) return m.Groups[gi++].Value;
                    return _.Value;
                });

                translated = result;
                return true;
            }

            // 3) Облегчённые numeric-паттерны
            foreach (var entry in _patternsNumeric)
            {
                if (entry.rx.IsMatch(srcNorm) || entry.rx.IsMatch(src))
                {
                    translated = entry.value;
                    return true;
                }
            }

            // 4) Тихая фиксация промаха
            //try
            //{
            //    if (_missFileCache.Count < 10000 && _missFileCache.Add(src))
            //    {
            //        using var writer = File.AppendText(_missLog);
            //        writer.WriteLine(src);
            //    }
            //}
            //catch { /* ignore */ }

            //if (LogMissesToConsole && _sessionMisses.Add(src))
            //    MelonLogger.Msg($"[Translate] MISS: '{src}'");

            return false;
        }
    }
}
