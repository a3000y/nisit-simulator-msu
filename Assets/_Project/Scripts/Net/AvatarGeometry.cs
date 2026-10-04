using UnityEngine;
using Unity.Netcode;

namespace NisitSimulator.Net
{
    public static class AvatarGeometry
    {
        // The controller's bottom is in LOCAL coordinates; TransformPoint includes center, scale and parent.
        public static Vector3 Feet(CharacterController controller) => controller != null
            ? controller.transform.TransformPoint(controller.center - Vector3.up * controller.height * .5f)
            : Vector3.zero;

        public static bool VisualBounds(Transform root, out Bounds bounds)
        {
            bounds = default; bool found = false;
            if (root == null) return false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool ValidScale(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && value.x > .01f && value.y > .01f && value.z > .01f && value.x < 10 && value.y < 10 && value.z < 10;
        public static void SetWorldScale(Transform model, Vector3 value)
        {
            if (model == null || !ValidScale(value)) return;
            Vector3 parent = model.parent != null ? model.parent.lossyScale : Vector3.one;
            model.localScale = new Vector3(value.x / Mathf.Abs(parent.x), value.y / Mathf.Abs(parent.y), value.z / Mathf.Abs(parent.z));
        }
        public static void PreparePuppet(Animator animator)
        {
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0);
        }
        // For catalog verification and a caller with a height target. Absolute assignment, never *=.
        public static bool NormalizeHeight(Animator animator, float height)
        {
            if (animator == null || !Finite(height) || height <= 0) return false;
            SetWorldScale(animator.transform, Vector3.one);
            PreparePuppet(animator);
            if (!VisualBounds(animator.transform, out var bounds) || bounds.size.y <= .01f) return false;
            SetWorldScale(animator.transform, Vector3.one * (height / bounds.size.y));
            PreparePuppet(animator);
            if (!VisualBounds(animator.transform, out bounds)) return false;
            // The model pivot is at its measured visual feet; root/NetworkObject stays intact.
            animator.transform.position += Vector3.up * (animator.transform.parent.position.y - bounds.min.y);
            return true;
        }
    }

    public struct AvatarVisualReference : INetworkSerializable, System.IEquatable<AvatarVisualReference>
    {
        public bool Valid;
        public int Model;
        public Vector3 WorldScale, FeetOffset;
        public Quaternion Rotation;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Valid); serializer.SerializeValue(ref Model); serializer.SerializeValue(ref WorldScale);
            serializer.SerializeValue(ref FeetOffset); serializer.SerializeValue(ref Rotation);
        }
        public bool Equals(AvatarVisualReference other) => Valid == other.Valid && Model == other.Model && WorldScale == other.WorldScale && FeetOffset == other.FeetOffset && Rotation == other.Rotation;
    }
    public struct AvatarAnimationFrame : INetworkSerializable
    {
        public int State;
        public float Phase, Length;
        public double SentTime;
        public bool Grounded;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref State); serializer.SerializeValue(ref Phase); serializer.SerializeValue(ref Length);
            serializer.SerializeValue(ref SentTime); serializer.SerializeValue(ref Grounded);
        }
    }
}
