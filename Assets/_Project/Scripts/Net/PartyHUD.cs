using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    public sealed class PartyHUD : MonoBehaviour
    {
        public static readonly Color[] TeamColors = {
            new Color(0.25f, 0.53f, 0.85f), new Color(0.82f, 0.36f, 0.50f),
            new Color(0.25f, 0.66f, 0.46f), new Color(0.76f, 0.51f, 0.16f) };
        public static Color MemberColor(byte slot) => slot < TeamColors.Length ? TeamColors[slot] : GrowthUI.Soft;
        public static TMP_FontAsset ThaiFont()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font.name.Contains("NisitThai")) return font;
            return GrowthUI.Font;
        }
        sealed class Row { public GameObject Root; public Image Dot, Fill; public TMP_Text Name, Status; }
        readonly Row[] rows = new Row[4];
        TMP_Text heading;
        public static PartyHUD Create(Transform parent)
        {
            var canvas = GrowthUI.MakeCanvas(parent, "Party HUD", 12);
            var ui = canvas.gameObject.AddComponent<PartyHUD>(); ui.Build(); return ui;
        }
        void Build()
        {
            var panel = GrowthUI.Box(transform, "PartyCard", new Vector2(0, 0), new Vector2(18, 140), new Vector2(310, 350), PastelTheme.CardCol);
            panel.raycastTarget = false;
            heading = Label(panel.transform, "ทีม", new Vector2(14, -10), new Vector2(282, 30), 23);
            for (int i = 0; i < rows.Length; i++)
            {
                var card = GrowthUI.Box(panel.transform, "Member", new Vector2(0, 1), new Vector2(10, -48 - i * 65), new Vector2(290, 60), new Color(1, 1, 1, 0.55f), false);
                card.raycastTarget = false;
                var dot = GrowthUI.Box(card.transform, "Swatch", new Vector2(0, 1), new Vector2(8, -8), new Vector2(14, 14), TeamColors[i], false); dot.raycastTarget = false;
                var name = Label(card.transform, "", new Vector2(28, -3), new Vector2(254, 25), 19);
                var status = Label(card.transform, "", new Vector2(8, -24), new Vector2(274, 23), 15);
                var bg = GrowthUI.Box(card.transform, "EnergyTrack", new Vector2(0, 1), new Vector2(8, -49), new Vector2(274, 6), GrowthUI.Strip, false); bg.raycastTarget = false;
                var fill = GrowthUI.Box(bg.transform, "Energy", Vector2.zero, Vector2.zero, new Vector2(274, 6), TeamColors[i], false); fill.raycastTarget = false;
                rows[i] = new Row { Root = card.gameObject, Dot = dot, Fill = fill, Name = name, Status = status };
            }
            Label(panel.transform, "P แล้วคลิกแผนที่เพื่อปักหมุด 10 วินาที", new Vector2(14, -316), new Vector2(282, 25), 15);
        }
        static TMP_Text Label(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize)
        {
            var t = GrowthUI.Text(parent, text, pos, size, fontSize, PastelTheme.TextDark, TextAlignmentOptions.Left);
            t.font = ThaiFont(); t.richText = false; t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = pos;
            return t;
        }
        public void Refresh(PartyRuntime party)
        {
            heading.text = "ทีม " + party.Members.Count + "/4";
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; bool valid = i < party.Members.Count && party.Members[i] != null;
                row.Root.SetActive(valid); if (!valid) continue;
                var av = party.Members[i]; var s = av.TeamSummary;
                row.Name.text = (av.TeamSlot < 4 ? (av.TeamSlot + 1) + ". " : "") + av.DisplayName + (av.IsOwner ? " (คุณ)" : "");
                row.Dot.color = row.Fill.color = MemberColor(av.TeamSlot);
                row.Status.text = PartyPresentation.StatusText(s) + (s.Ready ? "  " + Mathf.Min(100, (int)s.EnergyPercent) + "%" : "");
                row.Fill.rectTransform.sizeDelta = new Vector2(274 * Mathf.Clamp01(s.EnergyPercent / 100f), 6);
            }
        }
    }
}
