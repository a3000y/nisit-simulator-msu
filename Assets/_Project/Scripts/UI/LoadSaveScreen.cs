using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using NisitSimulator.SaveLoad;
using NisitSimulator.Systems;

namespace NisitSimulator.UI
{
    // 🗂️ หน้าโหลดเซฟแบบลิสต์ (สร้าง UI เองตอนรัน ไม่ต้อง bake) — ดีไซน์พาสเทลหรู
    //   โชว์ ปี/สาขา/GPA/เวลาเล่น ต่อช่อง · ต่อปุ่ม "เล่นต่อ" ในเมนูให้เปิดหน้านี้
    public class LoadSaveScreen : MonoBehaviour
    {
        const int Slots = 3;

        // ---- พาเลตต์พาสเทล ----
        static readonly Color CardCol   = new Color(0.955f, 0.93f, 0.985f);
        static readonly Color HeaderCol = new Color(0.82f, 0.78f, 0.94f);
        static readonly Color BoxCol    = new Color(0.995f, 0.99f, 1f);
        static readonly Color RowSel    = new Color(0.90f, 0.86f, 0.99f);
        static readonly Color PillCol   = new Color(0.88f, 0.83f, 0.98f);
        static readonly Color TextDark  = new Color(0.28f, 0.24f, 0.44f);
        static readonly Color TextMute  = new Color(0.55f, 0.52f, 0.66f);
        static readonly Color TitleCol  = new Color(0.42f, 0.26f, 0.58f);
        static readonly Color Divider   = new Color(0.84f, 0.81f, 0.90f);
        static readonly Color Dim       = new Color(0.16f, 0.13f, 0.26f, 0.58f);
        static readonly Color[] FacCols = {
            new Color(0.55f, 0.74f, 0.95f), new Color(0.98f, 0.76f, 0.52f),
            new Color(0.52f, 0.82f, 0.62f), new Color(0.74f, 0.64f, 0.95f) };

        TMP_FontAsset font;
        Sprite rounded;
        GameObject panel;

        // per-row
        Button[] rows = new Button[Slots];
        Image[] accent = new Image[Slots];
        Image[] badge = new Image[Slots];
        TMP_Text[] badgeYear = new TMP_Text[Slots];
        TMP_Text[] rowTitle = new TMP_Text[Slots];
        TMP_Text[] rowSub = new TMP_Text[Slots];
        GameObject[] gpaPill = new GameObject[Slots];
        TMP_Text[] gpaText = new TMP_Text[Slots];

        // detail
        Image portraitBox; TMP_Text portraitText;
        TMP_Text[] detVal = new TMP_Text[5];   // วิชาเอก / ปีการศึกษา / GPA / เวลาเล่น / เงิน
        TMP_Text detEmpty;
        Button loadBtn, delBtn; TMP_Text delLabel;

        int sel = 0;
        int deleteArmed = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("LoadSaveScreenInstaller");
            DontDestroyOnLoad(go);
            var inst = go.AddComponent<LoadSaveScreen>();
            SceneManager.sceneLoaded += (s, m) => inst.StartCoroutine(inst.HookSoon());
            inst.StartCoroutine(inst.HookSoon());
        }

        IEnumerator HookSoon()
        {
            yield return null; yield return null;
            TryHook();
            yield return new WaitForSecondsRealtime(0.6f);
            TryHook();
        }

        void TryHook()
        {
            var mmc = Object.FindFirstObjectByType<MainMenuController>();
            if (mmc == null || mmc.continueButton == null) return;
            mmc.continueButton.onClick.RemoveAllListeners();
            mmc.continueButton.onClick.AddListener(Open);
            var lbl = mmc.continueButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = "โหลดเซฟ";
        }

        public void Open()
        {
            if (panel == null) Build();
            sel = Mathf.Clamp(GameSession.SaveSlot, 0, Slots - 1);
            deleteArmed = -1;
            Refresh();
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
        }

        void Close() { if (panel != null) panel.SetActive(false); }

        static string Gpa(SaveData d)
        {
            if (d == null || d.gradePoints == null || d.gradePoints.Count == 0) return "—";
            float sum = 0f; foreach (var g in d.gradePoints) sum += g;
            return (sum / d.gradePoints.Count).ToString("0.00");
        }

        void Refresh()
        {
            for (int i = 0; i < Slots; i++)
            {
                var d = SaveSystem.LoadSlot(i);
                bool has = d != null;
                var fc = has ? FacCols[Mathf.Clamp(d.facultyIndex, 0, FacCols.Length - 1)] : new Color(0.80f, 0.80f, 0.86f);

                if (accent[i]) accent[i].color = fc;
                if (badge[i]) badge[i].color = fc;
                if (badgeYear[i]) badgeYear[i].text = has ? d.currentYear.ToString() : "–";
                if (rowTitle[i]) rowTitle[i].text = $"ช่อง {i + 1}";
                if (rowSub[i]) rowSub[i].text = has ? FacultyCatalog.NameOf(d.facultyIndex) : "— ว่าง —";
                if (rowSub[i]) rowSub[i].color = has ? TextMute : new Color(0.68f, 0.66f, 0.76f);
                if (gpaPill[i]) gpaPill[i].SetActive(has);
                if (has && gpaText[i]) gpaText[i].text = $"GPA {Gpa(d)}";
            }
            UpdateSelection();
        }

        void Select(int i) { sel = i; deleteArmed = -1; UpdateSelection(); }

        void UpdateSelection()
        {
            for (int i = 0; i < Slots; i++)
                if (rows[i] != null && rows[i].targetGraphic != null)
                    rows[i].targetGraphic.color = (i == sel) ? RowSel : BoxCol;

            var d = SaveSystem.LoadSlot(sel);
            bool has = d != null;

            if (portraitBox) portraitBox.color = has ? FacCols[Mathf.Clamp(d.facultyIndex, 0, FacCols.Length - 1)] : new Color(0.84f, 0.84f, 0.9f);
            if (portraitText) portraitText.text = has ? $"<size=42%>ปี</size>\n{d.currentYear}" : "ว่าง";

            if (detEmpty) detEmpty.gameObject.SetActive(!has);
            for (int k = 0; k < detVal.Length; k++)
                if (detVal[k]) detVal[k].transform.parent.gameObject.SetActive(has);

            if (has)
            {
                detVal[0].text = FacultyCatalog.NameOf(d.facultyIndex);
                detVal[1].text = $"ปี {d.currentYear}  ·  วันที่ {d.dayInYear}";
                detVal[2].text = Gpa(d);
                detVal[3].text = $"{d.gameDay} วัน";
                detVal[4].text = $"{d.money:n0} ฿";
            }

            if (loadBtn != null) { loadBtn.interactable = has; SetAlpha(loadBtn, has ? 1f : 0.4f); }
            if (delBtn != null)  { delBtn.interactable = has;  SetAlpha(delBtn, has ? 1f : 0.4f); }
            if (delLabel != null) delLabel.text = "ลบ";
        }

        void DoLoad()
        {
            if (!SaveSystem.HasSave(sel)) return;
            GameSession.SaveSlot = sel;
            GameSession.PendingLoad = true;
            GameSession.IsContinue = true;
            GameSession.OpenNetworkOnStart = false;
            SceneManager.LoadScene(GameSession.GameplayScene);
        }

        void DoDelete()
        {
            if (!SaveSystem.HasSave(sel)) return;
            if (deleteArmed != sel) { deleteArmed = sel; if (delLabel != null) delLabel.text = "ยืนยันลบ?"; return; }
            SaveSystem.DeleteSlot(sel);
            deleteArmed = -1;
            Refresh();
        }

        // ================= สร้าง UI =================
        void Build()
        {
            font = Object.FindFirstObjectByType<TMP_Text>()?.font;
            rounded = UIStyle.Rounded;

            var canGo = new GameObject("LoadSave Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = canGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

            var dim = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canGo.transform, false);
            var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = Dim;
            panel = dim;

            var card = Panel(dim.transform, "Card", Vector2.zero, new Vector2(1460, 850), CardCol);
            Shadow(card, 0.26f, 10);

            // ----- แถบหัว -----
            var header = Panel(card.transform, "Header", new Vector2(0, 352), new Vector2(1380, 128), HeaderCol);
            header.GetComponent<Image>().raycastTarget = false;
            MakeText(header.transform, "โหลดเซฟ", new Vector2(0, 14), new Vector2(1200, 60), 46, TitleCol, FontStyles.Bold, TextAlignmentOptions.Center);
            MakeText(header.transform, "เลือกไฟล์บันทึกเพื่อเล่นต่อ", new Vector2(0, -34), new Vector2(1000, 34), 22, new Color(0.5f, 0.44f, 0.62f), FontStyles.Normal, TextAlignmentOptions.Center);

            // ----- ซ้าย: ลิสต์ช่องเซฟ -----
            float[] ys = { 120f, -44f, -208f };
            for (int i = 0; i < Slots; i++)
            {
                int idx = i;
                var rowGo = Panel(card.transform, "Row" + i, new Vector2(-400, ys[i]), new Vector2(628, 152), BoxCol);
                Outline(rowGo, new Color(0.60f, 0.55f, 0.76f, 0.45f), 1.5f);
                Shadow(rowGo, 0.14f, 4);
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = rowGo.GetComponent<Image>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => Select(idx));
                rows[i] = btn;

                accent[i] = Panel(rowGo.transform, "Accent", new Vector2(-305, 0), new Vector2(12, 130), FacCols[i]).GetComponent<Image>();
                accent[i].raycastTarget = false;

                badge[i] = Panel(rowGo.transform, "Badge", new Vector2(-238, 0), new Vector2(104, 104), FacCols[i]).GetComponent<Image>();
                badge[i].raycastTarget = false;
                badgeYear[i] = MakeText(badge[i].transform, "1", Vector2.zero, new Vector2(104, 104), 46, new Color(1f, 1f, 1f, 0.96f), FontStyles.Bold, TextAlignmentOptions.Center);
                badgeYear[i].raycastTarget = false;

                rowTitle[i] = MakeText(rowGo.transform, "ช่อง " + (i + 1), new Vector2(46, 32), new Vector2(260, 40), 28, TextDark, FontStyles.Bold, TextAlignmentOptions.Left);
                rowTitle[i].raycastTarget = false;
                rowSub[i] = MakeText(rowGo.transform, "", new Vector2(46, -22), new Vector2(238, 34), 20, TextMute, FontStyles.Normal, TextAlignmentOptions.Left);
                rowSub[i].raycastTarget = false;
                rowSub[i].enableWordWrapping = false; rowSub[i].overflowMode = TextOverflowModes.Ellipsis;

                gpaPill[i] = Panel(rowGo.transform, "GpaPill", new Vector2(242, 0), new Vector2(140, 54), PillCol);
                gpaPill[i].GetComponent<Image>().raycastTarget = false;
                gpaText[i] = MakeText(gpaPill[i].transform, "GPA –", Vector2.zero, new Vector2(150, 58), 22, TitleCol, FontStyles.Bold, TextAlignmentOptions.Center);
                gpaText[i].raycastTarget = false;
            }

            // ----- ขวา: รายละเอียด -----
            var detail = Panel(card.transform, "Detail", new Vector2(405, 18), new Vector2(560, 604), BoxCol);
            Outline(detail, new Color(0.60f, 0.55f, 0.76f, 0.45f), 1.5f);
            Shadow(detail, 0.14f, 5);
            MakeText(detail.transform, "รายละเอียด", new Vector2(0, 262), new Vector2(500, 40), 26, TitleCol, FontStyles.Bold, TextAlignmentOptions.Center);

            portraitBox = Panel(detail.transform, "Portrait", new Vector2(0, 150), new Vector2(210, 210), FacCols[0]).GetComponent<Image>();
            portraitBox.raycastTarget = false;
            Shadow(portraitBox.gameObject, 0.18f, 4);
            portraitText = MakeText(portraitBox.transform, "ปี\n1", Vector2.zero, new Vector2(210, 210), 62, new Color(1f, 1f, 1f, 0.97f), FontStyles.Bold, TextAlignmentOptions.Center);
            portraitText.raycastTarget = false;

            string[] labels = { "วิชาเอก", "ปีการศึกษา", "GPA", "เวลาเล่น", "เงินสะสม" };
            float y0 = 6f, step = 52f;
            for (int k = 0; k < labels.Length; k++)
            {
                float yy = y0 - k * step;
                var rowGo = new GameObject("Info" + k, typeof(RectTransform));
                rowGo.transform.SetParent(detail.transform, false);
                var rrt = (RectTransform)rowGo.transform; rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
                rrt.anchoredPosition = new Vector2(0, yy); rrt.sizeDelta = new Vector2(500, step);

                MakeText(rowGo.transform, labels[k], new Vector2(-240, 0), new Vector2(220, 40), 21, TextMute, FontStyles.Normal, TextAlignmentOptions.Left);
                detVal[k] = MakeText(rowGo.transform, "", new Vector2(245, 0), new Vector2(300, 40), k == 2 ? 26 : 22, k == 2 ? TitleCol : TextDark, FontStyles.Bold, TextAlignmentOptions.Right);
                if (k < labels.Length - 1)
                {
                    var line = Panel(rowGo.transform, "Div", new Vector2(0, -step * 0.5f + 2f), new Vector2(470, 2), Divider);
                    line.GetComponent<Image>().raycastTarget = false;
                }
            }

            detEmpty = MakeText(detail.transform, "ช่องนี้ยังไม่มีข้อมูลเซฟ", new Vector2(0, -40), new Vector2(460, 60), 24, TextMute, FontStyles.Italic, TextAlignmentOptions.Center);

            // ----- ปุ่มล่าง -----
            loadBtn = MakeButton(card.transform, "โหลด", new Vector2(-380, -372), new Vector2(300, 82), new Color(0.55f, 0.84f, 0.66f), out _);
            loadBtn.onClick.AddListener(DoLoad);
            delBtn = MakeButton(card.transform, "ลบ", new Vector2(0, -372), new Vector2(300, 82), new Color(0.98f, 0.72f, 0.76f), out delLabel);
            delBtn.onClick.AddListener(DoDelete);
            var back = MakeButton(card.transform, "กลับ", new Vector2(380, -372), new Vector2(300, 82), new Color(0.85f, 0.80f, 0.90f), out _);
            back.onClick.AddListener(Close);

            panel.SetActive(false);
        }

        // ---------- helpers ----------
        GameObject Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = col;
            if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }
            return go;
        }

        void Outline(GameObject go, Color col, float dist)
        {
            var o = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            o.effectColor = col; o.effectDistance = new Vector2(dist, -dist); o.useGraphicAlpha = false;
        }
        void Shadow(GameObject go, float a, float dist)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, a); s.effectDistance = new Vector2(0, -dist);
        }

        Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Color col, out TMP_Text labelOut)
        {
            var go = Panel(parent, label + "Btn", pos, size, col);
            Shadow(go, 0.22f, 5);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var cb = btn.colors; cb.normalColor = col; cb.highlightedColor = Lighten(col, 0.08f); cb.pressedColor = Lighten(col, -0.08f); cb.fadeDuration = 0.08f; btn.colors = cb;
            labelOut = MakeText(go.transform, label, Vector2.zero, size, 29, new Color(0.20f, 0.22f, 0.38f), FontStyles.Bold, TextAlignmentOptions.Center);
            labelOut.raycastTarget = false;
            return btn;
        }

        void SetAlpha(Button b, float a)
        {
            if (b == null || b.targetGraphic == null) return;
            var c = b.targetGraphic.color; b.targetGraphic.color = new Color(c.r, c.g, c.b, a);
        }

        static Color Lighten(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);

        TMP_Text MakeText(Transform parent, string s, Vector2 pos, Vector2 size, float fs, Color col, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = s; t.fontSize = fs; t.color = col; t.fontStyle = style; t.alignment = align; t.richText = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return t;
        }
    }
}
