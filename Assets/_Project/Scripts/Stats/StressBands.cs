using UnityEngine;

namespace NisitSimulator.Stats
{
    public enum StressBand { Calm = 0, Tense = 1, Stressed = 2, Severe = 3 }

    // ===== ช่วงความเครียด 4 ช่วง + ค่าปรับสมดุลทั้งหมดของระบบความเครียด (ที่เดียว) =====
    //   สบาย 0–30 ×1.00 · ตึงตัว 30–55 ×1.05 (กดดันนิดหน่อย = ตั้งใจขึ้น) · เครียด 55–80 ×0.90 · เครียดจัด 80–100 ×0.80 → ×0.50
    //   ตรรกะล้วน (ไม่แตะฉาก) → ทดสอบด้วย EditMode ได้
    public static class StressBands
    {
        public const float TenseFrom = 30f;
        public const float StressedFrom = 55f;
        public const float SevereFrom = 80f;
        public const float Hysteresis = 3f;            // ลงจากช่วงต้องต่ำกว่าเส้นเกินนี้ (กันแจ้งเตือนรัวตอนค่าแกว่งที่เส้น)

        public const float TenseMult = 1.05f;
        public const float StressedMult = 0.90f;
        public const float SevereStartMult = 0.80f;

        // นอนไม่หลับ
        public const float InsomniaAbove = 70f;
        public const float InsomniaEnergyFraction = 0.75f;   // พลังงานฟื้นได้ถึง 75% ของค่าสูงสุด
        public const float InsomniaStressChange = -15f;      // แทนค่าปกติของเตียง (-25)
        public const float SleepStressChange = -25f;         // ค่าใหม่ของเตียง (เดิม -35)

        // ทางคลายเครียด
        public const float BenchPerHour = -2f;
        public const float CafeteriaMeal = -2f;
        public const float TalkFriend = -3f;
        public const float TalkCloseFriend = -6f;
        public const int CloseFriendLevel = 3;               // RelationshipManager: 3 = เพื่อนสนิท
        public const float FreeDayRecoveryMult = 1.5f;

        // ปรับสมดุล
        public const float ClassPerHour = 4f;                // เดิม +3
        public const float NaturalFallPerMinute = 0.006f;    // เดิม 0.01

        public static StressBand BandOf(float stress)
        {
            if (stress >= SevereFrom) return StressBand.Severe;
            if (stress >= StressedFrom) return StressBand.Stressed;
            if (stress >= TenseFrom) return StressBand.Tense;
            return StressBand.Calm;
        }

        static float LowerEdge(StressBand b)
        {
            switch (b)
            {
                case StressBand.Severe: return SevereFrom;
                case StressBand.Stressed: return StressedFrom;
                case StressBand.Tense: return TenseFrom;
                default: return 0f;
            }
        }

        // ช่วงที่แสดงผล (มี hysteresis): ขึ้นทันทีที่ถึงเส้น · ลงเมื่อต่ำกว่าเส้นของช่วงเดิมเกิน Hysteresis
        public static StressBand BandWithHysteresis(StressBand previous, float stress)
        {
            var raw = BandOf(stress);
            if (raw >= previous) return raw;
            if (stress >= LowerEdge(previous) - Hysteresis) return previous;
            // ลงหลายช่วงพร้อมกัน (เช่น ตื่นนอน) → ใช้ช่วงจริง
            return raw;
        }

        // ตัวคูณความรู้ตามความเครียด
        public static float KnowledgeMult(float stress, float maxStress = 100f, float minAtMax = 0.5f)
        {
            switch (BandOf(stress))
            {
                case StressBand.Calm: return 1f;
                case StressBand.Tense: return TenseMult;
                case StressBand.Stressed: return StressedMult;
                default:
                    float t = Mathf.InverseLerp(SevereFrom, Mathf.Max(SevereFrom + 1f, maxStress), stress);
                    return Mathf.Lerp(SevereStartMult, Mathf.Min(SevereStartMult, minAtMax), t);
            }
        }

        public static string NameOf(StressBand b)
        {
            switch (b)
            {
                case StressBand.Tense: return "ตึงตัว";
                case StressBand.Stressed: return "เครียด";
                case StressBand.Severe: return "เครียดจัด";
                default: return "สบาย";
            }
        }

        public static Color ColorOf(StressBand b)
        {
            switch (b)
            {
                case StressBand.Tense: return new Color(0.96f, 0.80f, 0.25f);
                case StressBand.Stressed: return new Color(0.95f, 0.55f, 0.20f);
                case StressBand.Severe: return new Color(0.88f, 0.27f, 0.30f);
                default: return new Color(0.40f, 0.80f, 0.50f);
            }
        }

        // ข้อความผลต่อการเรียน เช่น "ความรู้ +5%" / "ความรู้ -10%"
        public static string EffectText(float stress)
        {
            int pct = Mathf.RoundToInt((KnowledgeMult(stress) - 1f) * 100f);
            if (pct == 0) return "เรียนได้เต็มที่";
            return pct > 0 ? $"ความรู้ +{pct}%" : $"ความรู้ {pct}%";
        }

        // ข้อความแจ้งเตือนเมื่อเปลี่ยนช่วง
        public static string CrossingMessage(StressBand from, StressBand to, float stress)
        {
            if (to > from)
            {
                switch (to)
                {
                    case StressBand.Tense: return "เริ่มตึงตัว — ตั้งใจเรียนขึ้นนิดหน่อย (ความรู้ +5%)";
                    case StressBand.Stressed: return "เริ่มเครียดแล้ว — ความรู้ที่ได้ลดลง 10% · ลองนอน/คุยกับเพื่อน/นั่งพัก";
                    case StressBand.Severe:
                        return $"เครียดจัด! — ความรู้ลดลง {Mathf.RoundToInt((1f - KnowledgeMult(stress)) * 100f)}% · เกิน {InsomniaAbove:0} จะนอนไม่หลับ";
                }
            }
            switch (to)
            {
                case StressBand.Calm: return "ผ่อนคลายลงแล้ว — กลับมาสบายใจ";
                case StressBand.Tense: return "ความเครียดลดลงแล้ว — ตึงตัวกำลังดี";
                default: return $"ความเครียดลดลง — ตอนนี้ \"{NameOf(to)}\"";
            }
        }
    }
}
