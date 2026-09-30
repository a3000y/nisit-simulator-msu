using System;
using UnityEngine;
using NisitSimulator.Core;

namespace NisitSimulator.TimeSystem
{
    // นาฬิกาจำลองในเกม — ขับเคลื่อนตารางเรียน, การลดพลังงาน, เหตุการณ์ตามเวลา
    // 1 วันในเกม = gameMinutesPerRealSecond * ... (ปรับความเร็วได้)
    public class GameClock : MonoBehaviour
    {
        [Header("ความเร็วเวลา")]
        [Tooltip("เวลาจริง 1 วินาที = กี่นาทีในเกม (3 = หนึ่งวันตื่นถึงเข้านอนราว 5 นาทีจริง) ค่านี้คูณกับ StatDecay ด้วย สมดุลจึงไม่เพี้ยน")]
        public float gameMinutesPerRealSecond = 3f;

        [Header("เวลาเริ่มต้น")]
        public int startHour = 7;    // เริ่มเกมใหม่ 07:00 ที่หอพัก (ค่าในฉากเป็นตัวกำหนดจริง)
        public int startDay = 1;

        // เวลาในเกม (สะสมเป็นนาทีทั้งหมดตั้งแต่เริ่มวัน)
        private float totalGameMinutes;

        // event: ยิงทุกครั้งที่ "นาที" เปลี่ยน (สำหรับนาฬิกา HUD)
        public event Action<int, int> OnTimeChanged;   // (ชั่วโมง, นาที)
        // event: ยิงเมื่อขึ้นวันใหม่ (สำหรับสรุปวัน/ลดสถานะ/สุ่มเหตุการณ์)
        public event Action<int> OnDayChanged;         // (วันที่)

        // หยุดนาฬิกาของ "เครื่องนี้" ชั่วคราวโดยไม่แตะ Time.timeScale — ใช้ตอนผู้เล่นอยู่ในห้องสอบมินิเกม
        //   (ตัวจับเวลาสอบใช้ unscaled time จึงยังเดิน · เครื่องอื่นใน Multiplayer ไม่ถูกกระทบ)
        public static bool Suspended;

        // ===== Multiplayer (ตั้งโดย WorldTimeSync เท่านั้น — เล่นคนเดียวเป็น false ทั้งคู่ = พฤติกรรมเดิม) =====
        //   NetworkFollower     = เครื่องนี้เป็น client: เดินเวลาเองเพื่อความลื่น แต่ "ไม่ข้ามเที่ยงคืนเอง" · วัน/เวลาจริงมาจาก Host (ApplyAuthoritativeTime)
        //   NetworkAuthoritative = เครื่องนี้เป็น Host ที่มีผู้เล่นอื่นเชื่อมต่อ: นาฬิกาโลกเดินต่อแม้ Host อยู่ในห้องสอบ (เวลาโลกใช้ร่วมกัน หยุดให้คนเดียวไม่ได้)
        public static bool NetworkFollower;
        public static bool NetworkAuthoritative;

        private int lastMinute = -1;
        private int currentDay;

        // ตั้งค่าเริ่มต้นใน Awake ไม่ใช่ Start
        // เดิมตั้งใน Start แล้วยิง OnDayChanged ทันที ถ้า ProgressionManager.Start
        // ทำงานก่อน มันจะอ่าน clock.Day ได้ 0 แล้วนับ event นี้เป็น "วันใหม่"
        // ทำให้ DayInYear เริ่มที่ 2 ตั้งแต่เปิดเกม เท่ากับหายไปหนึ่งวันทุกปี
        void Awake()
        {
            currentDay = startDay;
            totalGameMinutes = startHour * 60f;
        }

        void Start()
        {
            OnDayChanged?.Invoke(currentDay);
        }

        void Update()
        {
            // เดินเวลาเฉพาะตอนเกมยังดำเนินอยู่ (หยุดเมื่อ pause/จบเกม)
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            if (Suspended && !NetworkAuthoritative) return;   // อยู่ในห้องสอบมินิเกม (MP: Host ไม่หยุดเวลาโลกของทุกคน)
            Step(Time.deltaTime * gameMinutesPerRealSecond);
        }

        // เดินเวลาในเกมไปข้างหน้า N นาที ด้วยขั้นตอนเดียวกับการเดินเวลาทุกเฟรม (ยิง OnTimeChanged / OnDayChanged ตามจริง)
        //   แบ่งเป็นช่วงละไม่เกิน 1 นาทีเกม → ไม่ข้ามเหตุการณ์รายนาที และข้ามวันได้ไม่เกินครั้งละหนึ่งวันต่อช่วง
        //   (ใช้โดย Dev Panel "จำลองเวลาเดินผ่าน" — ผู้เรียกต้องเช็ก IsActive/Suspended เองเหมือน Update)
        public void AdvanceMinutes(float minutes)
        {
            while (minutes > 0f)
            {
                float s = Mathf.Min(1f, minutes);
                Step(s);
                minutes -= s;
            }
        }

        void Step(float gameMinutes)
        {
            totalGameMinutes += gameMinutes;

            // ข้ามวันเมื่อครบ 24 ชม.
            if (totalGameMinutes >= 24 * 60)
            {
                if (NetworkFollower)
                {
                    // client: ค้างไว้ที่ 23:59.99 รอ Host ประกาศวันใหม่ (กันข้ามวันก่อน/ซ้ำกับ Host → ค่าขนม/สรุปวันซ้ำ)
                    totalGameMinutes = 24f * 60f - 0.01f;
                }
                else
                {
                    totalGameMinutes -= 24 * 60;
                    currentDay++;
                    OnDayChanged?.Invoke(currentDay);
                }
            }

            int hour = Hour;
            int minute = Minute;
            if (minute != lastMinute)
            {
                lastMinute = minute;
                OnTimeChanged?.Invoke(hour, minute);
            }
        }

        public int Hour => Mathf.FloorToInt(totalGameMinutes / 60f) % 24;
        public int Minute => Mathf.FloorToInt(totalGameMinutes % 60f);
        public int Day => currentDay;
        public float TotalMinutes => totalGameMinutes;

        // คืนค่าเวลาจากเซฟ — ไม่ยิง OnDayChanged (กันชนกับ ProgressionManager นับวันซ้ำ)
        public void RestoreClock(int day, float minutes)
        {
            currentDay = Mathf.Max(1, day);
            totalGameMinutes = Mathf.Clamp(minutes, 0f, 24f * 60f);
            lastMinute = -1;
            OnTimeChanged?.Invoke(Hour, Minute);
        }

        // แปลงเป็นข้อความ เช่น "10:30 AM" (ตามดีไซน์ HUD ในเอกสาร)
        public string GetTimeString()
        {
            int h = Hour;
            string ampm = h < 12 ? "AM" : "PM";
            int h12 = h % 12; if (h12 == 0) h12 = 12;
            return $"{h12:00}:{Minute:00} {ampm}";
        }

        // ข้ามเวลาไปตอนเช้า (สำหรับ "นอน") — ถ้าเลยเวลาตื่นแล้วจะข้ามไปวันถัดไป
        // ข้ามเวลาไปตอนเช้า (สำหรับ "นอน") — ถ้าเลยเวลาตื่นแล้วจะข้ามไปวันถัดไป
        //   ตั้งเวลาใหม่ "ก่อน" ยิง OnDayChanged → ผู้ฟัง (autosave/สรุปวัน/ค่าขนม) เห็นเวลาเช้าของวันใหม่แล้ว
        //   ข้ามวันได้ไม่เกิน 1 วันต่อครั้ง (OnDayChanged ยิงอย่างมากครั้งเดียว) · คืน true = ขึ้นวันใหม่
        public bool SkipToNextMorning(int wakeHour = 7)
        {
            ComputeWake(currentDay, totalGameMinutes, wakeHour, out int wakeDay);
            bool newDay = wakeDay != currentDay;
            totalGameMinutes = Mathf.Clamp(wakeHour, 0, 23) * 60f;
            lastMinute = -1;
            if (newDay) { currentDay = wakeDay; OnDayChanged?.Invoke(currentDay); }
            OnTimeChanged?.Invoke(Hour, Minute);
            return newDay;
        }

        // คำนวณ "เวลาตื่นครั้งถัดไป" (ฟังก์ชันล้วน ใช้ทั้งหน้ายืนยันการนอนและการข้ามเวลาจริง → ตรงกันเสมอ)
        //   ก่อนเวลาตื่น (เช่น 01:00) → ตื่นวันเดียวกัน · ถึง/เลยเวลาตื่น (เช่น 22:00) → ตื่นวันถัดไป
        public static float ComputeWake(int day, float minutes, int wakeHour, out int wakeDay)
        {
            float target = Mathf.Clamp(wakeHour, 0, 23) * 60f;
            wakeDay = minutes >= target ? day + 1 : day;
            return target;
        }

        // เวลาเป็นชั่วโมงทศนิยม 0–24 (ใช้คำนวณแสงให้ต่อเนื่อง ไม่กระโดดทีละนาที)
        public float HourFloat => Mathf.Repeat(totalGameMinutes, 24f * 60f) / 60f;

        // Multiplayer: รับวัน/เวลาจาก Host (ผ่าน WorldTimeSync) — ยิง OnDayChanged ทีละวันตามจริง (ครั้งเดียวต่อวัน ไม่ซ้ำ)
        //   วันเท่าเดิม → แค่ปรับเวลา (ไม่ยิงเหตุการณ์ข้ามวัน) · Host ย้อนวัน (ไม่ควรเกิด) → ตั้งตามโดยไม่ยิงเหตุการณ์
        public void ApplyAuthoritativeTime(int day, float minutes)
        {
            minutes = Mathf.Clamp(minutes, 0f, 24f * 60f - 0.01f);
            int steps = 0;
            while (day > currentDay && steps < 7)
            {
                currentDay++; steps++;
                if (day == currentDay) totalGameMinutes = minutes;   // วันสุดท้าย: ตั้งเวลาก่อนยิง ให้ผู้ฟังเห็นเวลาจริง
                OnDayChanged?.Invoke(currentDay);
            }
            if (day != currentDay) currentDay = Mathf.Max(1, day);
            totalGameMinutes = minutes;
            int m = Minute;
            if (m != lastMinute || steps > 0) { lastMinute = m; OnTimeChanged?.Invoke(Hour, m); }
        }
    }
}
