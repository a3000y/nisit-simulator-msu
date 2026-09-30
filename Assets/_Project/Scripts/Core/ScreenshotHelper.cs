using System.Collections;
using System.IO;
using UnityEngine;
using NisitSimulator.Systems;
using NisitSimulator.UI;
using NisitSimulator.Player;

namespace NisitSimulator.Core
{
    // 📸 ตัวช่วยแคปหน้าจอทำเอกสารปริญญานิพนธ์
    //   กด F9 แคป · F1-F8 เรียกหน้าจอที่ต้องใช้ในเล่ม
    //   ไฟล์เซฟที่โฟลเดอร์ Screenshots/ (นอก Assets) · แถบช่วยจำไม่ติดในรูป
    public class ScreenshotHelper : MonoBehaviour
    {
        public int superSize = 1;   // 2 = ละเอียด 2 เท่า (ไฟล์ใหญ่ขึ้น คมชัดสำหรับพิมพ์เล่ม)
        private bool hideHint;
        private bool showGui = false;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) StartCoroutine(Capture());
            if (Input.GetKeyDown(KeyCode.F1)) showGui = !showGui;

            // ปุ่มลัดเรียกหน้าจอสำหรับทำเอกสารปริญญานิพนธ์ (ฉาก Gameplay)
            //   F2–F8 เปลี่ยนสถานะเกม (จบเกม/เปิดสอบ) = คำสั่งลัด → ใช้ได้เฉพาะ Editor / Development Build เท่านั้น
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F2)) TriggerCharacterAction();
            if (Input.GetKeyDown(KeyCode.F3)) TriggerPhone();
            if (Input.GetKeyDown(KeyCode.F4)) TriggerGoToEvent();
            if (Input.GetKeyDown(KeyCode.F5)) GameManager.Instance?.EndGame(EndReason.Graduated); // จบการศึกษา
            if (Input.GetKeyDown(KeyCode.F6)) GameManager.Instance?.EndGame(EndReason.Died);      // เสียชีวิต/Game Over
            if (Input.GetKeyDown(KeyCode.F7)) GameManager.Instance?.EndGame(EndReason.Flunked);   // เรียนไม่ผ่าน/รีไทร์
            // F8 ชนกับ Dev Panel (ค่าเริ่มต้น F8) → ถ้า Dev Panel ใช้ F8 อยู่ ให้เปิดหน้าสอบด้วย Shift+F8 แทน
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool devOwnsF8 = NisitSimulator.DevTools.DevPanel.ToggleKey == KeyCode.F8;
            if (Input.GetKeyDown(KeyCode.F8) && (!devOwnsF8 || shift)) OpenExam();
#endif
        }

        // F2: จำลองท่าทางตัวละคร (นั่ง/พิมพ์งาน) สำหรับภาพ 4.9
        void TriggerCharacterAction()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            var act = player.GetComponent<PlayerActionController>();
            if (act != null)
            {
                act.Perform(10f, null, "Sitting");
                HUDController.Toast("เล่นท่าทางตัวละคร (นั่งอ่านหนังสือ/ทำงาน) 10 วิ — กด F9 แคปได้เลย");
            }
        }

        // F3: เปิด/ปิด โทรศัพท์นิสิต สำหรับภาพ 4.7
        void TriggerPhone()
        {
            var phone = Object.FindFirstObjectByType<PhoneController>(FindObjectsInactive.Include);
            phone?.Toggle();
        }

        // F4: จำลองเหตุการณ์เดินไปทำจริง (เสาสัญญาณ + ลูกศรชี้ทาง) สำหรับภาพ 4.6
        void TriggerGoToEvent()
        {
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
                HUDController.Toast("สร้างเหตุการณ์ GoTo (เสาสัญญาณแสง + ลูกศรนำทาง) แล้ว — กด F9 แคปได้เลย");
            }
        }

        // F8: เปิดหน้าสอบสำหรับแคป สำหรับภาพ 4.5
        void OpenExam()
        {
            var exam = Object.FindFirstObjectByType<ExamController>(FindObjectsInactive.Include);
            if (exam == null)
            {
                Debug.LogWarning("<color=orange>[Screenshot] ไม่พบ ExamController ในฉาก</color>");
                return;
            }
            for (var t = exam.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            if (exam.panel == null)
            {
                Debug.LogWarning("<color=orange>[Screenshot] ExamController.panel เป็น null</color>");
                return;
            }
            exam.Begin(true, 2);
            Debug.Log("<color=lime>[Screenshot] เปิดหน้าสอบแล้ว — กด F9 แคปได้เลย</color>");
        }

        // แคป: ซ่อนแถบช่วยจำ, ปิด NetworkUI, ลบ Toast ก่อน → รอจบเฟรม → ถ่าย
        IEnumerator Capture()
        {
            hideHint = true;
            HUDController.Instance?.ClearToast();

            var nui = Object.FindFirstObjectByType<NisitSimulator.Net.NetworkUI>(FindObjectsInactive.Include);
            if (nui != null && nui.panel != null) nui.panel.SetActive(false);

            yield return new WaitForEndOfFrame();

            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);
            string name = "shot_" + System.DateTime.Now.ToString("HHmmss") + ".png";
            string path = Path.Combine(dir, name);

            ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, superSize));
            Debug.Log("<color=lime>[Screenshot] บันทึกแล้ว → " + Path.GetFullPath(path) + "</color>");

            yield return new WaitForEndOfFrame();
            hideHint = false;
            HUDController.Toast("บันทึกภาพหน้าจอแล้ว: Screenshots/" + name);
        }

        void OnGUI()
        {
            if (hideHint || !showGui) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            style.normal.textColor = Color.white;
            GUI.color = new Color(0, 0, 0, 0.70f);
            GUI.Box(new Rect(10, 10, 360, 205), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 16, 340, 195),
                "📸 SCREENSHOT HELPER (ทำปริญญานิพนธ์)\n" +
                "• F9 = บันทึกภาพหน้าจอ (Screenshots/)\n" +
                "• F1 = ซ่อน/แสดง แถบคำแนะนำนี้\n" +
                "• F2 = ท่าทางตัวละคร (ภาพ 4.9)\n" +
                "• F3 = เปิด/ปิด โทรศัพท์ (ภาพ 4.7)\n" +
                "• F4 = เสาสัญญาณเหตุการณ์+ลูกศร (ภาพ 4.6)\n" +
                "• F5 = จบการศึกษา (ภาพ 4.8)\n" +
                "• F6 = เสียชีวิต/Game Over (ภาพ 4.8)\n" +
                "• F7 = รีไทร์/เรียนไม่ผ่าน (ภาพ 4.8)\n" +
                "• F8 = หน้าจอทำข้อสอบ (ภาพ 4.5) — Shift+F8 ถ้า Dev Panel ใช้ F8", style);
        }
    }
}
