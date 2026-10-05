using System.Text;
using UnityEngine;
using NisitSimulator.Interaction;

namespace NisitSimulator.Characters
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Nisit/Characters/Character Identity")]
    public class CharacterIdentity : MonoBehaviour
    {
        [Tooltip("Permanent authored identity. Keep it when moving or renaming this NPC.")]
        public string characterId = "";
        public CharacterRole role = CharacterRole.Student;
        [Range(1, 4)] public int initialClassYear = 1;
        [Tooltip("-1 = use the player's faculty for a classmate.")]
        public int facultyIndex = 0;
        [Tooltip("Relationship key from before the identity system. Kept to preserve old saves.")]
        public string legacyRelationshipId = "";
        public CharacterProfile Profile => CharacterRegistryRuntime.Instance?.Registry?.Get(characterId);

        public void ConfigureFrom(TalkNPC npc)
        {
            string name = npc.npcName ?? "";
            role = npc.isVendor ? CharacterRole.Vendor : name.Contains("บรรณารักษ์") ? CharacterRole.Staff : name.Contains("อาจารย์") ? CharacterRole.Teacher : CharacterRole.Student;
            initialClassYear = name.Contains("ปี 4") || name.Contains("ใกล้จบ") ? 4 : name.Contains("ปี 3") ? 3 : name.Contains("ปี 2") ? 2 : 1;
            facultyIndex = name.Contains("เพื่อนร่วมคณะ") ? -1 : 0;
            legacyRelationshipId = !string.IsNullOrEmpty(npc.stableRelationshipId) ? npc.stableRelationshipId : LegacyKey(npc);
        }
        public static string LegacyKey(TalkNPC npc) => $"{npc.npcName}_{Mathf.RoundToInt(npc.transform.position.x)}_{Mathf.RoundToInt(npc.transform.position.z)}";
        // Runtime fallback for dynamically built NPCs. Scene NPCs receive authored IDs from the setup tool.
        public static string SceneKey(Transform target)
        {
            var path = new StringBuilder();
            for (var t = target; t != null; t = t.parent) path.Insert(0, "/" + t.name + "[" + t.GetSiblingIndex() + "]");
            return "scene:" + target.gameObject.scene.path + path;
        }
    }
}
