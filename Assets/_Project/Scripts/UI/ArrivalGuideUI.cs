using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace NisitSimulator.UI
{
    public class ArrivalGuideUI : MonoBehaviour
    {
        GameObject card;
        bool compactLayout;
        TMP_Text heading, body;
        Button next, skip;
        RectTransform highlight, canvasRect, target;
        public void Build(System.Action onNext, System.Action onSkip)
        {
            var canvas = GrowthUI.MakeCanvas(transform, "Arrival Guide Canvas", 110);
            canvasRect = (RectTransform)canvas.transform;
            card = GrowthUI.Box(canvas.transform, "Dialogue", new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(1160, 248), GrowthUI.CardCol).gameObject;
            heading = GrowthUI.Text(card.transform, "พี่ต้นกล้า ปี 3", new Vector2(-90, 80), new Vector2(920, 46), 28, GrowthUI.Title, TextAlignmentOptions.Left);
            body = GrowthUI.Text(card.transform, "", new Vector2(0, 0), new Vector2(1090, 102), 26, GrowthUI.Ink, TextAlignmentOptions.TopLeft);
            UIFit.OneLine(heading, 28, 24); UIFit.Wrap(body, 26, 24);
            next = GrowthUI.Button(card.transform, "ต่อไป [E]", new Vector2(415, -86), new Vector2(260, 50), GrowthUI.Good, 24);
            next.onClick.AddListener(() => onNext());
            skip = GrowthUI.Button(canvas.transform, "ข้ามทัวร์", Vector2.zero, new Vector2(220, 52), GrowthUI.CardCol, 24);
            var sr = (RectTransform)skip.transform; sr.anchorMin = sr.anchorMax = new Vector2(.5f, 1); sr.anchoredPosition = new Vector2(0, -44);
            skip.onClick.AddListener(() => onSkip());
            highlight = new GameObject("ButtonHighlight", typeof(RectTransform)).GetComponent<RectTransform>(); highlight.SetParent(canvas.transform, false);
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                edge.transform.SetParent(highlight, false); edge.color = new Color(1f, .72f, .1f); edge.raycastTarget = false;
                var rt = edge.rectTransform;
                rt.anchorMin = i < 2 ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rt.anchorMax = i < 2 ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rt.sizeDelta = i < 2 ? new Vector2(0, 5) : new Vector2(5, 0); rt.anchoredPosition = Vector2.zero;
            }
            highlight.gameObject.SetActive(false);
        }
        public void Present(string title, string text, bool canNext, bool compact = false)
        {
            compactLayout = compact;
            heading.text = title; body.text = text; next.gameObject.SetActive(canNext);
            Arrange();
            skip.gameObject.SetActive(!compact);
        }
        void Arrange()
        {
            bool compact = compactLayout;
            bool app = compact && RegistrationUI.IsOpen;
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = compact && !app ? new Vector2(0, 0) : new Vector2(.5f, 0);
            rt.anchoredPosition = app ? new Vector2(0, 20) : compact ? new Vector2(530, 140) : new Vector2(0, 150);
            rt.sizeDelta = app ? new Vector2(1600, 230) : compact ? new Vector2(990, 230) : new Vector2(1160, 248);
            body.rectTransform.sizeDelta = new Vector2(app ? 1530 : compact ? 920 : 1090, 102);
            heading.rectTransform.sizeDelta = new Vector2(app ? 1400 : compact ? 790 : 920, 46);
            next.GetComponent<RectTransform>().anchoredPosition = new Vector2(app ? 635 : compact ? 335 : 415, -86);
            skip.gameObject.SetActive(!compact);
        }
        public void PointAt(RectTransform rect) { target = rect; }
        void LateUpdate()
        {
            Arrange();
            bool valid = target != null && target.gameObject.activeInHierarchy;
            highlight.gameObject.SetActive(valid); if (!valid) return;
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(null, corners[0]), null, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(null, corners[2]), null, out var max);
            highlight.anchorMin = highlight.anchorMax = new Vector2(.5f,.5f);
            highlight.anchoredPosition = (min + max) / 2;
            highlight.sizeDelta = max - min + Vector2.one * (12 + 3 * Mathf.Sin(Time.unscaledTime * 4));
        }
    }
}
