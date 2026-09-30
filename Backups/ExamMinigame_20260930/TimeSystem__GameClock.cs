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
        public int startHour = 8;    // เริ่มเกม 08:00
        public int startDay = 1;

        // เวลาในเกม (สะสมเป็นนาทีทั้งหมดตั้งแต่เริ่มวัน)
        private float totalGameMinutes;

        // event: ยิงทุกครั้งที่ "นาที" เปลี่ยน (สำหรับนาฬิกา HUD)
        public event Action<int, int> OnTimeChanged;   // (ชั่วโมง, นาที)
        // event: ยิงเมื่อขึ้นวันใหม่ (สำหรับสรุปวัน/ลดสถานะ/สุ่มเหตุการณ์)
        public event Action<int> OnDayChanged;         // (วันที่)

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
            totalGameMinutes += Time.deltaTime * gameMinutesPerRealSecond;

            // ข้ามวันเมื่อครบ 24 ชม.
            if (totalGameMinutes >= 24 * 60)
            {
                totalGameMinutes -= 24 * 60;
                currentDay++;
                OnDayChanged?.Invoke(currentDay);
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
        public void SkipToNextMorning(int wakeHour = 7)
        {
            float target = wakeHour * 60f;
            if (totalGameMinutes >= target) { currentDay++; OnDayChanged?.Invoke(currentDay); }   // เลยเวลาตื่น = พรุ่งนี้
            totalGameMinutes = target;
            lastMinute = -1;
            OnTimeChanged?.Invoke(Hour, Minute);
        }
    }
}
