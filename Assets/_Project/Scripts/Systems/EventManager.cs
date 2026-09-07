using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Stats;
using NisitSimulator.Player;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // ระบบสุ่มเหตุการณ์ (แบบเกมจริง)
    //   Instant = ปุ่มเดียว→แจ้งเตือน+ได้ผลเลย(ไม่เด้ง) · หลายปุ่ม→ป็อปอัพเลือก (เหลือแค่แบบนี้ที่มีป็อปอัพ)
    //   Effect  = ไม่มีป็อปอัพ! แจ้งเตือน(เล่าเหตุการณ์)+ใส่ผลทั้งวันเลย (ป่วย/ไฟแรง) ผ่าน PlayerEffects
    //   GoTo    = ไม่มีป็อปอัพ! โผล่เสาแสง+แจ้งเตือนในโลกเลย เดินไปเก็บเอง (เกมต่อเนื่อง) — ปล่อยผ่านได้
    // UI สร้าง+ต่อโดย Editor tool (Nisit -> Build Event System)
    public class EventManager : MonoBehaviour
    {
        public enum Kind { Instant, Effect, GoTo }
        public enum Eff { None, Sick, Inspired }

        [System.Serializable]
        public class Choice
        {
            public string label = "โอเค";
            public string result;
            public Kind kind = Kind.Instant;
            public Eff effect = Eff.None;
            public string targetDoor;      // GoTo: ชื่อ object อาคาร/ประตูเป้าหมาย
            public string objectiveText;   // GoTo: ข้อความภารกิจ
            public float energy, health, hunger, knowledge, satisfaction;
            public int money, exp;
        }

        public class GameEvent { public string title; public string desc; public Choice[] choices; }

        [Header("UI (เซ็ตโดย Editor)")]
        public GameObject panel;
        public TMP_Text titleText;
        public TMP_Text descText;
        public Button[] choiceButtons = new Button[2];
        public TMP_Text[] choiceLabels = new TMP_Text[2];
        public ObjectiveHUD objectiveHUD;   // แถบภารกิจ + ลูกศรชี้ทาง (A)

        [Header("โอกาสเกิดเหตุการณ์ต่อวัน (0-1)")]
        [Range(0f, 1f)] public float eventChance = 0.55f;

        public bool IsOpen { get; private set; }

        private ProgressionManager prog;
        private PlayerStats stats;
        private PlayerMovement move;
        private GameObject player;
        private GameEvent current;
        private bool ready;

        // ภารกิจเดินไปทำ (A)
        private Choice objective;
        private Vector3 objectiveTarget;
        private GameObject beacon;

        void Start()
        {
            prog = Object.FindFirstObjectByType<ProgressionManager>();
            stats = Object.FindFirstObjectByType<PlayerStats>();
            player = GameObject.Find("Player");
            if (player != null) move = player.GetComponent<PlayerMovement>();

            if (prog != null) prog.OnDayInYearChanged += OnDay;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int idx = i;
                if (choiceButtons[i] != null) choiceButtons[i].onClick.AddListener(() => Choose(idx));
            }
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy() { if (prog != null) prog.OnDayInYearChanged -= OnDay; }

        void Update()
        {
            // failsafe: กด Esc ปิดป็อปอัพเหตุการณ์ (กันค้างถ้าปุ่มเลือกหลุด/หาย)
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                current = null;
                if (panel != null) panel.SetActive(false);
                IsOpen = false;
                Time.timeScale = 1f;
                if (move != null) move.enabled = true;
                return;
            }

            // เช็คว่าถึงจุดหมายภารกิจ (A) หรือยัง
            if (objective == null || player == null) return;
            Vector3 a = player.transform.position;
            float dx = a.x - objectiveTarget.x, dz = a.z - objectiveTarget.z;
            if (dx * dx + dz * dz <= 9f)   // ภายใน 3 เมตร
            {
                var c = objective;
                ClearObjective();
                var act = player.GetComponent<PlayerActionController>() ?? player.AddComponent<PlayerActionController>();
                if (act != null && !act.IsBusy) act.Perform(1.8f, () => ApplyOutcome(c));
                else ApplyOutcome(c);
            }
        }

        private void OnDay(int dayInYear, int daysPerYear)
        {
            // ภารกิจค้างจากเมื่อวาน = พลาดโอกาส
            if (objective != null) { HUDController.Toast("พลาดโอกาสเมื่อวานไปแล้ว..."); ClearObjective(); }

            if (!ready) { ready = true; return; }   // ข้ามครั้งแรก (เริ่มเกม)

            int sem = AcademicCalendar.SemesterIndex(dayInYear);
            int len = AcademicCalendar.SemesterLen(sem);
            int semDay = AcademicCalendar.SemesterDay(dayInYear);
            int mid = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            bool examDay = semDay == len || (AcademicCalendar.HasMidterm(sem) && semDay == mid);
            if (examDay) return;   // เว้นวันสอบ

            if (Random.value <= eventChance) Trigger();
        }

        public void Trigger()
        {
            // ผู้เล่นกำลังอยู่ในหน้าต่าง/ทำกิจกรรม (movement ถูกปิด) → เลื่อนเหตุการณ์ไปก่อน (กันป็อปอัพซ้อน)
            if (move != null && !move.enabled) return;

            var bank = Bank();
            current = bank[Random.Range(0, bank.Count)];

            // GoTo = โอกาสในโลกจริง → ไม่เด้งป็อปอัพ โผล่เสาแสง+แจ้งเตือนเลย (เกมไม่สะดุด เดินไปเก็บเองหรือปล่อยผ่านก็ได้)
            var goChoice = FindGoTo(current);
            if (goChoice != null)
            {
                current = null;
                SetObjective(goChoice);
                return;
            }

            // Effect = เหตุการณ์ที่เกิดขึ้นแล้ว → ไม่เด้งป็อปอัพ แจ้งเตือน(เล่าเหตุการณ์)+ใส่ผลทั้งวันเลย
            var effChoice = FindEffect(current);
            if (effChoice != null)
            {
                if (string.IsNullOrEmpty(effChoice.result)) effChoice.result = current.desc;   // ใช้คำบรรยายเป็นข้อความแจ้งเตือน
                current = null;
                ApplyEffect(effChoice);
                return;
            }

            // Instant ปุ่มเดียว = ไม่มีทางเลือกจริง → ไม่เด้งป็อปอัพ แจ้งเตือน+ได้ผลเลย
            if (current.choices != null && current.choices.Length == 1)
            {
                var only = current.choices[0];
                if (string.IsNullOrEmpty(only.result)) only.result = current.desc;
                current = null;
                ApplyOutcome(only);
                return;
            }

            // Instant ที่เลือกได้จริง (หลายปุ่ม) = ป็อปอัพเหมือนเดิม
            if (panel == null) return;
            IsOpen = true;
            panel.SetActive(true);
            if (titleText != null) titleText.text = current.title;
            if (descText != null) descText.text = current.desc;

            int n = current.choices != null ? current.choices.Length : 0;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool has = i < n;
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(has);
                if (has && choiceLabels[i] != null) choiceLabels[i].text = current.choices[i].label;
            }

            if (move != null) move.enabled = false;
            Time.timeScale = 0f;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }

        private void Choose(int i)
        {
            if (!IsOpen || current == null || current.choices == null || i >= current.choices.Length) return;
            var c = current.choices[i];
            current = null;

            if (panel != null) panel.SetActive(false);
            IsOpen = false;
            Time.timeScale = 1f;
            if (move != null) move.enabled = true;

            switch (c.kind)
            {
                case Kind.GoTo:   SetObjective(c); break;
                case Kind.Effect: ApplyEffect(c);  break;
                default:
                    var act = player != null
                        ? (player.GetComponent<PlayerActionController>() ?? player.AddComponent<PlayerActionController>())
                        : null;
                    if (act != null && !act.IsBusy) act.Perform(1.8f, () => ApplyOutcome(c));
                    else ApplyOutcome(c);
                    break;
            }
        }

        // หาตัวเลือกแบบ GoTo ในเหตุการณ์ (มี = เป็นโอกาสในโลก ไม่ต้องเด้งป็อปอัพ)
        private static Choice FindGoTo(GameEvent e)
        {
            if (e == null || e.choices == null) return null;
            foreach (var c in e.choices) if (c.kind == Kind.GoTo) return c;
            return null;
        }

        // หาตัวเลือกแบบ Effect (มี = เหตุการณ์เกิดขึ้นแล้ว ไม่ต้องเด้งป็อปอัพ)
        private static Choice FindEffect(GameEvent e)
        {
            if (e == null || e.choices == null) return null;
            foreach (var c in e.choices) if (c.kind == Kind.Effect) return c;
            return null;
        }

        // ให้ระบบอื่น (เช่น NPC quest-giver) สั่งภารกิจเดินไปทำได้ — คืน true ถ้าเริ่มได้ (ไม่มีภารกิจค้าง)
        public bool StartObjectiveExternal(Choice c)
        {
            if (c == null || objective != null) return false;
            SetObjective(c);
            return true;
        }

        // ---------- A: เดินไปทำที่จริง (โผล่เสาแสง เดินไปเอง) ----------
        private void SetObjective(Choice c)
        {
            var door = GameObject.Find(c.targetDoor);
            objectiveTarget = door != null
                ? door.transform.position
                : (player != null ? player.transform.position + player.transform.forward * 6f : Vector3.zero);
            objective = c;
            SpawnBeacon(objectiveTarget);
            if (objectiveHUD != null) objectiveHUD.Set(objectiveTarget, c.objectiveText);
            HUDController.Toast($"โอกาส: {c.objectiveText} — เดินไปที่เสาแสงทอง (หรือปล่อยผ่านก็ได้)");
        }

        private void ClearObjective()
        {
            objective = null;
            if (objectiveHUD != null) objectiveHUD.Clear();
            if (beacon != null) { Destroy(beacon); beacon = null; }
        }

        private void SpawnBeacon(Vector3 pos)
        {
            if (beacon != null) Destroy(beacon);
            beacon = new GameObject("EventBeacon");
            beacon.transform.position = pos;

            var glow = new Color(1f, 0.82f, 0.28f);
            var mat = GlowMat(glow);

            // ลำแสง
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            beam.transform.SetParent(beacon.transform, false);
            beam.transform.localPosition = new Vector3(0f, 6f, 0f);
            beam.transform.localScale = new Vector3(0.5f, 6f, 0.5f);
            KillCollider(beam); beam.GetComponent<Renderer>().sharedMaterial = mat;

            // วงแสงที่พื้น
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(beacon.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            ring.transform.localScale = new Vector3(2.5f, 0.05f, 2.5f);
            KillCollider(ring); ring.GetComponent<Renderer>().sharedMaterial = mat;

            var fx = beacon.AddComponent<BeaconFX>();
            fx.beam = beam.transform; fx.ring = ring.transform;
        }

        private static Material GlowMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            mat.SetColor("_BaseColor", c); mat.color = c;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 3f);
            return mat;
        }

        private static void KillCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
        }

        // ---------- B: ผลกระทบทั้งวัน ----------
        private void ApplyEffect(Choice c)
        {
            var fx = player != null
                ? (player.GetComponent<PlayerEffects>() ?? player.AddComponent<PlayerEffects>())
                : null;
            if (fx != null)
            {
                if (c.effect == Eff.Sick) { fx.ApplySick(); NisitSimulator.Core.SFXManager.Error(); }
                else if (c.effect == Eff.Inspired) { fx.ApplyInspired(); NisitSimulator.Core.SFXManager.Success(); }
            }
            ApplyStats(c);
            if (!string.IsNullOrEmpty(c.result)) HUDController.Toast(c.result);
        }

        // ---------- ให้ผล (ใช้กับ Instant + ตอนถึงจุดหมาย GoTo) ----------
        private void ApplyOutcome(Choice c)
        {
            ApplyStats(c);
            if (!string.IsNullOrEmpty(c.result)) HUDController.Toast(c.result);
            // ผลรวมดี = สำเร็จ · ผลรวมร้าย = ผิดพลาด
            float net = c.satisfaction + c.knowledge * 0.2f + c.health + c.energy + c.money * 0.1f + c.exp * 0.1f;
            if (net >= 0f) NisitSimulator.Core.SFXManager.Success();
            else NisitSimulator.Core.SFXManager.Error();
        }

        private void ApplyStats(Choice c)
        {
            if (stats == null) return;
            if (c.energy != 0) stats.ChangeEnergy(c.energy);
            if (c.health != 0) stats.ChangeHealth(c.health);
            if (c.hunger != 0) stats.ChangeHunger(c.hunger);
            if (c.knowledge != 0) stats.ChangeKnowledge(c.knowledge);
            if (c.satisfaction != 0) stats.ChangeSatisfaction(c.satisfaction);
            if (c.money != 0) stats.ChangeMoney(c.money);
            if (c.exp != 0) stats.AddExp(c.exp);
        }

        // ---------- คลังเหตุการณ์ (ผสม GoTo / Effect / Instant) ----------
        private List<GameEvent> Bank() => new List<GameEvent>
        {
            // === A: เดินไปทำที่จริง ===
            new GameEvent { title = "เพื่อนชวนกินข้าว", desc = "เพื่อนชวนไปกินข้าวเที่ยงที่โรงอาหาร ไปด้วยกันไหม?",
                choices = new[] {
                    new Choice { label = "ไปกินด้วยกัน", kind = Kind.GoTo, targetDoor = "Door_โรงอาหาร", objectiveText = "ไปกินข้าวที่โรงอาหาร",
                                 satisfaction = 15, hunger = 25, money = -30, result = "อิ่มอร่อย! พอใจ +15" },
                    new Choice { label = "ขอตัวไปเรียน", knowledge = 15, result = "ตั้งใจเรียนต่อ ความรู้ +15" },
                } },
            new GameEvent { title = "เจอชีทข้อสอบเก่า", desc = "มีรุ่นพี่บอกว่าห้องสมุดมีชีทข้อสอบเก่า อยากไปหาไหม?",
                choices = new[] {
                    new Choice { label = "ไปหาที่ห้องสมุด", kind = Kind.GoTo, targetDoor = "Door_ห้องสมุด", objectiveText = "ไปทบทวนที่ห้องสมุด",
                                 knowledge = 45, energy = -10, result = "ติวเข้มจากชีทเก่า! ความรู้ +45" },
                    new Choice { label = "ไว้ก่อน", result = "เดินผ่านไปเฉย ๆ" },
                } },
            new GameEvent { title = "รุ่นพี่ชวนเข้าชมรม", desc = "รุ่นพี่ชวนไปสมัครชมรมกิจกรรมที่อาคารชมรม",
                choices = new[] {
                    new Choice { label = "ไปสมัคร", kind = Kind.GoTo, targetDoor = "Door_อาคารชมรม", objectiveText = "ไปสมัครชมรมที่อาคารชมรม",
                                 satisfaction = 12, exp = 30, result = "ได้เพื่อนใหม่! พอใจ +12, EXP +30" },
                    new Choice { label = "ขอตัวก่อน", knowledge = 10, result = "กลับไปอ่านหนังสือต่อ" },
                } },

            // === B: ผลกระทบทั้งวัน ===
            new GameEvent { title = "ฝนตกหนัก", desc = "เดินตากฝนกลางทางจนตัวเปียก เริ่มไม่สบาย...",
                choices = new[] {
                    new Choice { label = "แย่จัง...", kind = Kind.Effect, effect = Eff.Sick, health = -8 },
                } },
            new GameEvent { title = "นอนเต็มอิ่ม", desc = "เมื่อคืนหลับสบายมาก ตื่นมาสมองปลอดโปร่งสุด ๆ",
                choices = new[] {
                    new Choice { label = "รู้สึกดี!", kind = Kind.Effect, effect = Eff.Inspired, energy = 10 },
                } },

            // === Instant: ป็อปอัปเร็ว ===
            new GameEvent { title = "โชคดี!", desc = "เจอเงินตกอยู่ข้างทาง!",
                choices = new[] { new Choice { label = "เก็บเลย", money = 50, result = "เก็บเงินได้ +50฿" } } },
            new GameEvent { title = "คะแนนพิเศษ!", desc = "อาจารย์ให้คะแนนบวกจากการตอบคำถามในห้อง",
                choices = new[] { new Choice { label = "เยี่ยม!", knowledge = 20, exp = 20, result = "ได้คะแนนพิเศษ ความรู้ +20, EXP +20" } } },
            new GameEvent { title = "แมวจรจัด", desc = "มีแมวน้อยมานั่งคลอเคลียหน้าหอ",
                choices = new[] {
                    new Choice { label = "ลูบหัวแมว", satisfaction = 10, result = "หายเหนื่อยเลย! พอใจ +10" },
                    new Choice { label = "เดินผ่าน", result = "รีบไปเรียนต่อ" },
                } },

            // === เพิ่มเติม: GoTo (เดินไปทำที่ตึกอื่น ๆ ให้กระจาย) ===
            new GameEvent { title = "มีธุระที่สำนักงาน", desc = "ต้องไปยื่นเอกสารที่อาคารบริหาร ไปจัดการเลยไหม?",
                choices = new[] {
                    new Choice { label = "ไปจัดการ", kind = Kind.GoTo, targetDoor = "Door_อาคารบริหาร", objectiveText = "ไปยื่นเอกสารที่อาคารบริหาร",
                                 money = -25, exp = 15, satisfaction = 5, result = "จัดการเอกสารเรียบร้อย! EXP +15" },
                    new Choice { label = "ไว้ทีหลัง", satisfaction = -3, result = "ยังค้างอยู่... กังวลนิดหน่อย" },
                } },
            new GameEvent { title = "ของใช้หมด", desc = "ของจำเป็นหมดพอดี แวะร้านค้าหน่อยไหม?",
                choices = new[] {
                    new Choice { label = "ไปซื้อของ", kind = Kind.GoTo, targetDoor = "Door_ร้านค้า", objectiveText = "ไปซื้อของที่ร้านค้า",
                                 money = -20, satisfaction = 8, energy = 5, result = "ได้ของครบ สบายใจ! พอใจ +8" },
                    new Choice { label = "ทนไปก่อน", result = "เดินหน้าต่อ" },
                } },
            new GameEvent { title = "นัดติวกลุ่ม", desc = "เพื่อนชวนติวกลุ่มที่อาคารเรียน ไปไหม?",
                choices = new[] {
                    new Choice { label = "ไปติวกลุ่ม", kind = Kind.GoTo, targetDoor = "Door_อาคารเรียน", objectiveText = "ไปติวกลุ่มที่อาคารเรียน",
                                 knowledge = 45, energy = -12, result = "ติวกับเพื่อนได้ความรู้เพียบ! +45" },
                    new Choice { label = "ติวเอง", knowledge = 15, result = "อ่านเองที่ห้อง ความรู้ +15" },
                } },
            new GameEvent { title = "ง่วงจัด", desc = "ตาจะปิดแล้ว... กลับไปงีบที่หอสักหน่อยไหม?",
                choices = new[] {
                    new Choice { label = "กลับไปงีบ", kind = Kind.GoTo, targetDoor = "Door_หอพัก", objectiveText = "กลับไปงีบที่หอพัก",
                                 energy = 25, hunger = -5, result = "งีบสักพัก สดชื่นขึ้น! พลังงาน +25" },
                    new Choice { label = "ฝืนต่อ", energy = -8, result = "ฝืนไหว... แต่ล้าลงหน่อย" },
                } },

            // === เพิ่มเติม: Effect (ผลทั้งวัน) ===
            new GameEvent { title = "อดนอนดูซีรีส์", desc = "เมื่อคืนดูซีรีส์เพลินจนดึก วันนี้เพลียสุด ๆ",
                choices = new[] {
                    new Choice { label = "ง่วงจัง...", kind = Kind.Effect, effect = Eff.Sick, energy = -5 },
                } },
            new GameEvent { title = "ได้แรงบันดาลใจ", desc = "ฟังรุ่นพี่เล่าเรื่องความสำเร็จ ไฟลุกเลย!",
                choices = new[] {
                    new Choice { label = "สู้!", kind = Kind.Effect, effect = Eff.Inspired, satisfaction = 8 },
                } },

            // === เพิ่มเติม: Instant ===
            new GameEvent { title = "ถูกรางวัลจับฉลาก", desc = "กิจกรรมมหาลัยแจกรางวัล คุณได้รางวัลเล็ก ๆ!",
                choices = new[] { new Choice { label = "เย่!", money = 45, satisfaction = 5, result = "โชคดี! ได้ +45฿" } } },
            new GameEvent { title = "ทำแก้วน้ำหก", desc = "เผลอทำแก้วน้ำหกใส่กระเป๋า เสียของไปนิดหน่อย",
                choices = new[] { new Choice { label = "เซ็งเลย...", money = -20, satisfaction = -5, result = "ซวยนิดหน่อย เสีย 20฿" } } },
            new GameEvent { title = "เพื่อนแบ่งขนม", desc = "เพื่อนซื้อขนมมาเผื่อ แบ่งให้ด้วย!",
                choices = new[] { new Choice { label = "ขอบใจนะ!", hunger = 15, satisfaction = 8, result = "อิ่มใจอิ่มท้อง! พอใจ +8" } } },
        };
    }
}
