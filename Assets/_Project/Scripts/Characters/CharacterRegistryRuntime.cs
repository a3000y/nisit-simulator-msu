using System;
using System.Linq;
using UnityEngine;
using NisitSimulator.Academics;
using NisitSimulator.Interaction;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;
using Object = UnityEngine.Object;

namespace NisitSimulator.Characters
{
    // Scene-local runtime. Can initialize before GameplayBootstrap.Start when NPC Start order differs.
    public class CharacterRegistryRuntime : MonoBehaviour
    {
        public static CharacterRegistryRuntime Instance { get; private set; }
        public StudentRegistry Registry { get; private set; }
        public CharacterProfile Player => Registry?.Get(StudentRegistry.PlayerId);
        bool activated;
        ProgressionManager progression;

        public static CharacterRegistryRuntime EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CharacterRegistry");
            return go.AddComponent<CharacterRegistryRuntime>();
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        public void Initialize(SaveData loaded = null)
        {
            if (Registry != null) return;
            if (loaded == null && (GameSession.PendingLoad || GameSession.IsContinue)) loaded = SaveSystem.Load();
            Registry = new StudentRegistry(loaded?.studentRegistry);
            int faculty = Mathf.Clamp(loaded != null ? loaded.facultyIndex : GameSession.SelectedFacultyIndex, 0, 3);
            int year = Mathf.Clamp(loaded != null ? loaded.currentYear : 1, 1, 4);
            if (loaded != null && loaded.hasAcademicRecord && loaded.academic != null) year = Mathf.Clamp(loaded.academic.classYear, 1, 4);
            bool hadPlayer = Registry.Get(StudentRegistry.PlayerId) != null;
            var player = Registry.Register(StudentRegistry.PlayerId, string.IsNullOrWhiteSpace(GameSession.PlayerName) ? "นิสิตใหม่" : GameSession.PlayerName,
                CharacterRole.PlayerStudent, Registry.StartingAcademicYear, faculty, year);
            if (hadPlayer) GameSession.PlayerName = player.displayName;
            progression = Object.FindAnyObjectByType<ProgressionManager>();
            int elapsedYear = loaded != null ? Math.Max(1, loaded.calendarYear > 0 ? loaded.calendarYear : loaded.currentYear) : 1;
            int academicYear = Registry.StartingAcademicYear + elapsedYear - 1;
            // Stable ordering gives repeatable allocation during a legacy-save migration.
            var npcs = Object.FindObjectsByType<TalkNPC>(FindObjectsInactive.Include)
                .OrderBy(n => n.GetComponent<CharacterIdentity>()?.characterId ?? CharacterIdentity.SceneKey(n.transform), StringComparer.Ordinal).ToArray();
            foreach (var npc in npcs) BindNpc(npc, academicYear, faculty);
            var pgo = GameObject.Find("Player");
            if (pgo != null)
            {
                var binding = pgo.GetComponent<CharacterIdentity>() ?? pgo.AddComponent<CharacterIdentity>();
                binding.characterId = StudentRegistry.PlayerId; binding.role = CharacterRole.PlayerStudent;
            }
        }
        public void Activate(SaveData loaded)
        {
            Initialize(loaded); activated = true; RefreshPlayer();
        }
        public CharacterProfile RegisterNpc(TalkNPC npc)
        {
            Initialize();
            int academicYear = Registry.StartingAcademicYear + (progression != null ? Math.Max(0, progression.CalendarYear - 1) : 0);
            return BindNpc(npc, academicYear, GameSession.SelectedFacultyIndex);
        }
        CharacterProfile BindNpc(TalkNPC npc, int academicYear, int playerFaculty)
        {
            var binding = npc.GetComponent<CharacterIdentity>();
            if (binding == null) { binding = npc.gameObject.AddComponent<CharacterIdentity>(); binding.ConfigureFrom(npc); }
            if (string.IsNullOrEmpty(binding.characterId)) binding.characterId = !string.IsNullOrEmpty(npc.stableRelationshipId) ? "npc:" + npc.stableRelationshipId : CharacterIdentity.SceneKey(npc.transform);
            if (string.IsNullOrEmpty(binding.legacyRelationshipId)) binding.legacyRelationshipId = !string.IsNullOrEmpty(npc.stableRelationshipId) ? npc.stableRelationshipId : CharacterIdentity.LegacyKey(npc);
            int classYear = Mathf.Clamp(binding.initialClassYear, 1, 4);
            int faculty = Mathf.Clamp(binding.facultyIndex < 0 ? playerFaculty : binding.facultyIndex, 0, 3);
            var p = Registry.Register(binding.characterId, npc.npcName, binding.role, academicYear - classYear + 1, faculty, classYear, binding.legacyRelationshipId);
            npc.npcName = p.displayName;
            return p;
        }
        void Update() { if (activated) RefreshPlayer(); }
        void RefreshPlayer()
        {
            if (Player == null) return;
            if (progression == null) progression = Object.FindAnyObjectByType<ProgressionManager>();
            int year = CourseRegistrar.Active ? CourseRegistrar.Instance.ClassYear : progression != null ? progression.CurrentYear : Player.classYear;
            Registry.UpdateStudent(StudentRegistry.PlayerId, Mathf.Clamp(year, 1, 4), Mathf.Clamp(GameSession.SelectedFacultyIndex, 0, 3));
            if (!string.IsNullOrWhiteSpace(GameSession.PlayerName) && GameSession.PlayerName != Player.displayName) Registry.Rename(StudentRegistry.PlayerId, GameSession.PlayerName);
        }
        public void CollectSave(SaveData data)
        {
            Initialize(); RefreshPlayer(); data.studentRegistry = Registry.Snapshot();
        }
        public static string RoleText(CharacterRole role)
        {
            switch (role) { case CharacterRole.PlayerStudent: case CharacterRole.Student: return "นิสิต"; case CharacterRole.Teacher: return "อาจารย์"; case CharacterRole.Staff: return "บุคลากร"; default: return "ผู้ขาย"; }
        }
    }
}
