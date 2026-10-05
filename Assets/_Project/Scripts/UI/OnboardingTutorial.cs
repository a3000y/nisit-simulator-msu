using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Player;
using NisitSimulator.CameraRig;
using NisitSimulator.Stats;
using NisitSimulator.Systems;
using NisitSimulator.Core;
namespace NisitSimulator.UI
{
    [DefaultExecutionOrder(200)]
    public class OnboardingTutorial : MonoBehaviour
    {
        public Transform player, destination;
        public IsometricCameraRig cameraRig;
        public TMP_Text progress, title, body, status;
        public Button back, next, skip;
        public PhoneController phone;
        public PauseMenu pause;
        public MinimapToggle map;
        public GameObject checkpoint, activities;
        public Image hudHighlight;
        public TMP_Text satisfactionText;
        public int Step { get; private set; }
        public bool Ready => Step == 0 || Step == 3 || Step == 6 || completed[Step];
        readonly bool[] completed = new bool[7];
        bool walked, cameraChanged, studied, ate, rested, openedPhone, calendar, quests, openedMap, paused;
        float initialYaw, initialZoom;
        IsometricCameraRig.Mode initialMode;
        PlayerStats stats;
        string[] titles = { "ยินดีต้อนรับ นิสิตใหม่", "ลองเดินและใช้กล้อง", "ลองโต้ตอบ", "ดูแลตัวเองผ่านค่าสถานะ", "ลองใช้ชีวิตนิสิต", "เครื่องมือที่ต้องรู้", "พร้อมเข้ามหาวิทยาลัย" };
        void Start()
        {
            Time.timeScale = 1f;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            stats = player.GetComponent<PlayerStats>();
            foreach(var renderer in player.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer = 9;
            NisitSimulator.Academics.CourseRegistrar.EnsureExists().InitializeNew();
            RegistrationUI.EnsureExists();
            stats.LoadState(65,100,60,0,50,100,0,20);
            LevelSystem.EnsureExists();
            initialYaw = cameraRig.yawAngle; initialZoom = cameraRig.orthoSize; initialMode = cameraRig.mode;
            back.onClick.AddListener(Back); next.onClick.AddListener(Next); skip.onClick.AddListener(Skip);
            GameplayEvents.OnAction += ActivityDone;
            GameManager.Instance.StartGame();
            Refresh();
        }
        void OnDestroy() { GameplayEvents.OnAction -= ActivityDone; }
        public void Next() { if (!Ready) return; if (Step == 6) { OnboardingFlow.Finish(false); return; } Step++; Refresh(); }
        public void Back() { if (Step <= 0) return; Step--; Refresh(); }
        public void Skip() { OnboardingFlow.Finish(true); }
        public void CheckpointReached(GameObject interactor)
        {
            if (Step != 2 || interactor.transform != player) return;
            completed[2] = true;
        }
        void ActivityDone(string key)
        {
            if (Step != 4) return;
            if (key == GameplayEvents.Study) studied = true;
            if (key == GameplayEvents.Eat) ate = true;
            if (key == GameplayEvents.Relax) rested = true;
            completed[4] = studied && ate && rested;
        }
        void Update()
        {
            if (Step == 1)
            {
                if (Vector3.Distance(new Vector3(player.position.x,0,player.position.z), new Vector3(destination.position.x,0,destination.position.z)) < 1.2f) walked = true;
                if (Mathf.Abs(Mathf.DeltaAngle(initialYaw,cameraRig.yawAngle)) > 10f || Mathf.Abs(initialZoom-cameraRig.orthoSize) > .5f || cameraRig.mode != initialMode) cameraChanged = true;
                completed[1] = walked && cameraChanged;
            }
            if (Step == 5)
            {
                openedPhone |= phone.IsOpen;
                if (phone.IsOpen && phone.appView.activeSelf)
                {
                    if (!NisitSimulator.Academics.CourseRegistrar.Active) calendar |= phone.appTitle.text == "ปฏิทินการศึกษา";
                    quests |= phone.appTitle.text == "ภารกิจวันนี้";
                    openedMap |= phone.mapView != null && phone.mapView.activeSelf;
                }
                if (NisitSimulator.Academics.CourseRegistrar.Active) calendar |= RegistrationUI.IsOpen && RegistrationUI.Instance.CurrentTab == 1;
                openedMap |= map != null && map.IsOpen;
                paused |= pause.panel.activeSelf;
                completed[5] = openedPhone && calendar && quests && openedMap && paused;
            }
            foreach(var scaler in GetComponentsInParent<CanvasScaler>()) scaler.matchWidthOrHeight = Screen.width / (float)Mathf.Max(1,Screen.height) >= 1280f/720f ? 1f : 0f;
            var utilityScaler = skip.GetComponentInParent<CanvasScaler>(); if(utilityScaler!=null) utilityScaler.matchWidthOrHeight = Screen.width / (float)Mathf.Max(1,Screen.height) >= 1280f/720f ? 1f : 0f;
            next.interactable = Ready && !pause.panel.activeSelf && !phone.IsOpen;
            status.text = StatusText();
            if (satisfactionText != null && stats != null) satisfactionText.text = "ความพึงพอใจ " + stats.Satisfaction.ToString("0") + "  |  ข้อมูลฝึกเท่านั้น";
            if (hudHighlight != null && Step == 3) { var c = hudHighlight.color; c.a = .2f + .15f * Mathf.Sin(Time.unscaledTime * 3f); hudHighlight.color = c; }
        }
        string Mark(bool done, string text) => (done ? "<color=#177852>[ครบ] " : "[รอ] ") + text + (done ? "</color>" : "");
        string StatusText()
        {
            if (Step == 1) return Mark(walked,"ถึงวงกลมสีทอง") + "\n" + Mark(cameraChanged,"หมุน ซูม หรือสลับกล้อง");
            if (Step == 2) return Mark(completed[2],"โต้ตอบกับป้ายต้อนรับ");
            if (Step == 4) return Mark(studied,"เรียน") + "   " + Mark(ate,"กินอาหาร") + "   " + Mark(rested,"พักผ่อน");
            if (Step == 5) return Mark(openedPhone,"โทรศัพท์") + "   " + Mark(calendar,"ตารางเรียน") + "\n" + Mark(quests,"ภารกิจ") + "   " + Mark(openedMap,"แผนที่") + "   " + Mark(paused,"พักเกม");
            return Step == 6 ? "เงิน เวลา และ EXP ฝึกจะไม่ติดไปในเกมจริง" : "อ่านแล้วกดถัดไปได้เลย";
        }
        void Refresh()
        {
            progress.text = "ขั้นที่ " + (Step + 1) + "/7  ·  พื้นที่ฝึก";
            title.text = titles[Step];
            var interaction = player.GetComponent<PlayerInteraction>();
            string e = interaction.interactKey.ToString();
            var movement = player.GetComponent<PlayerMovement>();
            string cam = cameraRig.rotateLeftKey + " / " + cameraRig.rotateRightKey;
            string[] descriptions = {
                "รับบทเป็นนิสิต จัดเวลาเรียน กินอาหาร พักผ่อน และทำกิจกรรม\n\nเรียนให้ผ่านตามหลักสูตร หรือสะสม EXP ตามเกณฑ์ปีที่คณะใช้\nดูแลสุขภาพและ GPA ให้ถึงเกณฑ์",
                "WASD / ลูกศร: เดิน   Shift: วิ่ง\nSpace: กระโดด\n\nเดินไปวงกลมสีทอง\n" + cam + ": หมุนกล้อง หรือกดเมาส์กลางค้างแล้วลาก\nล้อเมาส์: ซูม   " + cameraRig.toggleKey + ": สลับมุมมอง",
                "เดินเข้าใกล้ป้ายต้อนรับ จนเห็นข้อความบน HUD\n\nกด [" + e + "] เพื่อโต้ตอบ\nร้านอาหาร ห้องเรียน และจุดกิจกรรมใช้การโต้ตอบแบบเดียวกัน",
                "HUD ซ้าย: พลังงาน ความอิ่ม สุขภาพ ความเครียด\nพักเพื่อฟื้นพลัง กินให้อิ่ม เพื่อลดความเครียด\n\nHUD ขวา: เวลา ชั้นปี เลเวล และ EXP\nTAB → สถานะ: เงินและความพึงพอใจ\nTAB → MSG REG: ตารางเรียนและผลการเรียน",
                "เดินไปจุดฝึกทั้ง 3 จุด แล้วกด [" + e + "]\nเรียน: ใช้พลังงาน ได้ EXP\nอาหาร: เพิ่มความอิ่ม\nพักผ่อน: ฟื้นพลัง เพิ่มความพึงพอใจ\n\nใช้ระบบเดิม แต่ข้อมูลนี้อยู่เฉพาะฉากฝึก",
                "[" + phone.toggleKey + "] โทรศัพท์ → เปิดภารกิจ\nมีหลักสูตร: ลงทะเบียน → ตารางเรียน\nคณะอื่น: เปิดปฏิทิน\n[" + map.key + "] แผนที่ หรือแอปแผนที่\n[Esc] พักเกม → เล่นต่อ\nลองให้ครบ แล้วปิดหน้าต่างก่อนถัดไป",
                "ต่อไป: รถจะพาไปส่งที่หอพัก รุ่นพี่จะพาทัวร์มหาวิทยาลัย\nวันอาทิตย์เป็นวันลงทะเบียน เริ่มเรียนวันจันทร์\n\nดูพลังงานและความอิ่มก่อนออกจากหอพัก ใช้แผนที่หาจุดหมาย\n\nเริ่มต้นด้วยข้อมูลและจุดเกิดจากระบบเกมใหม่เดิม"
            };
            body.text = descriptions[Step];
            back.interactable = Step > 0;
            next.GetComponentInChildren<TMP_Text>().text = Step == 6 ? (OnboardingFlow.Replay ? "กลับเมนูหลัก" : "เริ่มชีวิตนิสิต") : "ถัดไป";
            skip.GetComponentInChildren<TMP_Text>().text = OnboardingFlow.Replay ? "ออกจากการสอน" : "ข้ามการสอน";
            checkpoint.SetActive(Step == 2);
            activities.SetActive(Step == 4);
            destination.gameObject.SetActive(Step == 1);
            if (hudHighlight != null) hudHighlight.gameObject.SetActive(Step == 3);
        }
    }
}
