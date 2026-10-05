using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Systems;
using NisitSimulator.Stats;
namespace NisitSimulator.Interaction
{
    public class SeniorAdvisor : MonoBehaviour
    {
        public string Advice()
        {
            var reg = CourseRegistrar.Instance;
            if (reg != null && reg.IsActive && reg.Record.Current != null && !reg.Record.Current.isBreak && !reg.Record.Current.confirmed)
                return reg.Service.RegistrationWindowOpen(reg.SemDay)
                    ? "วันนี้วันอาทิตย์ อย่าลืมเปิด TAB แล้วเข้า MSG REG เลือกวิชาและกดยืนยันก่อนหมดวันนะ"
                    : "ภาคนี้ยังไม่ได้ลงทะเบียน ใช้เวลาอ่านทบทวนก่อน แล้วลงทะเบียนวันอาทิตย์แรกของภาคถัดไปนะ";
            var exam = Object.FindAnyObjectByType<ExamController>();
            if (exam != null && exam.HasPendingExam) return "วันนี้มีสอบนะ เช็กวิชาและห้องสอบ เตรียมตัวแล้วไปให้ทันล่ะ";
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            if (stats != null && stats.Stress >= StressBands.StressedFrom) return "ดูเครียดอยู่นะ ลองพัก กินข้าว หรือคุยกับเพื่อน แล้วค่อยกลับไปอ่านหนังสือก็ได้";
            return "ดูตารางเรียนก่อนออกจากหอทุกวันนะ เสาร์อาทิตย์ไม่มีคาบ ใช้เวลาทำกิจกรรมและพักผ่อนได้เลย";
        }
    }
}
