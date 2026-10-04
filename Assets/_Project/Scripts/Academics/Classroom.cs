using System;
using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.Academics
{
    // ประเภทห้องเรียน — ใช้เลือกห้องตอนจัดตาราง + น้ำหนักเหตุการณ์สุ่มระหว่างเรียน
    public enum ClassroomType { Lecture, ComputerLab, Seminar, Office }

    // ข้อมูลห้องเรียนหนึ่งห้อง (data ล้วน) — ผูกกับห้องจริงในฉากด้วย ClassroomZone.roomId
    [Serializable]
    public class Classroom
    {
        [Tooltip("รหัสห้องถาวร (เช่น IT-201) — ClassSession.roomId อ้างรหัสนี้ ห้ามซ้ำ")]
        public string roomId;
        public string displayName;
        [Tooltip("ชื่ออาคารที่แสดงผู้เล่น เช่น อาคาร IT")]
        public string building;
        [Tooltip("ชื่อตึกแบบเดิม (Spawn_<ชื่อ> / ClassSession.building) — ใช้ตอน Multiplayer และประตูวาร์ป")]
        public string legacyBuilding;
        public int floor = 1;
        public ClassroomType type = ClassroomType.Lecture;
        public int capacity = 10;

        public Classroom() { }
        public Classroom(string id, string name, string building, string legacy, int floor, ClassroomType type, int capacity)
        {
            roomId = id; displayName = name; this.building = building; legacyBuilding = legacy;
            this.floor = floor; this.type = type; this.capacity = capacity;
        }

        // "อาคาร IT · ชั้น 2 · IT-202"
        public string LocationText => $"{building} · ชั้น {floor} · {roomId}";

        public static string TypeName(ClassroomType t)
        {
            switch (t)
            {
                case ClassroomType.ComputerLab: return "แล็บคอม";
                case ClassroomType.Seminar: return "ห้องสัมมนา";
                case ClassroomType.Office: return "สำนักงาน";
                default: return "ห้องบรรยาย";
            }
        }
    }

    // ทะเบียนห้องเรียนทั้งหมด (Resources/Classrooms/ClassroomCatalog) — สร้าง/อัปเดตด้วย Nisit ▸ Classrooms ▸ Setup Classroom Zones
    //   ไม่มี asset → ใช้ค่าเริ่มต้นจากโค้ด (ClassroomDefaults) — เทสต์ใช้ทางนี้
    [CreateAssetMenu(fileName = "ClassroomCatalog", menuName = "Nisit/Classroom Catalog")]
    public class ClassroomCatalog : ScriptableObject
    {
        public const string ResourcePath = "Classrooms/ClassroomCatalog";
        public List<Classroom> rooms = new List<Classroom>();

        [NonSerialized] Dictionary<string, Classroom> _map;
        void OnValidate() { _map = null; }

        public Classroom Get(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return null;
            if (_map == null || _map.Count != rooms.Count)
            {
                _map = new Dictionary<string, Classroom>();
                foreach (var r in rooms) if (r != null && !string.IsNullOrEmpty(r.roomId) && !_map.ContainsKey(r.roomId)) _map.Add(r.roomId, r);
            }
            _map.TryGetValue(roomId, out var c);
            return c;
        }

        public bool Exists(string roomId) => Get(roomId) != null;

        public List<string> Validate()
        {
            var errs = new List<string>();
            var seen = new HashSet<string>();
            foreach (var r in rooms)
            {
                if (r == null || string.IsNullOrEmpty(r.roomId)) { errs.Add("มีห้องที่ไม่มี roomId"); continue; }
                if (!seen.Add(r.roomId)) errs.Add($"roomId ซ้ำ {r.roomId}");
            }
            return errs;
        }

        static ClassroomCatalog _cache;
        public static ClassroomCatalog LoadDefault()
        {
            if (_cache != null) return _cache;
            _cache = Resources.Load<ClassroomCatalog>(ResourcePath);
            if (_cache == null) _cache = ClassroomDefaults.Create();
            return _cache;
        }

        public static void ClearCache() { _cache = null; }
    }

    // ห้องเรียนค่าเริ่มต้น (ตรงกับห้องจริงในฉาก 01_Gameplay: ตึก IT 4 ชั้น, ตึก GE 3 ชั้น, ห้องวาร์ปอาคารบริหาร)
    //   ความจุ = จำนวนที่นั่งที่เมนู Setup Classroom Zones สร้าง (เมนูเขียนค่าจริงทับลง asset)
    public static class ClassroomDefaults
    {
        public const string BuildingIT = "อาคาร IT";
        public const string BuildingGE = "อาคารเรียน GE";
        public const string BuildingAdmin = "อาคารบริหาร";
        public const string LegacyIT = "คณะ IT";
        public const string LegacyGE = "อาคารเรียน";
        public const string LegacyAdmin = "อาคารบริหาร";

        // ห้องที่ใช้สอน (ห้องเจ้าหน้าที่/อาจารย์/ประชุม/ทำงานกลุ่ม/โครงงาน ไม่ใช้สอน)
        public static readonly string[] ITLabs = { "IT-201", "IT-202", "IT-203", "IT-204", "IT-205", "IT-206", "IT-207" };
        public static readonly string[] ITLectures = { "IT-101", "IT-102", "IT-103", "IT-105", "IT-106", "IT-301", "IT-302", "IT-306" };
        public static readonly string[] ITSeminars = { "IT-405", "IT-407" };
        public static readonly string[] GELectures = { "GE-101", "GE-102", "GE-103", "GE-104", "GE-201", "GE-202", "GE-203", "GE-204", "GE-301", "GE-302", "GE-303", "GE-304" };
        public const string AdminOffice = "ADM-101";

        public static List<Classroom> Rooms()
        {
            var l = new List<Classroom>();
            foreach (var id in ITLectures) l.Add(new Classroom(id, "ห้องบรรยาย " + id, BuildingIT, LegacyIT, FloorOf(id), ClassroomType.Lecture, 8));
            foreach (var id in ITLabs) l.Add(new Classroom(id, "แล็บคอมพิวเตอร์ " + id, BuildingIT, LegacyIT, FloorOf(id), ClassroomType.ComputerLab, 12));
            foreach (var id in ITSeminars) l.Add(new Classroom(id, "ห้องสัมมนา " + id, BuildingIT, LegacyIT, FloorOf(id), ClassroomType.Seminar, 15));
            foreach (var id in GELectures) l.Add(new Classroom(id, "ห้องเรียน " + id, BuildingGE, LegacyGE, FloorOf(id), ClassroomType.Lecture, 16));
            l.Add(new Classroom(AdminOffice, "สำนักงานฝึกงาน/สหกิจ", BuildingAdmin, LegacyAdmin, 1, ClassroomType.Office, 4));
            return l;
        }

        // IT-201 → 2 · GE-304 → 3 · อื่น ๆ → 1
        public static int FloorOf(string id)
        {
            int dash = id != null ? id.IndexOf('-') : -1;
            if (dash < 0 || dash + 1 >= id.Length) return 1;
            char c = id[dash + 1];
            return char.IsDigit(c) ? Mathf.Max(1, c - '0') : 1;
        }

        public static ClassroomCatalog Create()
        {
            var c = ScriptableObject.CreateInstance<ClassroomCatalog>();
            c.name = "ClassroomCatalog";
            c.rooms = Rooms();
            return c;
        }
    }

    // กฎกลางของระบบห้องเรียน/เร่งเวลา/เหตุการณ์ — ระบบใหม่ทั้งหมดทำงานเฉพาะโหมดเล่นคนเดียว
    public static class ClassroomRules
    {
        // TODO(multiplayer): ห้องเรียน/เร่งเวลา/เหตุการณ์ยังเป็นของเครื่องเดียว — ต้องมี host validation (ห้องที่นั่ง, ชั่วโมงที่นับ, ผลเหตุการณ์)
        //   และระบบโหวตเร่งเวลาให้ทุกคนในห้องเดียวกันก่อนเปิดใน multiplayer
        public static bool IsSinglePlayer =>
            !NisitSimulator.SaveLoad.GameSession.IsMultiplayerGame &&
            !NisitSimulator.TimeSystem.GameClock.NetworkFollower &&
            !NisitSimulator.TimeSystem.GameClock.NetworkAuthoritative;
    }
}
