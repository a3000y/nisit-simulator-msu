using System;
using System.Collections.Generic;
using System.Globalization;

namespace NisitSimulator.Characters
{
    public enum CharacterRole { PlayerStudent, Student, Teacher, Staff, Vendor }

    [Serializable]
    public class CharacterProfile
    {
        public string characterId;
        public string displayName;
        public CharacterRole role;
        public string relationshipId;
        public string studentNumber = "";
        public int admissionYear;
        public int admissionFacultyCode;
        public int admissionProgramCode;
        public int facultyIndex;
        public int classYear = 1;
        public bool IsStudent => role == CharacterRole.PlayerStudent || role == CharacterRole.Student;
        public string FormattedStudentNumber => StudentRegistry.DisplayNumber(studentNumber);
        public CharacterProfile Copy() => (CharacterProfile)MemberwiseClone();
    }

    [Serializable]
    public class StudentNumberCounter
    {
        public string prefix;
        public int lastSequence;
        public StudentNumberCounter Copy() => (StudentNumberCounter)MemberwiseClone();
    }

    [Serializable]
    public class StudentRegistrySave
    {
        public int version;
        public string worldId;
        public int startingAcademicYear = 2569;
        public List<CharacterProfile> profiles = new List<CharacterProfile>();
        public List<StudentNumberCounter> counters = new List<StudentNumberCounter>();
    }

    // One ledger per save world. Names and class years never participate in identity allocation.
    public sealed class StudentRegistry
    {
        public const int Version = 1;
        public const int DefaultStartingYear = 2569;
        public const string PlayerId = "player";
        readonly StudentRegistrySave data;
        readonly Dictionary<string, CharacterProfile> byId = new Dictionary<string, CharacterProfile>(StringComparer.Ordinal);
        readonly HashSet<string> usedNumbers = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, StudentNumberCounter> counters = new Dictionary<string, StudentNumberCounter>(StringComparer.Ordinal);
        public IReadOnlyList<CharacterProfile> Profiles => data.profiles;
        public int StartingAcademicYear => data.startingAcademicYear;
        public string WorldId => data.worldId;

        public StudentRegistry(StudentRegistrySave saved = null)
        {
            if (saved != null && saved.version > Version) throw new InvalidOperationException("Unsupported student registry version.");
            data = new StudentRegistrySave
            {
                version = Version,
                worldId = string.IsNullOrEmpty(saved?.worldId) ? Guid.NewGuid().ToString("N") : saved.worldId,
                startingAcademicYear = saved != null && saved.startingAcademicYear >= 1000 ? saved.startingAcademicYear : DefaultStartingYear
            };
            if (saved?.counters != null)
                foreach (var entry in saved.counters)
                {
                    if (entry == null || !IsDigits(entry.prefix, 7) || entry.lastSequence < 0 || entry.lastSequence > 9999)
                        throw new InvalidOperationException("Invalid student number counter.");
                    var c = Counter(entry.prefix);
                    c.lastSequence = Math.Max(c.lastSequence, entry.lastSequence);
                }
            if (saved?.profiles == null) return;
            foreach (var original in saved.profiles)
            {
                if (original == null || string.IsNullOrWhiteSpace(original.characterId) || byId.ContainsKey(original.characterId))
                    throw new InvalidOperationException("Missing or duplicate character identity.");
                var p = original.Copy();
                if (p.IsStudent)
                {
                    if (!IsDigits(p.studentNumber, 11) || !usedNumbers.Add(p.studentNumber))
                        throw new InvalidOperationException("Missing or duplicate student number.");
                    string expected = Prefix(p.admissionYear, p.admissionFacultyCode, p.admissionProgramCode);
                    if (!p.studentNumber.StartsWith(expected, StringComparison.Ordinal))
                        throw new InvalidOperationException("Student number does not match admission information.");
                    int sequence = int.Parse(p.studentNumber.Substring(7), CultureInfo.InvariantCulture);
                    if (sequence < 1) throw new InvalidOperationException("Invalid student sequence.");
                    var c = Counter(expected);
                    c.lastSequence = Math.Max(c.lastSequence, sequence);
                }
                else if (!string.IsNullOrEmpty(p.studentNumber)) throw new InvalidOperationException("Non-student has a student number.");
                data.profiles.Add(p);
                byId.Add(p.characterId, p);
            }
        }

        public CharacterProfile Get(string characterId) => characterId != null && byId.TryGetValue(characterId, out var p) ? p : null;
        public CharacterProfile FindByRelationship(string relationshipId)
        {
            if (string.IsNullOrEmpty(relationshipId)) return null;
            foreach (var p in data.profiles) if (p.relationshipId == relationshipId) return p;
            return null;
        }

        public CharacterProfile Register(string id, string name, CharacterRole role, int admissionYear, int facultyIndex, int classYear, string relationshipId = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A permanent character ID is required.", nameof(id));
            var existing = Get(id);
            if (existing != null) return existing;
            if (facultyIndex < 0 || facultyIndex > 3) throw new ArgumentOutOfRangeException(nameof(facultyIndex));
            if (classYear < 1 || classYear > 4) throw new ArgumentOutOfRangeException(nameof(classYear));
            if (!Enum.IsDefined(typeof(CharacterRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            var p = new CharacterProfile { characterId = id, displayName = string.IsNullOrWhiteSpace(name) ? "นิสิต" : name.Trim(), role = role,
                relationshipId = relationshipId ?? "", facultyIndex = facultyIndex, classYear = classYear };
            if (p.IsStudent)
            {
                p.admissionYear = admissionYear;
                p.admissionFacultyCode = FacultyCode(facultyIndex);
                p.admissionProgramCode = ProgramCode(facultyIndex);
                string prefix = Prefix(admissionYear, p.admissionFacultyCode, p.admissionProgramCode);
                var counter = Counter(prefix);
                int sequence = counter.lastSequence;
                string number;
                do
                {
                    if (++sequence > 9999) throw new InvalidOperationException("Student number range is full.");
                    number = prefix + sequence.ToString("D4", CultureInfo.InvariantCulture);
                } while (usedNumbers.Contains(number));
                counter.lastSequence = sequence;
                p.studentNumber = number;
                usedNumbers.Add(number);
            }
            data.profiles.Add(p); byId.Add(id, p);
            return p;
        }

        public void UpdateStudent(string id, int classYear, int facultyIndex)
        {
            var p = Get(id) ?? throw new ArgumentException("Unknown character.", nameof(id));
            if (!p.IsStudent) return;
            if (classYear < 1 || classYear > 4 || facultyIndex < 0 || facultyIndex > 3) throw new ArgumentOutOfRangeException();
            p.classYear = classYear; p.facultyIndex = facultyIndex;
        }
        public void Rename(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
            var p = Get(id) ?? throw new ArgumentException("Unknown character.", nameof(id));
            p.displayName = name.Trim();
        }
        public StudentRegistrySave Snapshot()
        {
            var copy = new StudentRegistrySave { version = Version, worldId = data.worldId, startingAcademicYear = data.startingAcademicYear };
            foreach (var p in data.profiles) copy.profiles.Add(p.Copy());
            foreach (var c in data.counters) copy.counters.Add(c.Copy());
            return copy;
        }
        StudentNumberCounter Counter(string prefix)
        {
            if (!counters.TryGetValue(prefix, out var c)) { c = new StudentNumberCounter { prefix = prefix }; counters.Add(prefix, c); data.counters.Add(c); }
            return c;
        }
        // Fictional game codes, deliberately independent of the faculty array index.
        public static int FacultyCode(int index) => index >= 0 && index < 4 ? index + 1 : throw new ArgumentOutOfRangeException(nameof(index));
        public static int ProgramCode(int index)
        {
            switch (index) { case 0: return 121; case 1: return 201; case 2: return 301; case 3: return 401; default: throw new ArgumentOutOfRangeException(nameof(index)); }
        }
        static string Prefix(int year, int faculty, int program)
        {
            if (year < 1000 || year > 9999 || faculty < 1 || faculty > 99 || program < 1 || program > 999) throw new ArgumentOutOfRangeException();
            return (year % 100).ToString("D2", CultureInfo.InvariantCulture) + faculty.ToString("D2", CultureInfo.InvariantCulture) + program.ToString("D3", CultureInfo.InvariantCulture);
        }
        static bool IsDigits(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char c in value) if (c < '0' || c > '9') return false;
            return true;
        }
        public static string DisplayNumber(string value) => IsDigits(value, 11) ? value.Substring(0, 2) + "-" + value.Substring(2, 2) + "-" + value.Substring(4, 3) + "-" + value.Substring(7, 4) : "";
    }
}
