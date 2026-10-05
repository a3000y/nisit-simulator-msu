#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Characters;
using NisitSimulator.SaveLoad;

namespace NisitSimulator.Tests
{
    public class StudentIdentityTests
    {
        static CharacterProfile Student(StudentRegistry r, string id, int faculty = 0, int year = 2569, CharacterRole role = CharacterRole.Student)
            => r.Register(id, "นิสิต " + id, role, year, faculty, 1, "relationship:" + id);

        [Test] public void PlayerAndNpcShareOneUniqueNumberLedger()
        {
            var r = new StudentRegistry(); var seen = new HashSet<string>();
            seen.Add(Student(r, StudentRegistry.PlayerId, role: CharacterRole.PlayerStudent).studentNumber);
            for (int i = 0; i < 100; i++) Assert.IsTrue(seen.Add(Student(r, "npc" + i).studentNumber));
            Assert.AreEqual("69011210001", r.Get(StudentRegistry.PlayerId).studentNumber);
            Assert.AreEqual("69-01-121-0001", r.Get(StudentRegistry.PlayerId).FormattedStudentNumber);
        }
        [Test] public void SamePersonKeepsIdentityNumberAndRelationshipAfterRenameAndPromotion()
        {
            var r = new StudentRegistry(); var p = Student(r, "npc"); string number = p.studentNumber;
            r.Rename("npc", "ชื่อใหม่"); r.UpdateStudent("npc", 4, 2);
            var existing = r.Register("npc", "ชื่อจากฉาก", CharacterRole.Student, 2575, 3, 1, "new-key");
            Assert.AreSame(p, existing); Assert.AreEqual(number, existing.studentNumber);
            Assert.AreEqual("relationship:npc", existing.relationshipId); Assert.AreEqual("ชื่อใหม่", existing.displayName);
            Assert.AreEqual(2569, existing.admissionYear); Assert.AreEqual(1, existing.admissionFacultyCode);
            Assert.AreEqual(4, existing.classYear); Assert.AreEqual(2, existing.facultyIndex);
        }
        [Test] public void JsonSaveRoundTripPreservesNamesNumbersWorldAndNextSequence()
        {
            var r = new StudentRegistry(); Student(r, "one"); Student(r, "two"); r.Rename("one", "ภาคิน");
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { studentRegistry = r.Snapshot() }));
            var loaded = new StudentRegistry(saved.studentRegistry);
            Assert.AreEqual(r.WorldId, loaded.WorldId); Assert.AreEqual("ภาคิน", loaded.Get("one").displayName);
            Assert.AreEqual(r.Get("two").studentNumber, loaded.Get("two").studentNumber);
            Assert.AreEqual("69011210003", Student(loaded, "three").studentNumber);
        }
        [Test] public void LegacySaveUpgradesOnceWithoutChangingRelationshipOrAcademicData()
        {
            var saved = JsonUtility.FromJson<SaveData>("{\"currentYear\":3,\"money\":999,\"relIds\":[\"พี่ต้น_22_-43\"],\"relPoints\":[90]}");
            // Unity JsonUtility may create an empty nested object for an absent legacy field.
            Assert.IsTrue(saved.studentRegistry == null || saved.studentRegistry.version == 0);
            var r = new StudentRegistry(saved.studentRegistry);
            var p = r.Register("npc:permanent", "พี่ต้น", CharacterRole.Student, 2567, 0, 3, saved.relIds[0]);
            saved.studentRegistry = r.Snapshot();
            var loaded = new StudentRegistry(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)).studentRegistry);
            var again = loaded.Register("npc:permanent", "พี่ต้น", CharacterRole.Student, 2569, 0, 1);
            Assert.AreEqual(p.studentNumber, again.studentNumber); Assert.AreEqual(saved.relIds[0], again.relationshipId);
            Assert.AreEqual(1, loaded.Profiles.Count); Assert.AreEqual(999, saved.money); Assert.AreEqual(90, saved.relPoints[0]);
        }
        [TestCase(CharacterRole.Teacher)] [TestCase(CharacterRole.Staff)] [TestCase(CharacterRole.Vendor)]
        public void NonStudentsHavePermanentIdentityWithoutConsumingStudentNumbers(CharacterRole role)
        {
            var r = new StudentRegistry(); var p = r.Register("staff", "อาจารย์", role, 0, 0, 1);
            Assert.IsFalse(p.IsStudent); Assert.AreEqual("", p.studentNumber);
            Assert.AreEqual("69011210001", Student(r, "student").studentNumber);
        }
        [Test] public void DifferentFacultiesAndAdmissionYearsHaveIndependentRanges()
        {
            var r = new StudentRegistry(); var seen = new HashSet<string>();
            for (int f = 0; f < 4; f++)
                for (int y = 2566; y <= 2569; y++) Assert.IsTrue(seen.Add(Student(r, f + "_" + y, f, y).studentNumber));
            Assert.AreEqual(16, seen.Count);
        }
        [Test] public void MissingCountersAreReconstructedFromExistingNumbers()
        {
            var r = new StudentRegistry(); Student(r, "one"); Student(r, "two");
            var saved = r.Snapshot(); saved.counters = null;
            Assert.AreEqual("69011210003", Student(new StudentRegistry(saved), "three").studentNumber);
        }
        [Test] public void RemovedProfilesNeverRecycleAllocatedNumbers()
        {
            var r = new StudentRegistry(); Student(r, "one"); Student(r, "two");
            var saved = r.Snapshot(); saved.profiles.Clear();
            Assert.AreEqual("69011210003", Student(new StudentRegistry(saved), "new").studentNumber);
        }
        [Test] public void SnapshotCannotMutateLiveRegistry()
        {
            var r = new StudentRegistry(); var p = Student(r, "one");
            var saved = r.Snapshot(); saved.profiles[0].displayName = "changed"; saved.counters[0].lastSequence = 100;
            Assert.AreEqual("นิสิต one", p.displayName); Assert.AreEqual("69011210002", Student(r, "two").studentNumber);
        }
        [Test] public void DuplicateIdsAndNumbersAreRejectedRatherThanReassigned()
        {
            var r = new StudentRegistry(); Student(r, "one"); Student(r, "two");
            var saved = r.Snapshot(); saved.profiles[1].characterId = saved.profiles[0].characterId;
            Assert.Throws<InvalidOperationException>(() => new StudentRegistry(saved));
            saved = r.Snapshot(); saved.profiles[1].studentNumber = saved.profiles[0].studentNumber;
            Assert.Throws<InvalidOperationException>(() => new StudentRegistry(saved));
        }
        [Test] public void FullRangeFailsWithoutCreatingAProfileOrWrappingSequence()
        {
            var saved = new StudentRegistrySave(); saved.counters.Add(new StudentNumberCounter { prefix = "6901121", lastSequence = 9999 });
            var r = new StudentRegistry(saved);
            Assert.Throws<InvalidOperationException>(() => Student(r, "overflow")); Assert.IsNull(r.Get("overflow"));
        }
        [Test] public void LeadingZeroAdmissionYearIsStoredAsElevenDigits()
        {
            var p = Student(new StudentRegistry(), "zero", year: 2500);
            Assert.AreEqual("00011210001", p.studentNumber); Assert.AreEqual("00-01-121-0001", p.FormattedStudentNumber);
        }
        [Test] public void UnsupportedFutureSaveAndInvalidFacultyAreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => new StudentRegistry(new StudentRegistrySave { version = 999 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Student(new StudentRegistry(), "bad", faculty: 4));
        }
    }
}
#endif
