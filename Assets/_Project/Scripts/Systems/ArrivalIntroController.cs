using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.Academics;
using NisitSimulator.CameraRig;
using NisitSimulator.Interaction;
using NisitSimulator.Player;
using NisitSimulator.SaveLoad;
using NisitSimulator.TimeSystem;
using NisitSimulator.UI;

namespace NisitSimulator.Systems
{
    // Scene-local, single-player onboarding. No network state or shared PlayerPrefs.
    public class ArrivalIntroController : MonoBehaviour
    {
        public static ArrivalIntroController Instance { get; private set; }
        public static bool Active => Instance != null && Instance.initialized && !Instance.progress.Done && ClassroomRules.IsSinglePlayer;
        public static bool BlocksMovement => Active && (Instance.progress.Step < 2 || Instance.progress.Step >= 9 || Instance.atStop);
        public static bool BlocksPhone => Active && Instance.progress.Step < 9;
        public static bool IsCinematic => Active && Instance.progress.Step == 0;
        public GameObject vehicle;
        public Transform driveStart, driveStop, driveEnd, arrivalPoint, seniorHome;
        public Transform[] stops;
        public TalkNPC senior;
        public float cinematicSeconds = 10f;
        public int Step => progress.Step;
        public bool AtStop => atStop;
        public bool RegistrationNavigationShown { get; private set; }
        ArrivalProgress progress = new ArrivalProgress();
        bool initialized, atStop, cameraHeld;
        float elapsed, greetingElapsed;
        int dialogueLine;
        GameObject player;
        Camera mainCamera;
        IsometricCameraRig rig;
        bool rigWasEnabled, wasOrthographic;
        float oldFov, oldSize;
        Renderer[] playerRenderers;
        bool[] rendererEnabled;
        ArrivalGuideUI guide;
        PhoneController phone;
        ObjectiveHUD hud;
        Vector3 greetingFrom;

        static readonly string[] Names = { "หอพัก", "โรงอาหารกลาง", "ร้านค้า", "อาคาร IT", "อาคาร GE", "ห้องสมุด", "อาคารชมรม" };
        public static readonly string[] StopLines = {
            "นี่หอพักของเรา กลับมานอนฟื้นพลังและบันทึกเกมได้ที่นี่\nห้องพักของน้องอยู่ชั้น 1 นะ",
            "หิวเมื่อไรก็แวะโรงอาหารกลาง เลือกร้านแล้วกด E ซื้ออาหารได้เลย\nกินให้อิ่มก่อนออกไปเรียน จะได้มีแรงตลอดวัน",
            "ร้านค้าขายของที่เก็บไว้ใช้ในกระเป๋าได้\nเตรียมของที่จำเป็นก่อนออกไปเรียนก็ดีนะ",
            "อาคาร IT มีห้องบรรยาย แล็บคอม และห้องสอบ\nดูรหัสห้องในตาราง แล้วใช้ปุ่มนำทางใน MSG REG ช่วยได้",
            "อาคาร GE เป็นห้องเรียนวิชาศึกษาทั่วไปและวิชาอื่นตามตาราง\nเช็กทั้งรหัสห้องและเวลาก่อนเข้าเรียนทุกครั้งนะ",
            "ห้องสมุดเหมาะกับการอ่านหนังสือเพิ่มความรู้และทบทวนวิชา\nเตรียมตัวก่อนสอบทีละนิด จะได้ไม่เครียดช่วงท้ายภาค",
            "ที่อาคารชมรมมีพื้นที่ทำกิจกรรมและพักผ่อนกับเพื่อน\nเสาร์อาทิตย์ไม่มีคาบเรียน หาเวลามาคลายเครียดได้เลย"
        };
        void Awake() { Instance = this; }
        public static void Begin(SaveData loaded)
        {
            // TODO(multiplayer): arrival and campus tour need a separate synchronized design.
            if (!ClassroomRules.IsSinglePlayer || OnboardingFlow.IsPractice) return;
            var self = Instance;
            if (self == null) self = Object.FindAnyObjectByType<ArrivalIntroController>();
            if (self == null) return;
            self.progress = ArrivalProgress.FromSave(loaded);
            self.initialized = true;
            if (!self.progress.Done) self.StartCoroutine(self.StartGuide());
        }
        IEnumerator StartGuide()
        {
            GameClock.ArrivalSuspended = true;
            yield return null; yield return null; // Let registrar, phone and quests finish Start.
            player = GameObject.Find("Player");
            phone = Object.FindAnyObjectByType<PhoneController>(FindObjectsInactive.Include);
            hud = Object.FindAnyObjectByType<ObjectiveHUD>(FindObjectsInactive.Include);
            if (player == null || arrivalPoint == null || stops == null || stops.Length != 7)
            { Debug.LogError("[Arrival] Missing scene references; run ArrivalSceneSetup."); Complete(); yield break; }
            guide = new GameObject("ArrivalGuideUI").AddComponent<ArrivalGuideUI>();
            guide.Build(Next, SkipTour);
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            if (CourseRegistrar.Active)
            {
                var quest = Object.FindAnyObjectByType<QuestSystem>();
                if (quest != null && !progress.QuestRewarded) quest.EnsureArrivalQuest();
            }
            if (senior != null) senior.enabled = false;
            if (progress.Step == 0) StartCar();
            else
            {
                if (vehicle != null) vehicle.SetActive(false);
                if (progress.Step == 1) PlacePlayer(arrivalPoint.position);
                EnterStep(false);
            }
        }
        public static void CollectSave(SaveData data)
        {
            if (Instance != null && Instance.initialized) Instance.progress.Collect(data);
            else { data.arrivalVersion = 1; data.arrivalIntroDone = true; data.tourStep = 14; }
        }
        void SaveCheckpoint() { if (initialized && player != null) SaveManager.Save(); }
        void SetStep(int step) { progress.Step = step; dialogueLine = 0; EnterStep(true); }
        void EnterStep(bool save)
        {
            atStop = false;
            ClassroomNavigator.ClearNavigation();
            if (guide != null) guide.PointAt(null);
            if (progress.Step == 1)
            {
                greetingElapsed = 0;
                if (senior != null) { greetingFrom = player.transform.position + new Vector3(-4, 0, 2); senior.transform.position = greetingFrom; Gesture("Walking"); }
                ShowGreeting();
            }
            else if (progress.Step >= 2 && progress.Step <= 8)
            {
                int i = progress.Step - 2;
                ClassroomNavigator.NavigateToPoint(stops[i].position, $"ทัวร์ {i + 1}/7 · {Names[i]}");
                if (senior != null) senior.transform.position = stops[i].position + new Vector3(1.5f, .08f, 0);
                guide.Present("พี่ต้นกล้า ปี 3 · ทัวร์มหาวิทยาลัย", "เดินตามเสาแสงไปที่" + Names[i] + "\nเวลาเกมพักไว้ระหว่างทัวร์ กดข้ามทัวร์เพื่อไปสอนลงทะเบียนได้", false);
            }
            else if (progress.Step >= 9 && progress.Step < 14) ShowRegistration();
            if (save) SaveCheckpoint();
        }
        void Update()
        {
            if (!Active || guide == null || player == null) return;
            if (NisitSimulator.Core.GameManager.Instance != null && !NisitSimulator.Core.GameManager.Instance.IsActive) return;
            GameClock.ArrivalSuspended = true;
            if (progress.Step == 0)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) { FinishCar(); return; }
                UpdateCar(); return;
            }
            if (progress.Step == 1 && senior != null)
            {
                greetingElapsed += Time.unscaledDeltaTime;
                var target = player.transform.position + new Vector3(-1.7f, 0, 1.3f);
                senior.transform.position = Vector3.Lerp(greetingFrom, target, Mathf.Clamp01(greetingElapsed / 2));
                senior.transform.LookAt(new Vector3(player.transform.position.x, senior.transform.position.y, player.transform.position.z));
                if (greetingElapsed >= 2) Gesture("Talking");
            }
            if (progress.Step >= 2 && progress.Step <= 8 && !atStop)
            {
                Vector3 delta = player.transform.position - stops[progress.Step - 2].position; delta.y = 0;
                if (delta.sqrMagnitude <= 16)
                {
                    atStop = true; ClassroomNavigator.ClearNavigation(); Gesture("Talking");
                    guide.Present("พี่ต้นกล้า ปี 3 · " + Names[progress.Step - 2], StopLines[progress.Step - 2], true);
                }
            }
            if (progress.Step >= 9) UpdateRegistration();
            if (Input.GetKeyDown(KeyCode.E)) Next();
        }
        public void Next()
        {
            if (!Active || guide == null) return;
            if (progress.Step == 1)
            {
                if (dialogueLine++ == 0) ShowGreeting(); else SetStep(2);
            }
            else if (progress.Step >= 2 && progress.Step <= 8 && atStop) SetStep(progress.Step + 1);
            else if (progress.Step == 12 && RegistrationUI.IsOpen && RegistrationUI.Instance.CurrentTab == 1 && RegistrationNavigationShown) SetStep(13);
            else if (progress.Step == 13 && (!CourseRegistrar.Active || (RegistrationUI.IsOpen && RegistrationUI.Instance.CurrentTab == 2))) Complete();
        }
        void ShowGreeting()
        {
            guide.Present("พี่ต้นกล้า ปี 3", dialogueLine == 0
                ? "สวัสดี เราชื่อต้นกล้า อยู่ปี 3 นะ ยินดีต้อนรับเข้ามหาวิทยาลัย!\nวันนี้วันอาทิตย์ เป็นวันลงทะเบียน เดี๋ยวพี่พารู้จักสถานที่ก่อน"
                : "วันนี้ยังไม่มีคาบ เริ่มเรียนวันจันทร์นะ\nตามเสาแสงไปทีละจุด หรือกดข้ามทัวร์แล้วไปดูวิธีลงทะเบียนได้เลย", true);
        }
        public void SkipTour()
        {
            if (!Active || progress.Step >= 9) return;
            RestoreCar();
            if (progress.Step <= 1) PlacePlayer(arrivalPoint.position);
            SetStep(9);
        }
        void ShowRegistration()
        {
            bool regular = CourseRegistrar.Active;
            string text = progress.Step == 9 ? (regular ? "กด TAB เปิดโทรศัพท์ แล้วเลือกแอป MSG REG ที่ไฮไลต์\nต้องเลือกวิชาและยืนยันภายในวันอาทิตย์แรกของภาคนะ" : "กด TAB เปิดโทรศัพท์ ดูปฏิทินและผลการเรียนได้เลย\nคณะนี้ใช้การเรียนตามปฏิทิน จึงไม่ต้องลงทะเบียนรายวิชา")
                : progress.Step == 10 ? "กด + เพิ่ม เพื่อเลือกวิชาที่ต้องการ แนะนำเริ่มจากวิชาตามแผน\nดูหน่วยกิตและตารางก่อนยืนยัน เลือกไว้เฉย ๆ ยังไม่ถือว่าลงทะเบียน"
                : progress.Step == 11 ? "เลือกรายวิชาแล้วกดยืนยันลงทะเบียนที่ไฮไลต์\nเพิ่มหรือถอนวิชาได้หลังยืนยัน แต่ต้องยืนยันการแก้ไขก่อนสิ้นวันอาทิตย์นะ"
                : progress.Step == 12 ? "ลงทะเบียนสำเร็จแล้ว! ยังเพิ่มหรือถอนวิชาแล้วกดยืนยันการแก้ไขได้\nเปิดแท็บตารางเรียน ลองกดนำทางไปห้องคาบถัดไป แล้วกดต่อไป [E]"
                : "เปิดแท็บผลการเรียนเพื่อดูเกรด GPA และหน่วยกิตสะสม\nพร้อมแล้วกดต่อไป [E] เริ่มชีวิตนิสิตได้เลย!";
            guide.Present("พี่ต้นกล้า ปี 3 · " + (progress.Step >= 12 ? "พร้อมเริ่มเรียน" : "สอนใช้โทรศัพท์"), text, progress.Step >= 12, true);
        }
        void UpdateRegistration()
        {
            var reg = CourseRegistrar.Instance;
            if (reg == null || !reg.IsActive)
            {
                if (progress.Step == 9 && phone != null && phone.IsOpen) SetStep(13);
                return;
            }
            var ui = RegistrationUI.Instance;
            if (reg.Record.Current != null && reg.Record.Current.confirmed && progress.Step < 12)
            {
                if (!progress.QuestRewarded) { GameplayEvents.Raise(GameplayEvents.Register); progress.QuestRewarded = true; }
                SetStep(12);
            }
            if (progress.Step == 9 && RegistrationUI.IsOpen) SetStep(10);
            if (progress.Step == 10 && reg.Service.SelectedCredits() > 0) SetStep(11);
            if (progress.Step == 11 && reg.Service.SelectedCredits() == 0) SetStep(10);
            if (ui != null)
            {
                string target = progress.Step == 9 || !RegistrationUI.IsOpen ? "app" : progress.Step == 10 ? "add" : progress.Step == 11 ? "confirm" : progress.Step == 12 ? (ui.CurrentTab != 1 ? "schedule" : "navigate") : "grades";
                guide.PointAt(ui.GuideTarget(target));
            }
        }
        public void NavigationDemonstrated()
        {
            if (progress.Step != 12) return;
            RegistrationNavigationShown = true;
            // Keep the real app visible until the user reads the next instruction.
            if (phone != null && !phone.IsOpen) phone.Toggle();
            RegistrationUI.Instance.Open(); RegistrationUI.Instance.ShowTab(1);
            ShowRegistration();
        }
        public void Complete()
        {
            RestoreCar(); ClassroomNavigator.ClearNavigation();
            progress.Done = true; progress.Step = ArrivalProgress.CompletedStep;
            GameClock.ArrivalSuspended = false;
            if (senior != null) { if (seniorHome != null) senior.transform.position = seniorHome.position; senior.enabled = true; Gesture("Idle"); }
            if (guide != null) { Destroy(guide.gameObject); guide = null; }
            SaveCheckpoint();
            HUDController.Toast("ยินดีต้อนรับสู่ชีวิตนิสิต! แวะคุยกับพี่ต้นกล้าได้ทุกวันนะ");
        }
        void StartCar()
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                rig = mainCamera.GetComponent<IsometricCameraRig>(); rigWasEnabled = rig != null && rig.enabled;
                if (rig != null) rig.enabled = false;
                wasOrthographic = mainCamera.orthographic; oldFov = mainCamera.fieldOfView; oldSize = mainCamera.orthographicSize;
                mainCamera.orthographic = false; mainCamera.fieldOfView = 48; cameraHeld = true;
            }
            playerRenderers = player.GetComponentsInChildren<Renderer>(); rendererEnabled = new bool[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++) { rendererEnabled[i] = playerRenderers[i].enabled; playerRenderers[i].enabled = false; }
            PlacePlayer(arrivalPoint.position);
            if (vehicle != null) { vehicle.SetActive(true); vehicle.transform.position = driveStart.position; }
            elapsed = 0;
            guide.Present("วันอาทิตย์ · วันแรกในมหาวิทยาลัย", "รถกำลังมาส่งที่หอพัก\nSpace / Esc ข้ามฉากรถ", false);
            SaveCheckpoint();
        }
        void UpdateCar()
        {
            elapsed += Time.unscaledDeltaTime;
            float seconds = elapsed * 10f / Mathf.Max(8f, cinematicSeconds);
            if (vehicle != null)
            {
                Vector3 a = seconds < 6 ? driveStart.position : driveStop.position;
                Vector3 b = seconds < 6 ? driveStop.position : driveEnd.position;
                float t = seconds < 6 ? Mathf.Clamp01(seconds / 4) : Mathf.Clamp01((seconds - 6) / 4);
                vehicle.transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
                Vector3 forward = b - a; forward.y = 0;
                if (forward.sqrMagnitude > .01f) vehicle.transform.rotation = Quaternion.LookRotation(forward);
                if (mainCamera != null)
                {
                    Vector3 focus = seconds < 6 ? vehicle.transform.position : player.transform.position;
                    mainCamera.transform.position = focus + new Vector3(8, 6, -10);
                    mainCamera.transform.LookAt(focus + Vector3.up);
                }
            }
            if (seconds >= 4 && seconds < 6)
            {
                ShowPlayer();
                PlacePlayer(Vector3.Lerp(driveStop.position + new Vector3(1.3f, 0, 0), arrivalPoint.position, (seconds - 4) / 2));
                var anim = player.GetComponentInChildren<Animator>(); if (anim != null) anim.SetFloat("Speed", .5f);
            }
            if (seconds >= 10) FinishCar();
        }
        void FinishCar() { RestoreCar(); PlacePlayer(arrivalPoint.position); SetStep(1); }
        void PlacePlayer(Vector3 pos) { if (player != null) InteriorManager.Teleport(player.transform, PlayerSpawnSystem.GroundSnap(player, pos), Quaternion.Euler(0, 180, 0)); }
        void ShowPlayer() { if (playerRenderers != null) for (int i = 0; i < playerRenderers.Length; i++) if (playerRenderers[i] != null) playerRenderers[i].enabled = rendererEnabled[i]; }
        void RestoreCar()
        {
            ShowPlayer();
            if (vehicle != null) vehicle.SetActive(false);
            if (player != null) { var a = player.GetComponentInChildren<Animator>(); if (a != null) a.SetFloat("Speed", 0); }
            if (cameraHeld && mainCamera != null)
            {
                mainCamera.orthographic = wasOrthographic; mainCamera.fieldOfView = oldFov; mainCamera.orthographicSize = oldSize;
                if (rig != null) { rig.enabled = rigWasEnabled; rig.SnapToTarget(); }
            }
            cameraHeld = false;
        }
        string currentGesture;
        void Gesture(string state)
        {
            if (senior == null || currentGesture == state) return;
            currentGesture = state;
            var a = senior.GetComponentInChildren<Animator>();
            if (a != null && a.HasState(0, Animator.StringToHash(state))) a.CrossFade(state, .15f);
        }
        void OnDestroy()
        {
            if (initialized && !progress.Done) { RestoreCar(); GameClock.ArrivalSuspended = false; }
            if (guide != null) Destroy(guide.gameObject);
            if (Instance == this) Instance = null;
        }
    }
}
