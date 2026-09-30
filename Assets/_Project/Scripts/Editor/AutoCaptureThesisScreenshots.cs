#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NisitSimulator.Core;
using NisitSimulator.UI;
using NisitSimulator.Systems;
using NisitSimulator.Player;

namespace NisitSimulator.EditorTools
{
    // 📸 เครื่องมือแคปภาพหน้าจออัตโนมัติทุกหน้าสำหรับเล่มปริญญานิพนธ์
    // เมนู: Nisit -> 📸 Auto Capture All Thesis Screenshots (กดครั้งเดียวแคปครบทุกรูป)
    public static class AutoCaptureThesisScreenshots
    {
        const string RunnerName = "_AutoThesisCaptureRunner";

        [MenuItem("Nisit/📸 Auto Capture All Thesis Screenshots (กดครั้งเดียวแคปครบทุกรูป)", false, 0)]
        public static void MenuStart()
        {
            if (!EditorUtility.DisplayDialog("Auto Capture",
                "เริ่มระบบแคปภาพหน้าจอเกมอัตโนมัติสำหรับเล่มปริญญานิพนธ์?\n\n" +
                "• ระบบจะเข้า Play Mode และสลับหน้าจอแคปภาพให้ครบทุกหน้า\n" +
                "• ไฟล์จะตั้งชื่อตามบทที่ 4 เช่น Fig4_2_MainMenu, Fig4_4_GameplayHUD เป็นต้น\n" +
                "• บันทึกไฟล์ลงโฟลเดอร์ Screenshots/ อัตโนมัติในความละเอียดคมชัด\n" +
                "• ใช้เวลาทำงานประมาณ 15 วินาที", "เริ่มเลย!", "ยกเลิก"))
                return;

            StartCapture();
        }

        public static void StartCapture()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene1.unity", OpenSceneMode.Single);

            var existing = GameObject.Find(RunnerName);
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject(RunnerName);
            go.AddComponent<ThesisCaptureRunner>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorApplication.isPlaying = true;
        }
    }

    public class ThesisCaptureRunner : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.8f);

            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);

            // 1. ภาพ 4.2 หน้าเมนูหลัก
            yield return Capture(dir, "Fig4_2_MainMenu.png");

            var menu = Object.FindFirstObjectByType<MainMenuController>();

            // 2. หน้าตั้งค่าเสียงในเมนู
            if (menu != null && menu.settingsPanel != null)
            {
                menu.settingsPanel.transform.SetAsLastSibling();
                menu.settingsPanel.SetActive(true);
                yield return new WaitForSeconds(0.5f);
                yield return Capture(dir, "Extra_Settings.png");
                menu.settingsPanel.SetActive(false);
                yield return new WaitForSeconds(0.3f);
            }

            // 3. ภาพ 4.3 หน้าแต่งตัวละคร 3D
            if (menu != null && menu.characterPanel != null)
            {
                menu.OpenCustomize();
                yield return new WaitForSeconds(0.8f);
                yield return Capture(dir, "Fig4_3_CharacterCreator.png");
            }

            // 4. ภาพ 4.3 หน้าเลือกคณะ
            if (menu != null && menu.facultyPanel != null)
            {
                menu.ConfirmCustomize();
                yield return new WaitForSeconds(0.6f);
                yield return Capture(dir, "Fig4_3_FacultySelect.png");
            }

            // 5. ฉาก 02_Lobby (ห้องล็อบบี้ Multiplayer)
            if (Application.CanStreamedLevelBeLoaded("02_Lobby"))
            {
                SceneManager.LoadScene("02_Lobby");
                yield return new WaitForSeconds(1.2f);
                yield return Capture(dir, "Extra_MultiplayerLobby.png");
            }

            // 6. โหลดฉาก 01_Gameplay
            SceneManager.LoadScene("01_Gameplay");
            yield return new WaitForSeconds(1.8f);

            var nuiInit = Object.FindFirstObjectByType<NisitSimulator.Net.NetworkUI>(FindObjectsInactive.Include);
            if (nuiInit != null && nuiInit.panel != null) nuiInit.panel.SetActive(false);
            HUDController.Instance?.ClearToast();

            // 7. ภาพ 4.4 หน้าจอเล่นเกมพร้อม HUD
            yield return Capture(dir, "Fig4_4_GameplayHUD.png");

            // 8. ภาพ 4.7 โทรศัพท์ในเกม (กด Tab)
            var phone = Object.FindFirstObjectByType<PhoneController>(FindObjectsInactive.Include);
            if (phone != null)
            {
                phone.Toggle();
                yield return new WaitForSeconds(0.7f);
                yield return Capture(dir, "Fig4_7_PhoneUI.png");
                phone.Toggle();
                yield return new WaitForSeconds(0.3f);
            }

            // 9. ภาพ 4.5 หน้าจอทำข้อสอบ (กด F8)
            var exam = Object.FindFirstObjectByType<ExamController>(FindObjectsInactive.Include);
            if (exam != null)
            {
                for (var t = exam.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                if (exam.panel != null)
                {
                    exam.Begin(true, 2);
                    yield return new WaitForSeconds(0.7f);
                    yield return Capture(dir, "Fig4_5_ExamScreen.png");
                    if (exam.panel != null) exam.panel.SetActive(false);
                    yield return new WaitForSeconds(0.3f);
                }
            }

            // 10. ภาพ 4.6 เหตุการณ์ GoTo (เสาสัญญาณสีทอง + ลูกศรนำทาง)
            var evMgr = Object.FindFirstObjectByType<EventManager>(FindObjectsInactive.Include);
            if (evMgr != null)
            {
                var choice = new EventManager.Choice
                {
                    kind = EventManager.Kind.GoTo,
                    objectiveText = "ช่วยงานอาจารย์ที่คณะ (ภารกิจสุ่ม)",
                    knowledge = 25,
                    money = 120,
                    exp = 40
                };
                evMgr.StartObjectiveExternal(choice);
                yield return new WaitForSeconds(0.8f);
                yield return Capture(dir, "Fig4_6_GoToEvent.png");
            }

            // 11. ภาพ 4.9 ท่าทางตัวละคร (นั่งอ่านหนังสือ/พิมพ์งาน)
            var player = GameObject.Find("Player");
            var act = player != null ? player.GetComponent<PlayerActionController>() : null;
            if (act != null)
            {
                act.Perform(10f, null, "Sitting");
                yield return new WaitForSeconds(0.8f);
                yield return Capture(dir, "Fig4_9_CharacterAction.png");
            }

            // 12. เมนูหยุดเกม Pause Menu (Esc)
            var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause != null && pause.panel != null)
            {
                pause.panel.SetActive(true);
                yield return new WaitForSeconds(0.6f);
                yield return Capture(dir, "Extra_PauseMenu.png");
                pause.panel.SetActive(false);
                yield return new WaitForSeconds(0.3f);
            }

            // 13. สมุดบันทึกความสำเร็จ (J)
            var achieve = Object.FindFirstObjectByType<AchievementsUI>(FindObjectsInactive.Include);
            if (achieve != null)
            {
                var f = typeof(AchievementsUI).GetField("panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var pan = f != null ? f.GetValue(achieve) as GameObject : null;
                if (pan != null)
                {
                    pan.SetActive(true);
                    yield return new WaitForSeconds(0.6f);
                    yield return Capture(dir, "Extra_Achievements.png");
                    pan.SetActive(false);
                    yield return new WaitForSeconds(0.3f);
                }
            }

            // 14. ภาพ 4.8 หน้าจบการศึกษา (Graduation / Game Over / Flunked Out)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndGame(EndReason.Graduated);
                yield return new WaitForSeconds(0.7f);
                yield return Capture(dir, "Fig4_8_Graduation.png");

                GameManager.Instance.EndGame(EndReason.Died);
                yield return new WaitForSeconds(0.7f);
                yield return Capture(dir, "Fig4_8_GameOver.png");

                GameManager.Instance.EndGame(EndReason.Flunked);
                yield return new WaitForSeconds(0.7f);
                yield return Capture(dir, "Fig4_8_FlunkedOut.png");
            }

            Debug.Log("<color=lime>[AutoThesisCapture] แคปครบทุกหน้าจอเรียบร้อยแล้ว! รวมทั้งสิ้น 13 รูป 🎉</color>");

            yield return new WaitForSeconds(0.5f);

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            EditorUtility.RevealInFinder(Path.Combine(dir, "Fig4_4_GameplayHUD.png"));
            EditorUtility.DisplayDialog("Nisit Simulator",
                "แคปภาพหน้าจอเกมครบทุกรูปแล้ว! 🎉\n\n" +
                "• บันทึกภาพลงโฟลเดอร์: Screenshots/\n" +
                "• ตั้งชื่อตรงตามเล่มบทที่ 4 ทุกรูป (Fig4_2 ถึง Fig4_9)\n" +
                "• ภาพหน้าจอเวอร์ชันล่าสุด (ฟอนต์ Pattaya + ธีมการ์ตูนพาสเทล)\n\n" +
                "เปิดโฟลเดอร์ให้แล้ว สามารถลากไปใส่เล่ม Word ได้เลยครับ!", "ยอดเยี่ยม!");
            #endif
        }

        IEnumerator Capture(string dir, string filename)
        {
            HUDController.Instance?.ClearToast();
            var nui = Object.FindFirstObjectByType<NisitSimulator.Net.NetworkUI>(FindObjectsInactive.Include);
            if (nui != null && nui.panel != null) nui.panel.SetActive(false);

            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, filename);
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("<color=cyan>[AutoThesisCapture] บันทึกแล้ว: " + filename + "</color>");
            yield return new WaitForSeconds(0.3f);
        }
    }
}
#endif
