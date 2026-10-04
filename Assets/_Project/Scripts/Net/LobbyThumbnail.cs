using UnityEngine;
using UnityEngine.UI;
using NisitSimulator.Systems;

namespace NisitSimulator.Net
{
    // Separate preview stages keep the existing gameplay avatar prefab untouched.
    public sealed class LobbyThumbnail : MonoBehaviour
    {
        GameObject stage, model;
        Camera cameraView;
        RenderTexture texture;
        string lastLook;
        public void Initialize(int slot)
        {
            stage = new GameObject("Lobby preview " + slot);
            stage.transform.position = new Vector3(1600 + slot * 14, 0, 1600);
            model = new GameObject("Model"); model.transform.SetParent(stage.transform, false);
            var lightGo = new GameObject("Preview light"); lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.localPosition = new Vector3(1, 2.5f, 2);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 8; light.intensity = 12;
            var cameraGo = new GameObject("Preview camera"); cameraGo.transform.SetParent(stage.transform, false);
            cameraGo.transform.localPosition = new Vector3(0, 1.1f, 3.3f);
            cameraGo.transform.LookAt(stage.transform.position + Vector3.up * 0.9f);
            cameraView = cameraGo.AddComponent<Camera>(); cameraView.fieldOfView = 32; cameraView.nearClipPlane = 0.1f; cameraView.farClipPlane = 8;
            cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(0.91f, 0.94f, 0.99f);
            texture = new RenderTexture(256, 320, 16); texture.Create(); cameraView.targetTexture = texture;
            GetComponent<RawImage>().texture = texture;
        }
        public void Show(LobbyPlayer player)
        {
            string look = player.Model + "/" + player.Color + "/" + player.Accessories;
            if (look == lastLook || model == null) return;
            lastLook = look; CharacterCatalog.Apply(model.transform, player.Model);
            NetworkAvatar.ApplyColor(model, player.Color);
            CharacterAccessories.Apply(model.transform, CharacterAccessories.Unpack(player.Accessories.ToString()));
            model.transform.localRotation = Quaternion.identity;
        }
        void OnEnable() { if (cameraView != null) cameraView.enabled = true; }
        void OnDisable() { if (cameraView != null) cameraView.enabled = false; }
        void OnDestroy() { if (stage != null) Destroy(stage); if (texture != null) { texture.Release(); Destroy(texture); } }
    }
}
