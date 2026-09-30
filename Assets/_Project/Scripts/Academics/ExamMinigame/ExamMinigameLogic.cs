using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NisitSimulator.Academics.ExamMinigame
{
    // ===== ตรรกะมินิเกมสอบ (C# ล้วน ไม่ผูกฉาก — ทดสอบด้วย EditMode test ได้) =====
    //   • สร้างรอบสอบ (สุ่มข้อไม่ซ้ำ + สุ่มลำดับตัวเลือก แล้ว "จำ" ไว้ใน state)
    //   • ตรวจคำตอบ 4 แบบ · คำใบ้ · ส่งข้อสอบ (ทำซ้ำได้โดยผลไม่เปลี่ยน)
    public static class ExamMinigameLogic
    {
        public const string OrderingRule =
            "วิธีให้คะแนน: บล็อกที่อยู่ถูกตำแหน่งได้คะแนนตามสัดส่วน (เรียงถูกทั้งหมด = เต็ม) · ถ้ามีหลายลำดับที่ถูก ระบบเลือกลำดับที่ให้คะแนนสูงสุด";
        public const string MatchingRule = "วิธีให้คะแนน: ได้คะแนนตามจำนวนคู่ที่จับถูก";
        public const string SingleRule = "วิธีให้คะแนน: ตอบถูกได้เต็ม ตอบผิด/ไม่ตอบได้ 0";

        public static string TypeName(ExamQuestionType t)
        {
            switch (t)
            {
                case ExamQuestionType.MultipleChoice: return "เลือกคำตอบ";
                case ExamQuestionType.Ordering: return "เรียงลำดับ";
                case ExamQuestionType.Matching: return "จับคู่";
                default: return "หาจุดผิด";
            }
        }

        public static string RuleText(ExamQuestionType t)
        {
            switch (t)
            {
                case ExamQuestionType.Ordering: return OrderingRule;
                case ExamQuestionType.Matching: return MatchingRule;
                default: return SingleRule;
            }
        }

        // สิทธิ์คำใบ้จากความรู้ของ "วิชาที่สอบ" (0–100) — <40 ไม่มี · 40–69 = 1 · 70–100 = 2 (ค่าเริ่มต้น)
        public static int HintAllowance(float knowledge100, ExamBankDatabase db)
        {
            float one = db != null ? db.oneHintFrom : 40f, two = db != null ? db.twoHintsFrom : 70f;
            if (knowledge100 + 1e-4f >= two) return 2;
            if (knowledge100 + 1e-4f >= one) return 1;
            return 0;
        }

        // "1,0,2" → [1,0,2] (ต้องเป็นการเรียงสับเปลี่ยนของ 0..n-1) · ผิดรูปแบบคืน null
        public static int[] ParseOrder(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(',');
            if (parts.Length != n) return null;
            var res = new int[n];
            var seen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out int v) || v < 0 || v >= n || seen[v]) return null;
                seen[v] = true; res[i] = v;
            }
            return res;
        }

        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        static List<int> Range(int n) { var l = new List<int>(n); for (int i = 0; i < n; i++) l.Add(i); return l; }

        static bool IsIdentity(List<int> l) { for (int i = 0; i < l.Count; i++) if (l[i] != i) return false; return true; }

        // ---------- สร้างรอบสอบ ----------
        public static ExamSessionState CreateSession(ExamBankDatabase db, CourseExamBank bank, System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            var s = new ExamSessionState
            {
                courseCode = bank.courseCode,
                bankVersion = bank.version,
                timeLimit = db.TimeFor(bank),
            };
            s.remainingSeconds = s.timeLimit;

            // สุ่มข้อจากคลังโดยไม่ซ้ำ (รหัสข้อซ้ำในคลังจะถูกข้าม)
            var pool = new List<ExamQuestion>();
            var seen = new HashSet<string>();
            foreach (var q in bank.questions) if (q != null && !string.IsNullOrEmpty(q.id) && seen.Add(q.id)) pool.Add(q);
            Shuffle(pool, rng);
            int n = Mathf.Min(db.QuestionsFor(bank), pool.Count);
            for (int i = 0; i < n; i++) s.questions.Add(NewQuestionState(pool[i], rng));
            return s;
        }

        static ExamQuestionState NewQuestionState(ExamQuestion q, System.Random rng)
        {
            var st = new ExamQuestionState { questionId = q.id, type = (int)q.type, maxPoints = q.points };
            int n = q.items != null ? q.items.Count : 0;
            st.displayOrder = Range(n);
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice:
                    Shuffle(st.displayOrder, rng);              // สลับตัวเลือก — คำตอบยังเก็บเป็น index เดิม
                    break;
                case ExamQuestionType.FindError:
                    break;                                       // บรรทัดโค้ด/ขั้นตอนต้องคงลำดับเดิม ไม่สลับ
                case ExamQuestionType.Ordering:
                    for (int k = 0; k < 12; k++) { Shuffle(st.displayOrder, rng); if (n < 2 || !IsIdentity(st.displayOrder)) break; }
                    break;
                case ExamQuestionType.Matching:
                    for (int k = 0; k < 12; k++) { Shuffle(st.displayOrder, rng); if (n < 2 || !IsIdentity(st.displayOrder)) break; }
                    for (int i = 0; i < n; i++) st.match.Add(-1);
                    break;
            }
            return st;
        }

        // ---------- สถานะคำตอบ ----------
        public static bool IsAnswered(ExamQuestionState st)
        {
            if (st == null) return false;
            switch ((ExamQuestionType)st.type)
            {
                case ExamQuestionType.MultipleChoice:
                case ExamQuestionType.FindError: return st.selected >= 0;
                case ExamQuestionType.Ordering: return st.order.Count > 0;
                case ExamQuestionType.Matching: foreach (var m in st.match) if (m >= 0) return true; return false;
            }
            return false;
        }

        public static int AnsweredCount(ExamSessionState s)
        {
            int c = 0; foreach (var q in s.questions) if (IsAnswered(q)) c++; return c;
        }

        // ---------- คำตอบ (เรียกจาก UI) — ไม่ทำอะไรถ้าส่งแล้ว ----------
        public static void Select(ExamSessionState s, ExamQuestionState st, int originalIndex)
        {
            if (s == null || s.submitted || st == null) return;
            if (st.eliminated.Contains(originalIndex)) return;
            st.selected = originalIndex;
        }

        public static void AppendOrder(ExamSessionState s, ExamQuestionState st, int block)
        {
            if (s == null || s.submitted || st == null || st.order.Contains(block)) return;
            st.order.Add(block);
        }

        public static void RemoveOrderAt(ExamSessionState s, ExamQuestionState st, int pos)
        {
            if (s == null || s.submitted || st == null || pos < 0 || pos >= st.order.Count) return;
            st.order.RemoveAt(pos);
        }

        public static void MoveOrderUp(ExamSessionState s, ExamQuestionState st, int pos)
        {
            if (s == null || s.submitted || st == null || pos <= 0 || pos >= st.order.Count) return;
            int t = st.order[pos - 1]; st.order[pos - 1] = st.order[pos]; st.order[pos] = t;
        }

        public static void ClearOrder(ExamSessionState s, ExamQuestionState st)
        {
            if (s == null || s.submitted || st == null) return;
            st.order.Clear();
        }

        // จับคู่: ซ้าย left ↔ ขวา right (index เดิม) · ขวาที่ถูกใช้แล้วจะย้ายมาคู่นี้ (คู่ละหนึ่ง) · right = -1 = ยกเลิกคู่
        public static void SetMatch(ExamSessionState s, ExamQuestionState st, int left, int right)
        {
            if (s == null || s.submitted || st == null || left < 0 || left >= st.match.Count) return;
            if (right >= 0) for (int i = 0; i < st.match.Count; i++) if (st.match[i] == right) st.match[i] = -1;
            st.match[left] = right;
        }

        // ---------- ตรวจคำตอบ ----------
        public static float Grade(ExamQuestion q, ExamQuestionState st)
        {
            if (q == null || st == null) return 0f;
            float pts = st.maxPoints > 0f ? st.maxPoints : q.points;
            int n = q.items != null ? q.items.Count : 0;
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice:
                case ExamQuestionType.FindError:
                    return st.selected >= 0 && st.selected == q.correctIndex ? pts : 0f;

                case ExamQuestionType.Ordering:
                {
                    if (n == 0 || st.order.Count == 0) return 0f;
                    int best = 0;
                    foreach (var valid in ValidOrders(q))
                    {
                        int hit = 0;
                        for (int i = 0; i < st.order.Count && i < valid.Length; i++) if (st.order[i] == valid[i]) hit++;
                        if (hit > best) best = hit;
                    }
                    return pts * best / n;
                }

                case ExamQuestionType.Matching:
                {
                    if (n == 0) return 0f;
                    int hit = 0;
                    for (int i = 0; i < st.match.Count && i < n; i++) if (st.match[i] == i) hit++;
                    return pts * hit / n;
                }
            }
            return 0f;
        }

        public static List<int[]> ValidOrders(ExamQuestion q)
        {
            int n = q.items != null ? q.items.Count : 0;
            var list = new List<int[]> { Range(n).ToArray() };
            if (q.alternateOrders != null)
                foreach (var a in q.alternateOrders) { var o = ParseOrder(a, n); if (o != null) list.Add(o); }
            return list;
        }

        // ส่งข้อสอบ — ตรวจทุกข้อ · เรียกซ้ำได้ ผลเดิม (ไม่ตรวจใหม่)
        public static void Submit(ExamSessionState s, CourseExamBank bank)
        {
            if (s == null || s.submitted) return;
            float earned = 0f, max = 0f;
            foreach (var st in s.questions)
            {
                var q = bank != null ? bank.Find(st.questionId) : null;
                if (q == null || (int)q.type != st.type) { st.missingInBank = true; st.earned = 0f; continue; }
                st.earned = Grade(q, st);
                earned += st.earned;
                max += st.maxPoints > 0f ? st.maxPoints : q.points;
            }
            s.earned = earned;
            s.maxPoints = max;
            s.score100 = max > 0f ? Mathf.RoundToInt(100f * earned / max) : 0;
            s.submitted = true;
        }

        public static float QuizFraction(ExamSessionState s) => s != null && s.maxPoints > 0f ? Mathf.Clamp01(s.earned / s.maxPoints) : 0f;

        // ---------- คำใบ้ ----------
        //   คืน false พร้อมเหตุผลถ้าใช้ไม่ได้ · ใช้แล้วไม่หักคะแนน · ข้อละครั้ง (กดซ้ำไม่เสียสิทธิ์)
        public static bool CanUseHint(ExamSessionState s, ExamQuestionState st, out string reason)
        {
            reason = null;
            if (s == null || st == null || s.submitted) { reason = "ส่งข้อสอบแล้ว"; return false; }
            if (st.hintUsed) { reason = "ใช้คำใบ้ข้อนี้ไปแล้ว (ไม่เสียสิทธิ์ซ้ำ)"; return false; }
            if (s.hintsAllowed <= 0) { reason = "ความรู้วิชานี้ยังไม่ถึง 40 — ไม่มีสิทธิ์ใช้คำใบ้"; return false; }
            if (s.hintsUsed >= s.hintsAllowed) { reason = "ใช้คำใบ้ครบสิทธิ์แล้ว"; return false; }
            return true;
        }

        public static bool UseHint(ExamSessionState s, ExamQuestionState st, ExamQuestion q, System.Random rng, out string reason)
        {
            if (!CanUseHint(s, st, out reason)) return false;
            if (q == null) { reason = "ไม่พบข้อนี้ในคลัง"; return false; }
            if (rng == null) rng = new System.Random();
            int n = q.items != null ? q.items.Count : 0;
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice:
                {
                    // ตัดตัวเลือกผิด 1 ตัว (เหลืออย่างน้อย 2 ตัวเลือก → ไม่เฉลยทั้งหมด)
                    var wrong = new List<int>();
                    for (int i = 0; i < n; i++) if (i != q.correctIndex && !st.eliminated.Contains(i)) wrong.Add(i);
                    if (wrong.Count >= 2) st.eliminated.Add(wrong[rng.Next(wrong.Count)]);
                    if (st.eliminated.Contains(st.selected)) st.selected = -1;
                    break;
                }
                case ExamQuestionType.FindError:
                {
                    // ตัดบรรทัดที่ถูกต้องออกประมาณครึ่งหนึ่ง (เหลือให้เลือกอย่างน้อย 2 บรรทัด)
                    var ok = new List<int>();
                    for (int i = 0; i < n; i++) if (i != q.correctIndex && !st.eliminated.Contains(i)) ok.Add(i);
                    Shuffle(ok, rng);
                    int remove = Mathf.Min(ok.Count - 1, Mathf.Max(1, n / 2));
                    for (int i = 0; i < remove; i++) st.eliminated.Add(ok[i]);
                    if (st.eliminated.Contains(st.selected)) st.selected = -1;
                    break;
                }
                case ExamQuestionType.Ordering:
                    st.revealed = 0;   // บอกว่าบล็อกไหนมาก่อน (ไม่บอกลำดับที่เหลือ)
                    break;
                case ExamQuestionType.Matching:
                {
                    // เปิดคู่ถูก 1 คู่ (ฝั่งซ้ายที่ยังจับไม่ถูก)
                    var cand = new List<int>();
                    for (int i = 0; i < n; i++) if (i >= st.match.Count || st.match[i] != i) cand.Add(i);
                    if (cand.Count > 0 && n >= 2) st.revealed = cand[rng.Next(cand.Count)];
                    break;
                }
            }
            st.hintUsed = true;
            s.hintsUsed++;
            reason = null;
            return true;
        }

        public static string HintText(ExamQuestion q, ExamQuestionState st)
        {
            if (q == null || st == null || !st.hintUsed) return "";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(q.hint)) sb.Append(q.hint);
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice:
                    if (st.eliminated.Count > 0) sb.Append(sb.Length > 0 ? "\n" : "").Append("(ตัดตัวเลือกที่ผิดออกให้ 1 ตัวแล้ว)");
                    break;
                case ExamQuestionType.FindError:
                    if (st.eliminated.Count > 0) sb.Append(sb.Length > 0 ? "\n" : "").Append($"(ตัดส่วนที่ถูกต้องออกให้ {st.eliminated.Count} รายการแล้ว)");
                    break;
                case ExamQuestionType.Ordering:
                    if (q.items != null && q.items.Count > 0)
                        sb.Append(sb.Length > 0 ? "\n" : "").Append("บล็อกแรกคือ: ").Append(FirstLine(q.items[0]));
                    break;
                case ExamQuestionType.Matching:
                    if (st.revealed >= 0 && st.revealed < q.items.Count)
                        sb.Append(sb.Length > 0 ? "\n" : "").Append($"คู่ที่ถูก 1 คู่: \"{q.items[st.revealed]}\" คู่กับ \"{q.matches[st.revealed]}\"");
                    break;
            }
            return sb.ToString();
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i) + " ...";
        }

        // ---------- ข้อความเฉลย/คำตอบ (หลังส่งเท่านั้น) ----------
        public static string PlayerAnswerText(ExamQuestion q, ExamQuestionState st)
        {
            if (q == null || st == null) return "-";
            if (!IsAnswered(st)) return "(ไม่ได้ตอบ)";
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice: return Item(q, st.selected);
                case ExamQuestionType.FindError: return $"บรรทัด/ส่วนที่ {st.selected + 1}: {Item(q, st.selected)}";
                case ExamQuestionType.Ordering:
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < st.order.Count; i++) sb.Append(i + 1).Append(". ").Append(Item(q, st.order[i])).Append('\n');
                    if (st.order.Count < q.items.Count) sb.Append($"(วางไว้ {st.order.Count}/{q.items.Count} บล็อก)");
                    return sb.ToString().TrimEnd('\n');
                }
                case ExamQuestionType.Matching:
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < q.items.Count; i++)
                    {
                        int m = i < st.match.Count ? st.match[i] : -1;
                        sb.Append(q.items[i]).Append("  =  ").Append(m >= 0 && m < q.matches.Count ? q.matches[m] : "-").Append(m == i ? "   [ถูก]" : m >= 0 ? "   [ผิด]" : "").Append('\n');
                    }
                    return sb.ToString().TrimEnd('\n');
                }
            }
            return "-";
        }

        public static string CorrectAnswerText(ExamQuestion q)
        {
            if (q == null) return "-";
            switch (q.type)
            {
                case ExamQuestionType.MultipleChoice: return Item(q, q.correctIndex);
                case ExamQuestionType.FindError: return $"บรรทัด/ส่วนที่ {q.correctIndex + 1}: {Item(q, q.correctIndex)}";
                case ExamQuestionType.Ordering:
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < q.items.Count; i++) sb.Append(i + 1).Append(". ").Append(q.items[i]).Append('\n');
                    if (q.alternateOrders != null && q.alternateOrders.Count > 0) sb.Append("(มีลำดับอื่นที่ถูกด้วย)");
                    return sb.ToString().TrimEnd('\n');
                }
                case ExamQuestionType.Matching:
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < q.items.Count; i++) sb.Append(q.items[i]).Append("  =  ").Append(q.matches[i]).Append('\n');
                    return sb.ToString().TrimEnd('\n');
                }
            }
            return "-";
        }

        static string Item(ExamQuestion q, int i) => q.items != null && i >= 0 && i < q.items.Count ? q.items[i] : "-";

        // รูปแบบข้อสอบในคลังของวิชา (ไว้โชว์ตอนแสดงรายละเอียดก่อนเริ่ม)
        public static string FormatsOf(CourseExamBank bank)
        {
            var set = new List<string>();
            if (bank != null)
                foreach (var q in bank.questions)
                    if (q != null) { var n = TypeName(q.type); if (!set.Contains(n)) set.Add(n); }
            return set.Count > 0 ? string.Join(" · ", set) : "-";
        }
    }
}
