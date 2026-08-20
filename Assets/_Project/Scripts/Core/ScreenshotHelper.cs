using System.Collections;
using System.IO;
using UnityEngine;
using NisitSimulator.Systems;

namespace NisitSimulator.Core
{
    // 📸 ตัวช่วยแคปหน้าจอทำเอกสาร — กด F9 แคป · F5-F8 เรียกหน้าจอที่เข้าถึงยาก
    //   ไฟล์เซฟที่โฟลเดอร์ Screenshots/ (นอก Assets) · แถบช่วยจำไม่ติดในรูป
    // ใช้: เมนู  Nisit -> Add Screenshot Helper  (แปะให้ทั้ง 2 ฉาก)
    public class ScreenshotHelper : MonoBehaviour
    {
        public int superSize = 1;   // 2 = ละเอียด 2 เท่า (ไฟล์ใหญ่ขึ้น)
        private bool hideHint;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) StartCoroutine(Capture());

            // ปุ่มลัดเรียกหน้าจอที่เข้าถึงยาก (เฉพาะในฉากเกม)
            if (Input.GetKeyDown(KeyCode.F5)) GameManager.Instance?.EndGame(EndReason.Graduated); // จบการศึกษา
            if (Input.GetKeyDown(KeyCode.F6)) GameManager.Instance?.EndGame(EndReason.Died);      // เสียชีวิต
            if (Input.GetKeyDown(KeyCode.F7)) GameManager.Instance?.EndGame(EndReason.Flunked);   // เรียนไม่ผ่าน
            if (Input.GetKeyDown(KeyCode.F8)) OpenExam();
        }

        // เปิดหน้าสอบสำหรับแคป (แข็งแรง + บอกเหตุผลถ้าเปิดไม่ได้)
        void OpenExam()
        {
            var exam = Object.FindFirstObjectByType<ExamController>(FindObjectsInactive.Include);
            if (exam == null)
            {
                Debug.LogWarning("<color=orange>[Screenshot] ไม่พบ ExamController ในฉาก — กด Nisit ▸ Build Exam System ก่อน</color>");
                return;
            }
            // เปิดวัตถุตลอดสาย (เผื่อ Canvas สอบถูกปิดไว้) ให้แสดงผลได้
            for (var t = exam.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            if (exam.panel == null)
            {
                Debug.LogWarning("<color=orange>[Screenshot] ExamController.panel ยังไม่ถูกต่อ (null) — รัน Nisit ▸ Build Exam System อีกครั้ง</color>");
                return;
            }
            exam.Begin(true, 2);
            Debug.Log("<color=lime>[Screenshot] เปิดหน้าสอบแล้ว — กด F9 แคปได้เลย</color>");
        }

        // แคป: ซ่อนแถบช่วยจำก่อน → รอจบเฟรม → ถ่าย (รูปจะไม่ติดตัวอักษรช่วยจำ)
        IEnumerator Capture()
        {
            hideHint = true;
            yield return new WaitForEndOfFrame();

            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);
            string name = "shot_" + System.DateTime.Now.ToString("HHmmss") + ".png";
            string path = Path.Combine(dir, name);

            ScreenCapture.CaptureScreenshot(path, Mathf.Max(1, superSize));
            Debug.Log("<color=lime>[Screenshot] บันทึกแล้ว → " + Path.GetFullPath(path) + "</color>");

            yield return new WaitForEndOfFrame();
            hideHint = false;
        }

        void OnGUI()
        {
            if (hideHint) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = Color.white;
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.Box(new Rect(10, 10, 330, 128), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 16, 320, 120),
                "SCREENSHOT HELPER\n" +
                "F9 = Capture  (-> Screenshots/)\n" +
                "F5 = Graduation screen\n" +
                "F6 = Game Over screen\n" +
                "F7 = Flunked Out screen\n" +
                "F8 = Exam screen", style);
        }
    }
}
