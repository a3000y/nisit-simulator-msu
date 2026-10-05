using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Characters;
using NisitSimulator.Systems;

namespace NisitSimulator.UI
{
    // Uses the existing phone canvas/font/navigation. Two profiles per page remain readable.
    public class StudentIdentityPhone : MonoBehaviour
    {
        const int PageSize = 2;
        PhoneController phone;
        int page;
        GameObject previous, next;
        RectTransform body;
        Vector2 originalBodySize;
        bool bodyReserved;

        public static StudentIdentityPhone EnsureOn(PhoneController phone)
        {
            var ui = phone.GetComponent<StudentIdentityPhone>();
            if (ui == null) ui = phone.gameObject.AddComponent<StudentIdentityPhone>();
            if (ui.phone == null) { ui.phone = phone; ui.Build(); }
            return ui;
        }
        void Build()
        {
            if (phone.homeView == null || phone.appButtons == null || phone.appButtons.Length == 0 || phone.appButtons[0] == null) return;
            var template = phone.appButtons[0];
            var card = Instantiate(template.gameObject, phone.homeView.transform);
            card.name = "StudentCardApp";
            ((RectTransform)card.transform).anchoredPosition = new Vector2(0, -256);
            var label = card.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) { label.text = "บัตรนิสิต"; UIFit.OneLine(label, label.fontSize, 18); }
            var button = card.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => phone.OpenApp((int)PhoneController.App.StudentCard));
            card.SetActive(true);
            if (phone.appCard == null || phone.appBody == null || phone.backButton == null) return;
            body = phone.appBody.rectTransform; originalBodySize = body.sizeDelta;
            previous = PageButton("IdentityPrevious", "ก่อนหน้า", -94, -1);
            next = PageButton("IdentityNext", "ถัดไป", 94, 1);
            previous.SetActive(false); next.SetActive(false);
        }
        GameObject PageButton(string name, string text, float x, int change)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(phone.appCard.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(156, 44); rt.anchoredPosition = new Vector2(x, 12);
            var background = go.GetComponent<Image>();
            background.sprite = phone.appButtons[0].GetComponent<Image>()?.sprite;
            background.type = Image.Type.Sliced; background.color = new Color(0.30f, 0.34f, 0.50f);
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.font = phone.appBody.font; label.text = text; label.fontSize = 22;
            label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
            UIFit.OneLine(label, 22, 18);
            var b = go.GetComponent<Button>(); b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(() => { page += change; phone.OpenApp((int)PhoneController.App.Friends); });
            return go;
        }
        public void Configure(PhoneController.App app)
        {
            bool friends = app == PhoneController.App.Friends;
            if (body != null && friends != bodyReserved)
            {
                body.sizeDelta = originalBodySize - (friends ? new Vector2(0, 64) : Vector2.zero);
                bodyReserved = friends;
            }
            if (previous != null) previous.SetActive(friends);
            if (next != null) next.SetActive(friends);
        }
        static string Safe(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
        public string CardText()
        {
            var runtime = CharacterRegistryRuntime.EnsureExists(); runtime.Initialize();
            var p = runtime.Player;
            return $"<b>{Safe(p.displayName)}</b>\n\n" +
                $"เลขนิสิต\n<b>{p.FormattedStudentNumber}</b>\n\n" +
                $"คณะ {FacultyCatalog.NameOf(p.facultyIndex)}\n" +
                $"ชั้นปี {p.classYear}\nปีเข้าเรียน {p.admissionYear}\n" +
                $"รหัสสาขาที่เข้าเรียน {p.admissionProgramCode:000}";
        }
        public string FriendsText()
        {
            var runtime = CharacterRegistryRuntime.EnsureExists(); runtime.Initialize();
            var rel = RelationshipManager.Instance;
            var ids = new List<string>(rel.AllIds);
            ids.Sort((a, b) => { int points = rel.GetPoints(b).CompareTo(rel.GetPoints(a)); return points != 0 ? points : string.CompareOrdinal(a, b); });
            if (ids.Count == 0)
            {
                page = 0;
                if (previous != null) previous.GetComponent<Button>().interactable = false;
                if (next != null) next.GetComponent<Button>().interactable = false;
                return "ยังไม่รู้จักใครเลย\nลองเดินไปทักทาย NPC (กด E) ดูสิ!";
            }
            int pages = Mathf.CeilToInt(ids.Count / (float)PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            if (previous != null) previous.GetComponent<Button>().interactable = page > 0;
            if (next != null) next.GetComponent<Button>().interactable = page < pages - 1;
            var sb = new StringBuilder();
            sb.AppendLine($"<size=80%>รู้จัก {ids.Count} คน · เพื่อน {rel.FriendCount} คน\nหน้า {page + 1}/{pages}</size>\n");
            for (int i = page * PageSize; i < Mathf.Min(ids.Count, (page + 1) * PageSize); i++)
            {
                string id = ids[i]; int points = rel.GetPoints(id), level = RelationshipManager.LevelOf(points);
                var profile = runtime.Registry.FindByRelationship(id);
                sb.AppendLine($"<b>{Safe(rel.DisplayName(id))}</b> <color=#FF7BA6>{RelationshipManager.Hearts(level)}</color>");
                if (profile != null)
                {
                    if (profile.IsStudent) sb.AppendLine($"<size=78%>ปี {profile.classYear} · {FacultyCatalog.NameOf(profile.facultyIndex)}\nเลขนิสิต {profile.FormattedStudentNumber}</size>");
                    else sb.AppendLine($"<size=78%>{CharacterRegistryRuntime.RoleText(profile.role)}</size>");
                }
                sb.AppendLine($"<size=76%><color=#625079>{RelationshipManager.NameOfLevel(level)} ({points})</color></size>\n");
            }
            return sb.ToString();
        }
    }
}
