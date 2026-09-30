using UnityEngine;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;
using NisitSimulator.UI;

namespace NisitSimulator.Interaction
{
    // ห้องสอบประจำคณะ — กด E เพื่อเข้าสอบ (เฉพาะวันสอบ + เฉพาะคณะที่เรียน)
    // facultyIndex: 0=IT, 1=บริหาร, 2=วิทย์, 3=นิเทศ (-1 = คณะไหนก็สอบได้)
    public class ExamStation : MonoBehaviour, IInteractable
    {
        public int facultyIndex = -1;

        private ExamController exam;

        void Start() { exam = Object.FindFirstObjectByType<ExamController>(); }

        bool IsMyFaculty => facultyIndex < 0 || facultyIndex == GameSession.SelectedFacultyIndex;

        public string GetPrompt()
        {
            if (!IsMyFaculty)
                return $"ตึกคณะ {FacultyCatalog.NameOf(facultyIndex)} (ไม่ใช่คณะคุณ)";
            if (exam != null && exam.HasPendingExam)
                return NisitSimulator.Academics.CourseRegistrar.Active
                    ? "กด E เพื่อเข้าห้องสอบ — เลือกวิชาที่ลงทะเบียนเพื่อสอบ (วันนี้มีสอบ!)"
                    : "กด E เพื่อเข้าห้องสอบ (วันนี้มีสอบ!)";
            return "กด E — ห้องสอบคณะคุณ (วันนี้ยังไม่มีสอบ)";
        }

        public void Interact(GameObject interactor)
        {
            if (!IsMyFaculty)
            {
                HUDController.Toast($"คุณเรียนคณะ {FacultyCatalog.NameOf(GameSession.SelectedFacultyIndex)} — ต้องไปสอบที่ตึกคณะตัวเอง");
                return;
            }
            if (exam == null) exam = Object.FindFirstObjectByType<ExamController>();
            if (exam != null) exam.TryTakeExam(interactor);
            else HUDController.Toast("ยังไม่มีระบบสอบ (กด Nisit ▸ Build Exam System)");
        }
    }
}
