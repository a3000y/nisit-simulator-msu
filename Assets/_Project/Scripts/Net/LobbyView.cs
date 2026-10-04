using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    public sealed class LobbyView : MonoBehaviour
    {
        enum Page { Choose, Settings, Waiting }
        Page page;
        LobbyController controller;
        LobbyConnection connection;
        GameObject choose, settings, waiting, customize, kick, chatPanel;
        TMP_InputField address, roomName;
        TMP_Text message, maxLabel, modeLabel, shareLabel, heading, count, code, codeHint, transportLabel, startReason, copyLabel, toast, kickText;
        Button join, open, cancel, fallback, start, ready, share, copy, eye, prevIp, nextIp;
        readonly GameObject[] cards = new GameObject[4];
        readonly TMP_Text[] names = new TMP_Text[4], readiness = new TMP_Text[4], pings = new TMP_Text[4];
        readonly RawImage[] previews = new RawImage[4];
        readonly LobbyThumbnail[] thumbnails = new LobbyThumbnail[4];
        readonly Image[] crowns = new Image[4];
        readonly Button[] kicks = new Button[4];
        readonly Dictionary<string, string> previousPlayers = new Dictionary<string, string>();
        readonly List<Sprite> icons = new List<Sprite>();
        bool hasRoster, reveal = true;
        int ipIndex;
        float nextRefresh, copyUntil, toastUntil;
        string shareText = "", kickTarget = "";
        static readonly Color Blue = new Color(.73f, .85f, 1);
        static readonly Color Pink = new Color(1, .80f, .87f);
        static readonly Color Green = new Color(.74f, .93f, .81f);
        public static LobbyView Create(LobbyController controller, LobbyConnection connection)
        {
            var canvas = GrowthUI.MakeCanvas(null, "Lobby UI", 80);
            var view = canvas.gameObject.AddComponent<LobbyView>(); view.controller = controller; view.connection = connection; view.Build(); return view;
        }
        GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }
        TMP_Text Text(Transform parent, string value, Vector2 pos, Vector2 size, float font = 26)
        {
            var text = GrowthUI.Text(parent, value, pos, size, font, PastelTheme.TextDark);
            text.font = PartyHUD.ThaiFont(); text.richText = false; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Color color, Action action, float font = 25)
        {
            var button = GrowthUI.Button(parent, label, pos, size, color, font);
            var text = button.GetComponentInChildren<TMP_Text>(); text.font = PartyHUD.ThaiFont(); text.richText = false;
            button.onClick.AddListener(() => action()); var colors = button.colors; colors.disabledColor = new Color(.62f, .62f, .65f, .8f); button.colors = colors; return button;
        }
        TMP_InputField InputField(Transform parent, string hint, Vector2 pos, Vector2 size, int limit)
        {
            var box = GrowthUI.Box(parent, "Input", new Vector2(.5f, .5f), pos, size, Color.white);
            var field = box.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = box;
            var viewport = Group(box.transform, "Viewport"); var rt = (RectTransform)viewport.transform; rt.offsetMin = new Vector2(18, 8); rt.offsetMax = new Vector2(-18, -8); viewport.AddComponent<RectMask2D>();
            var text = Text(viewport.transform, "", Vector2.zero, size, 27); Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholder = Text(viewport.transform, hint, Vector2.zero, size, 24); Stretch(placeholder.rectTransform); placeholder.alignment = TextAlignmentOptions.MidlineLeft; placeholder.color = GrowthUI.Soft;
            field.textViewport = rt; field.textComponent = (TextMeshProUGUI)text; field.placeholder = placeholder; field.characterLimit = limit; field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }
        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        Sprite Icon(string kind)
        {
            var texture = new Texture2D(32, 24, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 24; y++) for (int x = 0; x < 32; x++)
            {
                bool filled = kind == "crown" ? (y >= 3 && y <= 8 && x >= 3 && x <= 28) || (y > 8 && y < 20 && x >= 3 && x <= 28 && (y < 13 || Mathf.Abs(x - 5) < (20 - y) / 2f || Mathf.Abs(x - 16) < (22 - y) / 2f || Mathf.Abs(x - 27) < (20 - y) / 2f)) :
                    kind == "eye" ? (Mathf.Abs(y - 12) <= 1 + 9 * Mathf.Sin(x / 31f * Mathf.PI) && (Mathf.Abs(y - 12) >= 7 * Mathf.Sin(x / 31f * Mathf.PI) || (x - 16) * (x - 16) + (y - 12) * (y - 12) < 16)) :
                    kind == "left" ? (x >= 8 && x <= 21 && Mathf.Abs(y - 12) <= x - 8) : (x >= 10 && x <= 23 && Mathf.Abs(y - 12) <= 23 - x);
                texture.SetPixel(x, y, filled ? Color.white : Color.clear);
            }
            texture.Apply(); var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 24), new Vector2(.5f, .5f)); icons.Add(sprite); return sprite;
        }
        void AddIcon(Button button, string kind)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image)); go.transform.SetParent(button.transform, false);
            var image = go.GetComponent<Image>(); image.sprite = Icon(kind); image.color = PastelTheme.TextDark; image.raycastTarget = false;
            var rt = image.rectTransform; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(32, 24);
        }
        void Build()
        {
            var bg = GrowthUI.Box(transform, "Background", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080), new Color(.95f, .94f, .99f), false); Stretch(bg.rectTransform);
            choose = Group(transform, "Choose"); settings = Group(transform, "Settings"); waiting = Group(transform, "Waiting");
            BuildChoose(); BuildSettings(); BuildWaiting();
            var status = GrowthUI.Box(transform, "Status", new Vector2(.5f, .5f), new Vector2(0, -445), new Vector2(1720, 86), PastelTheme.CardCol);
            message = Text(status.transform, "", new Vector2(-190, 0), new Vector2(1190, 80), 24);
            cancel = Button(status.transform, "ยกเลิก", new Vector2(670, 0), new Vector2(170, 54), Pink, () => connection.Cancel());
            fallback = Button(status.transform, "สร้างห้องแบบ LAN", new Vector2(560, 0), new Vector2(360, 54), Blue, () => { connection.Mode = LobbyConnectionMode.Lan; ShowPage(Page.Settings); });
            toast = Text(transform, "", new Vector2(0, 490), new Vector2(1500, 48), 26);
            BuildCustomize(); BuildKick(); BuildChat(); ShowPage(Page.Choose);
        }
        void BuildChoose()
        {
            Text(choose.transform, "เล่นกับเพื่อน", new Vector2(0, 380), new Vector2(1500, 80), 48);
            var hostCard = GrowthUI.Box(choose.transform, "Create card", new Vector2(.5f, .5f), new Vector2(-365, 15), new Vector2(650, 520), PastelTheme.CardCol);
            Text(hostCard.transform, "สร้างห้อง", new Vector2(0, 150), new Vector2(550, 70), 40);
            Text(hostCard.transform, "ตั้งค่าห้อง แล้วชวนเพื่อนมาเล่นด้วยกัน\nเล่นได้สูงสุด 4 คน", new Vector2(0, 25), new Vector2(550, 120), 28);
            Button(hostCard.transform, "ตั้งค่าห้อง", new Vector2(0, -145), new Vector2(490, 68), Pink, () => ShowPage(Page.Settings));
            var joinCard = GrowthUI.Box(choose.transform, "Join card", new Vector2(.5f, .5f), new Vector2(365, 15), new Vector2(650, 520), PastelTheme.CardCol);
            Text(joinCard.transform, "เข้าห้อง", new Vector2(0, 150), new Vector2(550, 70), 40);
            Text(joinCard.transform, "กรอกรหัสห้องหรือ IP ที่เพื่อนส่งให้", new Vector2(0, 66), new Vector2(570, 60), 25);
            address = InputField(joinCard.transform, "รหัสห้อง 6 ตัว หรือ IP", new Vector2(-55, -20), new Vector2(430, 66), 64);
            address.gameObject.name = "LobbyAddressInput";
            address.onSubmit.AddListener(_ => Join());
            Button(joinCard.transform, "วาง", new Vector2(220, -20), new Vector2(100, 66), Blue, () => address.text = GUIUtility.systemCopyBuffer.Trim());
            join = Button(joinCard.transform, "เข้าห้อง", new Vector2(0, -145), new Vector2(490, 68), Blue, Join);
            Button(choose.transform, "กลับเมนูหลัก", new Vector2(0, -335), new Vector2(380, 60), Color.white, controller.BackToMenu);
        }
        void Join() { if (!connection.Busy) connection.JoinAddress(address.text); }
        void BuildSettings()
        {
            var card = GrowthUI.Box(settings.transform, "Room settings", new Vector2(.5f, .5f), new Vector2(0, 15), new Vector2(940, 765), PastelTheme.CardCol);
            Text(card.transform, "ตั้งค่าห้อง", new Vector2(0, 298), new Vector2(760, 70), 40);
            Text(card.transform, "ชื่อห้อง", new Vector2(0, 220), new Vector2(760, 42), 24);
            roomName = InputField(card.transform, "ชื่อห้องไม่เกิน 20 ตัวอักษร", new Vector2(0, 165), new Vector2(760, 64), 60); roomName.text = connection.RoomTitle;
            roomName.gameObject.name = "LobbyRoomNameInput";
            roomName.onEndEdit.AddListener(value => { connection.RoomTitle = LobbyRules.LimitName(value, "ห้องของผู้เล่น"); roomName.SetTextWithoutNotify(connection.RoomTitle); });
            maxLabel = Text(card.transform, "", new Vector2(0, 66), new Vector2(480, 50), 27);
            var less = Button(card.transform, "", new Vector2(-300, 66), new Vector2(68, 54), Blue, () => connection.RoomMax = Mathf.Max(2, connection.RoomMax - 1)); AddIcon(less, "left");
            var more = Button(card.transform, "", new Vector2(300, 66), new Vector2(68, 54), Blue, () => connection.RoomMax = Mathf.Min(4, connection.RoomMax + 1)); AddIcon(more, "right");
            modeLabel = Text(card.transform, "", new Vector2(0, -20), new Vector2(760, 45), 26);
            Button(card.transform, "เปลี่ยนรูปแบบการเชื่อมต่อ", new Vector2(0, -77), new Vector2(760, 54), Blue, () => connection.Mode = connection.Mode == LobbyConnectionMode.Online ? LobbyConnectionMode.Lan : LobbyConnectionMode.Online);
            shareLabel = Text(card.transform, "", new Vector2(0, -153), new Vector2(760, 45), 26);
            Button(card.transform, "เปลี่ยนสิทธิ์การแชร์รหัส", new Vector2(0, -210), new Vector2(760, 54), Pink, () => connection.Share = connection.Share == LobbyShareMode.HostOnly ? LobbyShareMode.Everyone : LobbyShareMode.HostOnly);
            Button(card.transform, "ย้อนกลับ", new Vector2(-245, -310), new Vector2(250, 62), Color.white, () => ShowPage(Page.Choose));
            open = Button(card.transform, "เปิดห้อง", new Vector2(155, -310), new Vector2(450, 62), Green, () => { connection.RoomTitle = LobbyRules.LimitName(roomName.text, "ห้องของผู้เล่น"); connection.OpenHost(); });
        }
        void BuildWaiting()
        {
            var header = GrowthUI.Box(waiting.transform, "Header", new Vector2(.5f, .5f), new Vector2(0, 385), new Vector2(1730, 100), PastelTheme.CardCol);
            heading = Text(header.transform, "", new Vector2(-295, 0), new Vector2(1070, 85), 35);
            count = Text(header.transform, "", new Vector2(575, 0), new Vector2(440, 85), 29);
            var codeCard = GrowthUI.Box(waiting.transform, "Share card", new Vector2(.5f, .5f), new Vector2(0, 245), new Vector2(1730, 145), PastelTheme.CardCol);
            transportLabel = Text(codeCard.transform, "", new Vector2(-515, 38), new Vector2(650, 40), 23);
            code = Text(codeCard.transform, "", new Vector2(-500, -18), new Vector2(640, 65), 40);
            codeHint = Text(codeCard.transform, "", new Vector2(80, -15), new Vector2(390, 85), 21);
            copy = Button(codeCard.transform, "คัดลอก", new Vector2(385, 0), new Vector2(185, 60), Blue, Copy); copyLabel = copy.GetComponentInChildren<TMP_Text>();
            eye = Button(codeCard.transform, "", new Vector2(550, 0), new Vector2(82, 60), Pink, () => reveal = !reveal); AddIcon(eye, "eye");
            Text(codeCard.transform, "แสดงหรือซ่อน", new Vector2(680, 0), new Vector2(175, 60), 21);
            prevIp = Button(codeCard.transform, "", new Vector2(-805, -18), new Vector2(50, 46), Blue, () => SelectIp(-1)); AddIcon(prevIp, "left");
            nextIp = Button(codeCard.transform, "", new Vector2(-185, -18), new Vector2(50, 46), Blue, () => SelectIp(1)); AddIcon(nextIp, "right");
            for (int i = 0; i < 4; i++)
            {
                int slot = i; var card = GrowthUI.Box(waiting.transform, "Slot " + i, new Vector2(.5f, .5f), new Vector2(-648 + i * 432, -52), new Vector2(400, 370), PastelTheme.CardCol); cards[i] = card.gameObject;
                names[i] = Text(card.transform, "", new Vector2(0, 148), new Vector2(325, 46), 25);
                var preview = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)); preview.transform.SetParent(card.transform, false);
                var rt = (RectTransform)preview.transform; rt.anchoredPosition = new Vector2(0, 18); rt.sizeDelta = new Vector2(175, 215);
                previews[i] = preview.GetComponent<RawImage>(); previews[i].raycastTarget = false;
                thumbnails[i] = preview.AddComponent<LobbyThumbnail>(); thumbnails[i].Initialize(i);
                readiness[i] = Text(card.transform, "", new Vector2(0, -115), new Vector2(335, 42), 23);
                pings[i] = Text(card.transform, "", new Vector2(-85, -158), new Vector2(180, 32), 18);
                kicks[i] = Button(card.transform, "นำออก", new Vector2(100, -155), new Vector2(150, 40), Pink, () => ConfirmKick(slot), 21);
                var crownGo = new GameObject("Host crown", typeof(RectTransform), typeof(Image)); crownGo.transform.SetParent(card.transform, false); crowns[i] = crownGo.GetComponent<Image>(); crowns[i].sprite = Icon("crown"); crowns[i].color = GrowthUI.Gold; crowns[i].raycastTarget = false;
                crowns[i].rectTransform.anchoredPosition = new Vector2(-175, 150); crowns[i].rectTransform.sizeDelta = new Vector2(32, 24);
            }
            startReason = Text(waiting.transform, "", new Vector2(0, -282), new Vector2(1120, 45), 25);
            start = Button(waiting.transform, "เริ่มเกม", new Vector2(0, -350), new Vector2(340, 65), Green, controller.StartGame);
            ready = Button(waiting.transform, "พร้อม", new Vector2(0, -350), new Vector2(340, 65), Green, controller.ToggleReady);
            Button(waiting.transform, "ปรับแต่งตัวละคร", new Vector2(420, -350), new Vector2(310, 65), Blue, controller.Customize);
            Button(waiting.transform, "ออกจากห้อง", new Vector2(-420, -350), new Vector2(310, 65), Pink, controller.BackToMenu);
            share = Button(waiting.transform, "เปลี่ยนสิทธิ์แชร์รหัส", new Vector2(710, -282), new Vector2(290, 45), Color.white, () => { var state = LobbyState.Instance; if (state != null) state.SetShareMode(state.ShareCodeMode.Value != LobbyShareMode.Everyone); }, 21);
            Text(waiting.transform, "กด Y เพื่อแชท", new Vector2(-730, -282), new Vector2(240, 45), 21);
        }
        void BuildCustomize()
        {
            if (controller.customizeGroup != null) controller.customizeGroup.SetActive(false);
            var prefab = Resources.Load<GameObject>(LobbyCharacterCustomizer.ResourcePath);
            if (prefab != null)
            {
                // A separate canvas keeps Scene1's complete layout and its world-space preview stage.
                customize = Instantiate(prefab);
                customize.GetComponent<LobbyCharacterCustomizer>().Initialize(() => customize.SetActive(false));
                customize.SetActive(false);
                return;
            }
            customize = Group(transform, "Customize unavailable"); GrowthUI.Dim(customize.transform);
            var card = GrowthUI.Box(customize.transform, "Missing character page", new Vector2(.5f, .5f), Vector2.zero, new Vector2(800, 300), PastelTheme.CardCol);
            Text(card.transform, "ไม่พบหน้าแต่งตัวละคร กรุณาอัปเดตไฟล์เกม", Vector2.zero, new Vector2(720, 100), 27);
            Debug.LogError("[Lobby] Missing shared character page. Run Nisit/Build Lobby Character UI.");
            Button(card.transform, "กลับห้องรอ", new Vector2(0, -95), new Vector2(520, 58), Blue, () => customize.SetActive(false)); customize.SetActive(false);
        }
        void BuildKick()
        {
            kick = Group(transform, "Kick confirmation"); GrowthUI.Dim(kick.transform);
            var card = GrowthUI.Box(kick.transform, "Confirm", new Vector2(.5f, .5f), Vector2.zero, new Vector2(720, 280), PastelTheme.CardCol);
            kickText = Text(card.transform, "", new Vector2(0, 55), new Vector2(660, 110), 29);
            Button(card.transform, "ยกเลิก", new Vector2(-170, -65), new Vector2(270, 60), Color.white, () => kick.SetActive(false));
            Button(card.transform, "นำออกจากห้อง", new Vector2(170, -65), new Vector2(270, 60), Pink, () => { if (LobbyState.Instance != null) LobbyState.Instance.KickPlayer(kickTarget); kick.SetActive(false); }); kick.SetActive(false);
        }
        void BuildChat()
        {
            var chat = gameObject.AddComponent<ChatUI>(); chatPanel = Group(transform, "Chat modal"); GrowthUI.Dim(chatPanel.transform);
            var card = GrowthUI.Box(chatPanel.transform, "Chat", new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 560), PastelTheme.CardCol);
            Text(card.transform, "แชททีม กด Y หรือ Esc เพื่อปิด", new Vector2(0, 215), new Vector2(840, 50), 26);
            chat.log = Text(card.transform, "", new Vector2(0, 25), new Vector2(820, 330), 24); chat.log.alignment = TextAlignmentOptions.TopLeft;
            chat.input = InputField(card.transform, "พิมพ์ข้อความ แล้วกด Enter", new Vector2(0, -210), new Vector2(820, 64), 120); chat.panel = chatPanel; chatPanel.SetActive(false);
        }
        void ShowPage(Page next) { page = next; choose.SetActive(next == Page.Choose); settings.SetActive(next == Page.Settings); waiting.SetActive(next == Page.Waiting); }
        public void ShowCustomize() { if (page == Page.Waiting) customize.SetActive(true); }
        void Copy() { if (shareText.Length > 0) { GUIUtility.systemCopyBuffer = shareText; copyUntil = Time.unscaledTime + 2; } }
        void SelectIp(int direction)
        {
            if (connection.LanAddresses.Count == 0 || LobbyState.Instance == null) return;
            ipIndex = (ipIndex + direction + connection.LanAddresses.Count) % connection.LanAddresses.Count; LobbyState.Instance.SelectLanAddress(connection.LanAddresses[ipIndex]);
        }
        void ConfirmKick(int slot)
        {
            var state = LobbyState.Instance; if (state == null) return;
            foreach (var player in state.Snapshot()) if (player.SlotIndex == slot && !player.IsHost)
            { kickTarget = player.PlayerId.ToString(); kickText.text = "นำ " + player.Name + " ออกจากห้องหรือไม่"; kick.SetActive(true); break; }
        }
        void Toast(string text) { toast.text = text; toastUntil = Time.unscaledTime + 3; }
        void Update()
        {
            if (connection == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (kick.activeSelf) kick.SetActive(false);
                else if (customize.activeSelf) customize.SetActive(false);
                else if (chatPanel.activeSelf) { /* ChatUI handles closing and input focus. */ }
                else if (connection.Busy) connection.Cancel();
                else if (page == Page.Settings) ShowPage(Page.Choose);
                else controller.BackToMenu();
            }
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (Input.GetKeyDown(KeyCode.Return) && !connection.Busy && !customize.activeSelf && !kick.activeSelf && !chatPanel.activeSelf && (selected == null || selected.GetComponent<TMP_InputField>() == null))
            { if (page == Page.Choose) Join(); else if (page == Page.Settings) open.onClick.Invoke(); else if (NetworkManager.Singleton.IsHost) controller.StartGame(); else controller.ToggleReady(); }
            if (Time.unscaledTime < nextRefresh) return; nextRefresh = Time.unscaledTime + .2f;
            var state = LobbyState.Instance;
            if (connection.Connected && state != null && state.IsSpawned) { if (page != Page.Waiting) ShowPage(Page.Waiting); RefreshWaiting(state); }
            else if (page == Page.Waiting) { ShowPage(Page.Choose); previousPlayers.Clear(); hasRoster = false; customize.SetActive(false); kick.SetActive(false); }
            message.text = connection.Message; cancel.gameObject.SetActive(connection.Busy); fallback.gameObject.SetActive(!connection.Busy && !connection.Connected && connection.OfferLan);
            join.interactable = open.interactable = !connection.Busy && !connection.Connected;
            maxLabel.text = "จำนวนผู้เล่นสูงสุด " + connection.RoomMax + " คน";
            modeLabel.text = connection.Mode == LobbyConnectionMode.Online ? "ออนไลน์ผ่านรหัสห้อง" : "LAN ผ่าน IP";
            shareLabel.text = connection.Share == LobbyShareMode.HostOnly ? "แชร์รหัส: โฮสต์เท่านั้น" : "แชร์รหัส: ทุกคนในห้อง";
            if (Time.unscaledTime >= toastUntil) toast.text = "";
        }
        void RefreshWaiting(LobbyState state)
        {
            var nm = NetworkManager.Singleton; bool host = nm.IsHost; var players = state.Snapshot();
            heading.text = state.RoomName.Value.ToString(); count.text = players.Count + "/" + state.MaxPlayers.Value + " คน  " + (state.ConnectionMode.Value == LobbyConnectionMode.Lan ? "LAN" : "ออนไลน์");
            var current = new Dictionary<string, string>(); foreach (var p in players) current[p.PlayerId.ToString()] = p.Name.ToString();
            if (hasRoster)
            {
                foreach (var p in current) if (!previousPlayers.ContainsKey(p.Key)) Toast(p.Value + " เข้าห้องแล้ว");
                foreach (var p in previousPlayers) if (!current.ContainsKey(p.Key)) Toast(p.Value + " ออกจากห้องแล้ว");
                if (players.Count == state.MaxPlayers.Value && previousPlayers.Count < players.Count) Toast("ห้องเต็มแล้ว");
            }
            previousPlayers.Clear(); foreach (var p in current) previousPlayers[p.Key] = p.Value; hasRoster = true;
            bool canSee = host || state.ShareCodeMode.Value == LobbyShareMode.Everyone;
            bool lan = state.ConnectionMode.Value == LobbyConnectionMode.Lan;
            shareText = canSee ? host ? state.PrivateJoinCode : state.JoinCode.Value.ToString() : "";
            transportLabel.text = lan ? "LAN ผ่าน IP" : "ออนไลน์ผ่านรหัสห้อง";
            string formatted = !lan && shareText.Length == 6 ? shareText.Substring(0, 3) + " " + shareText.Substring(3) : shareText;
            code.text = !canSee ? "ขอรหัสจากโฮสต์" : reveal ? formatted : "******";
            codeHint.text = !canSee ? "โฮสต์เป็นผู้ส่งรหัสให้เพื่อน" : shareText == "127.0.0.1" ? "ไม่พบ IP เครือข่าย\nใช้ได้เฉพาะเครื่องนี้" : lan ? "ใช้ Wi-Fi หรือเครือข่ายเดียวกัน" : "ส่งรหัสนี้ให้เพื่อนเข้าห้อง";
            copy.gameObject.SetActive(canSee); eye.gameObject.SetActive(canSee); copy.interactable = shareText.Length > 0; copyLabel.text = Time.unscaledTime < copyUntil ? "คัดลอกแล้ว" : "คัดลอก";
            share.gameObject.SetActive(host); share.GetComponentInChildren<TMP_Text>().text = state.ShareCodeMode.Value == LobbyShareMode.HostOnly ? "แชร์รหัส: โฮสต์เท่านั้น" : "แชร์รหัส: ทุกคน";
            prevIp.gameObject.SetActive(host && lan && connection.LanAddresses.Count > 1); nextIp.gameObject.SetActive(host && lan && connection.LanAddresses.Count > 1);
            for (int slot = 0; slot < 4; slot++)
            {
                cards[slot].SetActive(slot < state.MaxPlayers.Value); bool found = false; LobbyPlayer player = default;
                foreach (var p in players) if (p.SlotIndex == slot) { player = p; found = true; break; }
                names[slot].text = found ? player.Name.ToString() : "รอผู้เล่น" + new string('.', 1 + (int)Time.unscaledTime % 3);
                readiness[slot].text = found ? player.Ready ? "พร้อม" : "ยังไม่พร้อม" : "ชวนเพื่อนเข้าห้องนี้";
                readiness[slot].color = found && player.Ready ? GrowthUI.Good : GrowthUI.Soft;
                pings[slot].text = found ? player.IsHost ? "โฮสต์" : player.Ping + " ms" : "";
                previews[slot].gameObject.SetActive(found); crowns[slot].gameObject.SetActive(found && player.IsHost); kicks[slot].gameObject.SetActive(found && host && !player.IsHost);
                if (found) thumbnails[slot].Show(player);
            }
            bool canStart = LobbyRules.CanStart(players, state.RoomState.Value, out string why);
            startReason.text = why; start.gameObject.SetActive(host); start.interactable = host && canStart; ready.gameObject.SetActive(!host);
            if (state.Find(nm.LocalClientId, out var mine)) ready.GetComponentInChildren<TMP_Text>().text = mine.Ready ? "ยกเลิกพร้อม" : "พร้อม";
        }
        void OnDestroy() { if (customize != null) Destroy(customize); foreach (var icon in icons) { if (icon != null) { Destroy(icon.texture); Destroy(icon); } } }
    }
}
