using System.Collections.Generic;
using UnityEngine;

namespace NisitSimulator.CameraRig
{
    [DefaultExecutionOrder(110)]
    public class BuildingRoofHider : MonoBehaviour
    {
        public Transform player;
        public Transform[] roofGroups;
        public Vector3 localMin = new Vector3(-16f, -1f, -12f);
        public Vector3 localMax = new Vector3(16f, 9f, 12f);
        public float exitMargin = 0.25f;
        public float checkInterval = 0.1f;
        public bool IsRoofHidden { get; private set; }

        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<bool> originalStates = new List<bool>();
        float timer;

        void OnEnable()
        {
            renderers.Clear();
            originalStates.Clear();
            if (roofGroups != null)
                foreach (var group in roofGroups)
                    if (group != null)
                        foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                            if (!renderers.Contains(renderer))
                            {
                                renderers.Add(renderer);
                                originalStates.Add(renderer.enabled);
                            }
            timer = 0;
            Refresh();
        }

        void LateUpdate()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = Mathf.Max(0.02f, checkInterval);
            Refresh();
        }

        public void Refresh()
        {
            if (player == null)
            {
                var camera = Camera.main;
                var rig = camera != null ? camera.GetComponent<IsometricCameraRig>() : null;
                if (rig != null && rig.target != null) player = rig.target;
                else
                {
                    var found = GameObject.FindGameObjectWithTag("Player");
                    if (found == null) found = GameObject.Find("Player");
                    if (found != null) player = found.transform;
                }
            }
            bool inside = false;
            if (player != null)
            {
                Vector3 point = transform.InverseTransformPoint(player.position);
                float margin = IsRoofHidden ? exitMargin : 0;
                inside = point.x > localMin.x - margin && point.x < localMax.x + margin
                    && point.z > localMin.z - margin && point.z < localMax.z + margin
                    && point.y > localMin.y && point.y < localMax.y;
            }
            if (inside == IsRoofHidden) return;
            IsRoofHidden = inside;
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].enabled = inside ? false : originalStates[i];
        }

        void OnDisable()
        {
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].enabled = originalStates[i];
            IsRoofHidden = false;
        }
    }
}