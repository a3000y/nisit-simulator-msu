using UnityEngine;

namespace NisitSimulator.Data
{
    // นิยามวิชาเรียน 1 วิชา — ใช้กับระบบลงทะเบียน/ตารางเรียน (บท 1.3.2.1)
    // คลิกขวาใน Project → Create → Nisit → Course
    [CreateAssetMenu(fileName = "NewCourse", menuName = "Nisit/Course")]
    public class CourseData : ScriptableObject
    {
        [Header("ข้อมูลวิชา")]
        public string courseCode;      // รหัสวิชา เช่น "IT101"
        public string courseName;      // ชื่อวิชา
        public int credits = 3;        // หน่วยกิต
        public int yearLevel = 1;      // เปิดให้ชั้นปีที่เท่าไร (1-4)

        [Header("ตารางเรียน")]
        public int classHour = 9;      // คาบเรียนเริ่มกี่โมง
        public int durationHours = 1;  // เรียนกี่ชั่วโมง

        [Header("ผลตอบแทนเมื่อเข้าเรียน")]
        public float knowledgeGain = 10f;  // ความรู้ที่ได้
        public int expGain = 20;           // EXP ที่ได้
        public float energyCost = 15f;     // พลังงานที่เสีย

        // TODO (M5): เพิ่มเงื่อนไขวิชาบังคับก่อน (prerequisite) และคะแนนสอบ
    }
}
