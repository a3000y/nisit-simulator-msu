using System;
using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.UI;

namespace NisitSimulator.Net
{
    // Uses the character page copied from Scene1, including its original controller and preview.
    public sealed class LobbyCharacterCustomizer : MonoBehaviour
    {
        public const string ResourcePath = "UI/LobbyCharacterCustomizer";
        public CharacterCreatorController creator;
        public Button confirmButton;
        public Button backButton;
        Action close;

        public void Initialize(Action onClose)
        {
            close = onClose;
            confirmButton.onClick.AddListener(Close);
            backButton.onClick.AddListener(Close);
        }

        public void Close()
        {
            // CharacterCreatorController already applies changes to GameSession and NetworkAvatar.
            close?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
