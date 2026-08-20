using System.Collections.Generic;

namespace NisitSimulator.Systems
{
    // คลังคณะ + ข้อสอบแยกตามคณะ (ออกแบบ data-driven เผื่อ multiplayer: คณะเป็นค่าต่อผู้เล่น)
    // ข้อสอบจริง = ข้อทั่วไป (ทุกคณะ) + ข้อเฉพาะคณะนั้น
    public static class FacultyCatalog
    {
        public static readonly string[] Names =
        {
            "เทคโนโลยีสารสนเทศ (IT)",
            "บริหารธุรกิจ",
            "วิทยาศาสตร์",
            "นิเทศศาสตร์",
        };

        public static int Count => Names.Length;
        public static string NameOf(int i) => Names[System.Math.Max(0, System.Math.Min(i, Names.Length - 1))];

        // รวมข้อทั่วไป + ข้อเฉพาะคณะ (คืน list ใหม่ทุกครั้ง สุ่มได้อิสระ)
        public static List<ExamController.Question> GetQuestions(int facultyIndex)
        {
            var list = new List<ExamController.Question>(General());
            list.AddRange(FacultyQuestions(facultyIndex));
            return list;
        }

        // ---- ข้อทั่วไป (ทุกคณะ) ----
        static ExamController.Question[] General() => new[]
        {
            new ExamController.Question("ข้อใดคือวิธีอ่านหนังสือที่ช่วยจำได้ดีที่สุด?",
                new[]{"อ่านรวดเดียวก่อนสอบ","ทบทวนซ้ำเป็นช่วง ๆ","ท่องแบบนกแก้ว","อ่านไปเล่นมือถือไป"}, 1),
            new ExamController.Question("การพักผ่อนเพียงพอมีผลต่อการเรียนอย่างไร?",
                new[]{"ไม่มีผล","ง่วงกว่าเดิม","ช่วยให้สมองจำและคิดได้ดีขึ้น","ทำให้ลืมทุกอย่าง"}, 2),
            new ExamController.Question("ข้อใดช่วยจัดการเวลาได้ดีที่สุด?",
                new[]{"ทำทุกอย่างนาทีสุดท้าย","วางแผน/ทำตารางล่วงหน้า","ไม่ต้องวางแผน","เลื่อนไปเรื่อย ๆ"}, 1),
            new ExamController.Question("GPA ย่อมาจากอะไร?",
                new[]{"Great Personal Attitude","Grade Point Average","General Public Award","Group Project Access"}, 1),
            new ExamController.Question("การเข้าเรียนสม่ำเสมอส่งผลอย่างไร?",
                new[]{"เสียเวลาเปล่า","สะสมความรู้ ไม่ตกหล่นเนื้อหา","ทำให้เกรดตก","ไม่มีผลใด ๆ"}, 1),
            new ExamController.Question("ก่อนวันสอบควรทำสิ่งใดเป็นอันดับแรก?",
                new[]{"นอนดึกสุด ๆ","ทบทวนสรุป + นอนให้พอ","เล่นเกมทั้งคืน","งดกินข้าว"}, 1),
        };

        // ---- ข้อเฉพาะคณะ ----
        static ExamController.Question[] FacultyQuestions(int i)
        {
            switch (i)
            {
                case 1: return Business();
                case 2: return Science();
                case 3: return Comm();
                default: return IT();
            }
        }

        static ExamController.Question[] IT() => new[]
        {
            new ExamController.Question("1 ไบต์ (Byte) เท่ากับกี่บิต?",
                new[]{"2 บิต","8 บิต","16 บิต","32 บิต"}, 1),
            new ExamController.Question("HTML ย่อมาจากอะไร?",
                new[]{"HyperText Markup Language","High Tech Modern Language","Hyper Transfer Main Link","Home Tool Markup Logic"}, 0),
            new ExamController.Question("อุปกรณ์ใดเป็น 'หน่วยความจำหลัก' ที่ใช้ทำงานชั่วคราว?",
                new[]{"ฮาร์ดดิสก์","RAM","การ์ดจอ","คีย์บอร์ด"}, 1),
            new ExamController.Question("คอมพิวเตอร์ทำงานด้วยเลขฐานอะไร?",
                new[]{"ฐาน 10","ฐาน 2 (ไบนารี)","ฐาน 8","ฐาน 16"}, 1),
            new ExamController.Question("ข้อใดคือ 'ระบบปฏิบัติการ' (OS)?",
                new[]{"Photoshop","Windows","Excel","Chrome"}, 1),
            new ExamController.Question("SQL ใช้ทำอะไรเป็นหลัก?",
                new[]{"จัดการฐานข้อมูล","ตกแต่งรูป","ตัดต่อวิดีโอ","พิมพ์เอกสาร"}, 0),
            new ExamController.Question("โครงสร้างข้อมูลแบบ 'เข้าก่อน–ออกก่อน' (FIFO) คือ?",
                new[]{"Stack","Queue","Array","Tree"}, 1),
            new ExamController.Question("ข้อใดเป็น 'ภาษาโปรแกรม'?",
                new[]{"HTTP","Python","HTML","USB"}, 1),
        };

        static ExamController.Question[] Business() => new[]
        {
            new ExamController.Question("4P ทางการตลาด ไม่รวมข้อใด?",
                new[]{"Product","Price","Place","People"}, 3),
            new ExamController.Question("'กำไร' คำนวณจากอะไร?",
                new[]{"รายได้ − ต้นทุน","รายได้ + ต้นทุน","ต้นทุน − รายได้","รายได้ × ภาษี"}, 0),
            new ExamController.Question("งบการเงินใดบอก 'สินทรัพย์–หนี้สิน–ทุน'?",
                new[]{"งบกำไรขาดทุน","งบดุล (งบแสดงฐานะการเงิน)","งบกระแสเงินสด","ใบเสร็จ"}, 1),
            new ExamController.Question("SWOT วิเคราะห์อะไร?",
                new[]{"จุดแข็ง-อ่อน-โอกาส-อุปสรรค","ยอดขายรายเดือน","ภาษี","สต๊อกสินค้า"}, 0),
            new ExamController.Question("'อุปสงค์' (Demand) หมายถึงอะไร?",
                new[]{"ความต้องการซื้อ","ปริมาณผลิต","ต้นทุนวัตถุดิบ","กำไรสุทธิ"}, 0),
            new ExamController.Question("ROI ย่อมาจากอะไร?",
                new[]{"Return On Investment","Rate Of Interest","Revenue Of Income","Risk Of Inflation"}, 0),
        };

        static ExamController.Question[] Science() => new[]
        {
            new ExamController.Question("น้ำ (H₂O) ประกอบด้วยธาตุใดบ้าง?",
                new[]{"ไฮโดรเจน + ออกซิเจน","คาร์บอน + ออกซิเจน","ไฮโดรเจน + ไนโตรเจน","ออกซิเจนอย่างเดียว"}, 0),
            new ExamController.Question("แรงโน้มถ่วงโลกมีค่าประมาณเท่าใด?",
                new[]{"9.8 m/s²","3.0 m/s²","1.6 m/s²","20 m/s²"}, 0),
            new ExamController.Question("ออร์แกเนลล์ใดสร้างพลังงานให้เซลล์?",
                new[]{"นิวเคลียส","ไมโทคอนเดรีย","ไรโบโซม","แวคิวโอล"}, 1),
            new ExamController.Question("สูตรพื้นที่วงกลมคือข้อใด?",
                new[]{"πr²","2πr","πd","r²"}, 0),
            new ExamController.Question("ธาตุใดมีสัญลักษณ์ 'O'?",
                new[]{"ทองคำ","ออกซิเจน","เหล็ก","โอโซน"}, 1),
            new ExamController.Question("สถานะของสสารพื้นฐานมีกี่สถานะ?",
                new[]{"2","3","4","5"}, 1),
        };

        static ExamController.Question[] Comm() => new[]
        {
            new ExamController.Question("PR ย่อมาจากอะไร?",
                new[]{"Public Relations","Private Report","Press Room","People Review"}, 0),
            new ExamController.Question("'5W1H' ใช้ในการทำอะไร?",
                new[]{"เขียนข่าว/รายงาน","คำนวณภาษี","เขียนโปรแกรม","ทดลองวิทยาศาสตร์"}, 0),
            new ExamController.Question("สื่อใดเป็น 'สื่อออนไลน์'?",
                new[]{"หนังสือพิมพ์","โซเชียลมีเดีย","วิทยุ AM","ป้ายโฆษณา"}, 1),
            new ExamController.Question("'กลุ่มเป้าหมาย' (Target Audience) คืออะไร?",
                new[]{"ผู้รับสารที่ต้องการสื่อถึง","ทีมงาน","งบประมาณ","อุปกรณ์ถ่ายทำ"}, 0),
            new ExamController.Question("ข้อใดคือหลักจริยธรรมสื่อที่สำคัญ?",
                new[]{"นำเสนอความจริง ไม่บิดเบือน","ปั้นข่าวให้ดraม่า","แอบถ่ายทุกคน","ลอกข่าวคนอื่น"}, 0),
            new ExamController.Question("การ 'ตรวจสอบข้อเท็จจริง' ก่อนเผยแพร่เรียกว่าอะไร?",
                new[]{"Fact-checking","Copy-paste","Broadcasting","Editing"}, 0),
        };
    }
}
