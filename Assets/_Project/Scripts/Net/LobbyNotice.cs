using UnityEngine;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    public sealed class LobbyNotice : MonoBehaviour
    {
        float expires;
        public static string LastNotice { get; private set; } = "";
        public static void Show(string text)
        {
            LastNotice = text;
            var canvas = GrowthUI.MakeCanvas(null, "Lobby notice", 160);
            DontDestroyOnLoad(canvas.gameObject);
            canvas.gameObject.AddComponent<LobbyNotice>().expires = Time.realtimeSinceStartup + 4;
            var box = GrowthUI.Box(canvas.transform, "Notice", new Vector2(.5f, 1), new Vector2(0, -36), new Vector2(1000, 70), PastelTheme.CardCol);
            var label = GrowthUI.Text(box.transform, text, Vector2.zero, new Vector2(960, 60), 26, PastelTheme.TextDark);
            label.font = PartyHUD.ThaiFont(); label.richText = false;
        }
        void Update() { if (Time.realtimeSinceStartup >= expires) Destroy(gameObject); }
    }
}
