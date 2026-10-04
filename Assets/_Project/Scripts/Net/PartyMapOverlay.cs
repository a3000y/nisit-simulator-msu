using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NisitSimulator.Interaction;

namespace NisitSimulator.Net
{
    // UI overlay on each existing map image. Uses its camera projection, including camera rotation and UV rect.
    [DefaultExecutionOrder(250)]
    public sealed class PartyMapOverlay : MonoBehaviour, IPointerClickHandler
    {
        RawImage map;
        Camera mapCamera;
        Image input;
        readonly List<Image> dots = new List<Image>();
        readonly List<TMP_Text> numbers = new List<TMP_Text>();
        Sprite circle;
        Texture2D texture;
        public static PartyMapOverlay Create(RawImage image, Camera camera)
        {
            var go = new GameObject("PartyMapOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(image.transform, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var overlay = go.AddComponent<PartyMapOverlay>(); overlay.map = image; overlay.mapCamera = camera;
            overlay.input = go.GetComponent<Image>(); overlay.input.color = Color.clear; overlay.input.raycastTarget = false;
            overlay.MakeCircle(); return overlay;
        }
        void MakeCircle()
        {
            texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            var pixels = new Color[24 * 24];
            for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++)
                pixels[y * 24 + x] = new Color(1, 1, 1, Mathf.Clamp01(11.5f - Vector2.Distance(new Vector2(x, y), new Vector2(11.5f, 11.5f))));
            texture.SetPixels(pixels); texture.Apply();
            circle = Sprite.Create(texture, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f));
        }
        void OnDestroy() { if (circle != null) Destroy(circle); if (texture != null) Destroy(texture); }
        void LateUpdate()
        {
            var party = PartyRuntime.Instance;
            bool active = party != null && PartyRuntime.IsMultiplayerSession;
            input.raycastTarget = active && party.PingArmed && PartyRuntime.CanUsePingInput();
            int index = 0;
            if (active)
            {
                bool localInterior = InteriorManager.Instance != null && InteriorManager.Instance.IsInside;
                foreach (var av in party.Members)
                {
                    if (av == null || !av.IsSpawned || !av.TeamSummary.Ready || av.TeamSlot >= 4) continue;
                    // Within the same teleported interior use actual position. Outside show the entrance.
                    Vector3 position = localInterior || !av.TeamSummary.TeleportedInterior ? av.transform.position : av.TeamSummary.MapPosition;
                    if (!av.IsOwner) Draw(index++, position, av.TeamSlot, false);
                    for (int j = 0; j < av.TeamPingCount; j++)
                    {
                        var ping = av.TeamPingAt(j);
                        if (PartyPingGate.IsVisible(ping, party.ServerNow)) Draw(index++, ping.Position, av.TeamSlot, true);
                    }
                }
            }
            for (int i = index; i < dots.Count; i++) dots[i].gameObject.SetActive(false);
        }
        void Draw(int index, Vector3 world, byte slot, bool ping)
        {
            while (dots.Count <= index)
            {
                var go = new GameObject("TeamMarker", typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>(); image.sprite = circle; image.raycastTarget = false;
                var labelGo = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI)); labelGo.transform.SetParent(go.transform, false);
                var label = labelGo.GetComponent<TextMeshProUGUI>(); label.font = PartyHUD.ThaiFont(); label.fontSize = 11;
                label.color = new Color(0.15f, 0.12f, 0.22f); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                dots.Add(image); numbers.Add(label);
            }
            var dot = dots[index];
            var vp = mapCamera.WorldToViewportPoint(world);
            Rect uv = map.uvRect;
            bool valid = vp.z > 0 && Mathf.Abs(uv.width) > 0.0001f && Mathf.Abs(uv.height) > 0.0001f;
            dot.gameObject.SetActive(valid); if (!valid) return;
            Vector2 normalized = new Vector2((vp.x - uv.x) / uv.width, (vp.y - uv.y) / uv.height);
            // Keep out-of-view team markers at the edge; fade indicates outside the current view.
            bool outside = normalized.x < 0 || normalized.x > 1 || normalized.y < 0 || normalized.y > 1;
            var size = map.rectTransform.rect.size;
            float radius = ping ? 11 : 7;
            Vector2 margin = new Vector2(radius / Mathf.Max(size.x, radius * 2), radius / Mathf.Max(size.y, radius * 2));
            normalized.x = Mathf.Clamp(normalized.x, margin.x, 1 - margin.x);
            normalized.y = Mathf.Clamp(normalized.y, margin.y, 1 - margin.y);
            var rt = dot.rectTransform; rt.anchorMin = rt.anchorMax = normalized; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.one * radius * 2; rt.localRotation = Quaternion.Euler(0, 0, ping ? 45 : 0);
            dot.sprite = ping ? null : circle;
            var color = PartyHUD.MemberColor(slot); color.a = outside ? 0.55f : 1; dot.color = color;
            numbers[index].text = (slot + 1).ToString(); numbers[index].rectTransform.localRotation = Quaternion.Euler(0, 0, ping ? -45 : 0);
        }
        public void OnPointerClick(PointerEventData e)
        {
            var party = PartyRuntime.Instance;
            if (party == null || !party.PingArmed || e.button != PointerEventData.InputButton.Left) return;
            if (InteriorManager.Instance != null && InteriorManager.Instance.IsInside)
            { NisitSimulator.UI.HUDController.Toast("ออกจากอาคารก่อนปักหมุดบนแผนที่มหาวิทยาลัย"); return; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(map.rectTransform, e.position, e.pressEventCamera, out var point)) return;
            var rect = map.rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0 || !rect.Contains(point)) return;
            Vector2 normalized = new Vector2((point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height);
            var uv = map.uvRect;
            var ray = mapCamera.ViewportPointToRay(new Vector3(uv.x + normalized.x * uv.width, uv.y + normalized.y * uv.height, 0));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance)) party.TryPing(ray.GetPoint(distance));
        }
    }
}
