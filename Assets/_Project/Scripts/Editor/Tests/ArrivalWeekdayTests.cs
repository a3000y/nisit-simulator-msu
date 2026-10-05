#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Systems;
using NisitSimulator.SaveLoad;
using NisitSimulator.Academics;

namespace NisitSimulator.Tests
{
    public class ArrivalWeekdayTests
    {
        [Test]
        public void ArrivalClockLockCannotBeReleasedByExamUpdate()
        {
            bool previous = NisitSimulator.TimeSystem.GameClock.Suspended;
            try
            {
                NisitSimulator.TimeSystem.GameClock.ArrivalSuspended = true;
                NisitSimulator.TimeSystem.GameClock.Suspended = false;
                Assert.IsTrue(NisitSimulator.TimeSystem.GameClock.Suspended);
                NisitSimulator.TimeSystem.GameClock.ArrivalSuspended = false;
                Assert.IsFalse(NisitSimulator.TimeSystem.GameClock.Suspended);
                NisitSimulator.TimeSystem.GameClock.Suspended = true;
                NisitSimulator.TimeSystem.GameClock.ArrivalSuspended = false;
                Assert.IsTrue(NisitSimulator.TimeSystem.GameClock.Suspended);
            }
            finally
            {
                NisitSimulator.TimeSystem.GameClock.ArrivalSuspended = false;
                NisitSimulator.TimeSystem.GameClock.Suspended = previous;
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EveryTermStartsSunday_AndEveryDayHasCorrectWeekday(int term)
        {
            for (int day = 1; day <= AcademicCalendar.SemesterLen(term); day++)
            {
                int date = AcademicCalendar.DayInYear(term, day);
                Assert.AreEqual((DayOfWeek)((day - 1) % 7), AcademicCalendar.WeekdayOf(date));
                Assert.AreEqual(term < 2 && day == 1, AcademicCalendar.IsRegistrationDay(date));
                if (AcademicCalendar.IsExamDay(date)) Assert.IsFalse(AcademicCalendar.IsWeekend(date));
            }
        }
        [Test]
        public void AllClassSectionsHaveFourMeetings_WithoutWeekendOrExamClasses()
        {
            var cur = CsCurriculumDefaults.Create();
            var catalog = ClassroomDefaults.Create();
            try
            {
                foreach (var course in cur.courses)
                {
                    Assert.AreEqual(4, course.sessions.Count, course.code);
                    if (course.IsRequired) Assert.AreEqual(4, course.retakeSessions.Count, course.code);
                    foreach (var section in new[] { course.sessions, course.retakeSessions })
                        foreach (var session in section)
                        {
                            Assert.IsFalse(AcademicCalendar.IsWeekendSemesterDay(session.day), course.code);
                            Assert.AreNotEqual(6, session.day, course.code);
                            Assert.AreNotEqual(13, session.day, course.code);
                        }
                }
                Assert.IsEmpty(RegistrationService.FindRoomConflicts(cur, catalog, true));
            }
            finally { UnityEngine.Object.DestroyImmediate(cur); UnityEngine.Object.DestroyImmediate(catalog); }
        }
        [TestCase(2)] [TestCase(8)] [TestCase(14)]
        public void RegistrationClosesAfterFirstSunday_WithoutAutoConfirm(int day)
        {
            var cur = CsCurriculumDefaults.Create();
            try
            {
                var service = new RegistrationService(cur, new AcademicRecord());
                service.OpenTerm(1, 0);
                Assert.IsTrue(service.Add("CS101", out var reason), reason);
                service.CheckRegistrationDeadline(day, out _);
                Assert.IsFalse(service.RegistrationWindowOpen(day));
                Assert.IsFalse(service.Confirm(out reason));
                Assert.IsFalse(service.Record.Current.confirmed);
            }
            finally { UnityEngine.Object.DestroyImmediate(cur); }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(5)] [TestCase(9)] [TestCase(12)] [TestCase(13)]
        public void ArrivalStateSurvivesJson_AndInterruptedCarResumesGreeting(int step)
        {
            var state = new ArrivalProgress { Step = step, QuestRewarded = step >= 12 };
            var data = new SaveData { calendarVersion = AcademicCalendar.Version };
            state.Collect(data);
            var restored = ArrivalProgress.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)));
            Assert.AreEqual(Math.Max(1, step), restored.Step);
            Assert.IsFalse(restored.Done);
            Assert.AreEqual(state.QuestRewarded, restored.QuestRewarded);
        }
        [Test]
        public void LegacySavesSkipArrival_NewGamePlays_CompletedSaveNeverRepeats()
        {
            Assert.IsTrue(ArrivalProgress.FromSave(new SaveData()).Done);
            Assert.AreEqual(0, ArrivalProgress.FromSave(null).Step);
            Assert.IsFalse(ArrivalProgress.FromSave(null).Done);
            var data = new SaveData();
            new ArrivalProgress { Step = 14, Done = true, QuestRewarded = true }.Collect(data);
            var restored = ArrivalProgress.FromSave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)));
            Assert.IsTrue(restored.Done); Assert.IsTrue(restored.QuestRewarded);
        }
        [Test]
        public void V2MigrationPreservesTicksAndRewards_MapsDates_AndIsIdempotent()
        {
            for (int oldDay = 1; oldDay <= 23; oldDay++)
            {
                var record = new AcademicRecord { hasCurrent = true, current = new TermState { serial = 1, confirmed = true } };
                var enrollment = new Enrollment { code = "CS101", termSerial = 1, progress = 3.5f };
                enrollment.AddTick("6:2"); ClassEventLogic.SetState(enrollment, "6:2", ClassEventLogic.Done);
                record.enrollments.Add(enrollment);
                var data = new SaveData { calendarVersion = 2, dayInYear = oldDay, gameDay = 40, academic = record, hasAcademicRecord = true };
                Assert.IsTrue(CalendarMigration.Upgrade(data));
                int term = oldDay <= 10 ? 0 : oldDay <= 20 ? 1 : 2;
                int expected = term == 2 ? oldDay + 8 : AcademicCalendar.DayInYear(term, CalendarMigration.MapV2TermDay(oldDay - term * 10));
                Assert.AreEqual(expected, data.dayInYear);
                Assert.AreEqual(40, data.gameDay);
                Assert.AreEqual("9:2", enrollment.meetingKeys[0]);
                StringAssert.StartsWith("9:2", enrollment.classEventKeys[0]);
                Assert.AreEqual(1, enrollment.meetingTicks[0]); Assert.AreEqual(3.5f, enrollment.progress);
                Assert.IsTrue(data.arrivalIntroDone);
                Assert.IsFalse(CalendarMigration.Upgrade(data)); Assert.AreEqual(expected, data.dayInYear);
            }
        }
        [Test]
        public void V2UnconfirmedFirstDayStaysSunday()
        {
            var data = new SaveData { calendarVersion = 2, dayInYear = 11 };
            CalendarMigration.Upgrade(data);
            Assert.AreEqual(15, data.dayInYear);
            Assert.IsTrue(AcademicCalendar.IsRegistrationDay(data.dayInYear));
        }
    }
}
#endif
