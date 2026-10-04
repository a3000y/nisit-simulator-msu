#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using NisitSimulator.Net;
using NisitSimulator.UI;

namespace NisitSimulator.EditorTools
{
    public static class LobbyCharacterUIBuilder
    {
        const string SourceScene = "Assets/Scenes/Scene1.unity";
        const string Folder = "Assets/_Project/Resources/UI";
        public const string PrefabPath = Folder + "/LobbyCharacterCustomizer.prefab";

        [MenuItem("Nisit/Build Lobby Character UI")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("ออกจาก Play Mode ก่อนสร้างหน้าแต่งตัวล็อบบี้");
            var source = SceneManager.GetSceneByPath(SourceScene);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            if (openedSource) source = EditorSceneManager.OpenPreviewScene(SourceScene);
            var outputScene = EditorSceneManager.NewPreviewScene();
            try
            {
                MainMenuController menu = null;
                foreach (var root in source.GetRootGameObjects())
                {
                    menu = root.GetComponentInChildren<MainMenuController>(true);
                    if (menu != null) break;
                }
                if (menu == null || menu.characterPanel == null)
                    throw new InvalidOperationException("ไม่พบหน้าแต่งตัวเล่นคนเดียวใน Scene1");
                var original = menu.characterPanel.GetComponentInChildren<CharacterCreatorController>(true);
                if (original == null || original.previewRoot == null || original.previewCamera == null)
                    throw new InvalidOperationException("หน้าแต่งตัวเล่นคนเดียวยังต่อพรีวิวไม่ครบ");
                var originalStage = original.previewRoot.parent;
                if (originalStage == null || !original.previewCamera.transform.IsChildOf(originalStage))
                    throw new InvalidOperationException("กล้องและตัวละครพรีวิวต้องอยู่ในเวทีเดียวกัน");

                var wrapper = new GameObject("LobbyCharacterCustomizer");
                wrapper.SetActive(false);
                SceneManager.MoveGameObjectToScene(wrapper, outputScene);
                var canvasObject = new GameObject("Character Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(wrapper.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;
                var sourceCanvas = menu.characterPanel.GetComponentInParent<Canvas>();
                var sourceScaler = sourceCanvas != null ? sourceCanvas.GetComponent<CanvasScaler>() : null;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                if (sourceScaler != null) EditorUtility.CopySerialized(sourceScaler, scaler);
                else { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); }

                var panel = UnityEngine.Object.Instantiate(menu.characterPanel, canvasObject.transform, false);
                panel.name = menu.characterPanel.name;
                panel.SetActive(true);
                var stage = UnityEngine.Object.Instantiate(originalStage.gameObject, wrapper.transform, false);
                stage.name = originalStage.name;
                stage.transform.position = originalStage.position;
                stage.SetActive(true);
                var creator = panel.GetComponentInChildren<CharacterCreatorController>(true);
                creator.previewRoot = Map(originalStage, original.previewRoot, stage.transform);
                creator.previewCamera = Map(originalStage, original.previewCamera.transform, stage.transform).GetComponent<Camera>();
                creator.previewCamera.targetTexture = null;
                creator.previewCamera.enabled = false;
                creator.previewImage.texture = null;

                var customizer = wrapper.AddComponent<LobbyCharacterCustomizer>();
                customizer.creator = creator;
                customizer.confirmButton = Map(menu.characterPanel.transform, menu.characterConfirmButton.transform, panel.transform).GetComponent<Button>();
                customizer.backButton = Map(menu.characterPanel.transform, menu.characterBackButton.transform, panel.transform).GetComponent<Button>();
                // Only the lobby binds these buttons. No single-player scene loading or save selection.
                customizer.confirmButton.onClick = new Button.ButtonClickedEvent();
                customizer.backButton.onClick = new Button.ButtonClickedEvent();
                customizer.confirmButton.GetComponentInChildren<TMP_Text>(true).text = "กลับห้องรอ";
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.text == "สร้างนิสิตใหม่") text.text = "ปรับแต่งตัวละคร";
                    text.font = PartyHUD.ThaiFont();
                }
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "UI");
                if (PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath) == null)
                    throw new InvalidOperationException("บันทึกหน้าแต่งตัวล็อบบี้ไม่สำเร็จ");
                Debug.Log("[Lobby] สร้างหน้าแต่งตัวจาก Scene1 แล้ว: " + PrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(outputScene);
                if (openedSource) EditorSceneManager.ClosePreviewScene(source);
            }
        }

        // Map by sibling index: original UI contains many buttons with the same GameObject name.
        static Transform Map(Transform root, Transform original, Transform copy)
        {
            var path = new Stack<int>();
            while (original != root)
            {
                if (original == null) throw new InvalidOperationException("ปุ่มแต่งตัวอยู่นอกแผงต้นฉบับ");
                path.Push(original.GetSiblingIndex());
                original = original.parent;
            }
            while (path.Count > 0) copy = copy.GetChild(path.Pop());
            return copy;
        }
    }
}
#endif
