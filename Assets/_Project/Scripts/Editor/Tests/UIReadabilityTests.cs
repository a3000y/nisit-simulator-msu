#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.UI;

namespace NisitSimulator.Tests
{
    // EditMode tests: ตัวช่วยกันตัวอักษรล้น (UIFit) · ชื่อแอป MSG REG · ข้อความที่ผู้เล่นเห็นไม่มีอักษรที่ฟอนต์ไม่มี
    public class UIReadabilityTests
    {
        [Test]
        public void ClampWidth_PaddingAndLimits()
        {
            Assert.AreEqual(420f, UIFit.ClampWidth(100f, 30f, 420f, 1000f), 1e-3f, "สั้น → กว้างขั้นต่ำ");
            Assert.AreEqual(660f, UIFit.ClampWidth(600f, 30f, 420f, 1000f), 1e-3f, "ข้อความ + ขอบซ้ายขวา");
            Assert.AreEqual(1000f, UIFit.ClampWidth(2000f, 30f, 420f, 1000f), 1e-3f, "ยาว → ไม่เกินกว้างสุด");
        }

        [Test]
        public void LineCount_Wraps()
        {
            Assert.AreEqual(1, UIFit.LineCount(300f, 940f));
            Assert.AreEqual(1, UIFit.LineCount(940f, 940f));
            Assert.AreEqual(2, UIFit.LineCount(1200f, 940f));
            Assert.AreEqual(3, UIFit.LineCount(2000f, 940f));
            Assert.AreEqual(1, UIFit.LineCount(500f, 0f));
        }

        [Test]
        public void Ellipsize_ShortUnchanged_LongCut()
        {
            Assert.AreEqual("ภาษาไทย", UIFit.Ellipsize("ภาษาไทย", 10));
            string s = UIFit.Ellipsize("การออกแบบและพัฒนาระบบความปลอดภัยสารสนเทศ", 12);
            Assert.IsTrue(s.EndsWith("…"));
            Assert.LessOrEqual(s.Length, 12);
            Assert.AreEqual("", UIFit.Ellipsize(null, 5));
        }

        [Test]
        public void Ellipsize_NeverStartsEllipsisAfterCut_OnThaiCombiningMark()
        {
            // "ที่" = ท + ี + ่ → ตัดตรงกลางต้องถอยไปหน้าพยัญชนะ ไม่ให้สระ/วรรณยุกต์ค้างลอย
            const string src = "ที่ที่ที่ที่ที่";
            for (int max = 2; max < src.Length; max++)
            {
                string s = UIFit.Ellipsize(src, max);
                Assert.IsTrue(s.EndsWith("…"));
                int cut = s.Length - 1;   // ตัวแรกที่ถูกตัดทิ้ง
                Assert.IsFalse(UIFit.IsThaiCombining(src[cut]), $"max={max}: ตัดกลางพยางค์ → {s}");
                Assert.IsTrue(src.StartsWith(s.Substring(0, cut)));
            }
        }

        [Test]
        public void ThaiCombining_Detection()
        {
            Assert.IsTrue(UIFit.IsThaiCombining('่'));   // ่
            Assert.IsTrue(UIFit.IsThaiCombining('ี'));   // ี
            Assert.IsTrue(UIFit.IsThaiCombining('ั'));   // ั
            Assert.IsFalse(UIFit.IsThaiCombining('ก'));
            Assert.IsFalse(UIFit.IsThaiCombining('า'));
        }

        [Test]
        public void AppName_IsMsgReg()
        {
            Assert.AreEqual("MSG REG", RegistrationUI.AppName);
        }

        [Test]
        public void TermTable_LongTitles_UseWiderColumn()
        {
            var r = new TermReport();
            r.rows.Add(new TermReportRow { code = "CS303", title = "การพัฒนาแอปพลิเคชันบนอุปกรณ์เคลื่อนที่ขั้นสูงและการออกแบบส่วนต่อประสาน", credits = 3, letter = "B", passed = true, attendance = 1f });
            r.rows.Add(new TermReportRow { code = "GE101", title = "ภาษาไทยเพื่อการสื่อสาร", credits = 3, letter = "A", passed = true, attendance = 1f });
            string t = TermResultUI.TableText(r);
            StringAssert.Contains("ภาษาไทยเพื่อการสื่อสาร", t, "ชื่อสั้นไม่ถูกตัด");
            StringAssert.Contains("การพัฒนาแอปพลิเคชันบนอุปกรณ์เคลื่อนที่", t, "ชื่อยาวแสดงได้มากกว่าเดิม (เดิมตัดที่ 25 ตัว)");
            StringAssert.Contains("…", t);
        }

        [Test]
        public void LegacyGrades_ShowsGpaWithoutExamController()
        {
            StringAssert.Contains("GPA", RegistrationUI.LegacyGradesText());
        }

        // ข้อความในโค้ดเกม (ไม่นับ Editor/DevTools/Tests/คอมเมนต์/Debug.Log) ต้องไม่มี ▸ — ฟอนต์ไทยของเกมไม่มีอักษรนี้ (แสดงเป็นช่องว่าง)
        [Test]
        public void VisibleStrings_HaveNoMissingGlyphs()
        {
            var bad = new List<string>();
            string root = "Assets/_Project/Scripts";
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string n = f.Replace('\\', '/');
                if (n.Contains("/Editor/") || n.Contains("/DevTools/") || n.Contains("/Tests/")) continue;
                var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i].Trim();
                    if (!l.Contains("▸") || l.StartsWith("//") || l.Contains("Debug.Log")) continue;
                    int cm = l.IndexOf("//", System.StringComparison.Ordinal);
                    int ar = l.IndexOf('▸');
                    if (cm >= 0 && cm < ar && l.LastIndexOf('"', ar) < cm) continue;   // อยู่ในคอมเมนต์ท้ายบรรทัด
                    bad.Add(n + ":" + (i + 1));
                }
            }
            Assert.IsEmpty(bad, "พบ ▸ ในข้อความที่ผู้เล่นเห็น: " + string.Join(", ", bad));
        }
    }
}
#endif
