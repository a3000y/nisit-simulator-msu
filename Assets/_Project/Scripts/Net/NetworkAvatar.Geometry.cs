using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using NisitSimulator.Systems;

namespace NisitSimulator.Net
{
    public partial class NetworkAvatar
    {
        readonly NetworkVariable<AvatarVisualReference> visualReference = new NetworkVariable<AvatarVisualReference>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<AvatarAnimationFrame> animationFrame = new NetworkVariable<AvatarAnimationFrame>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<uint> teleportSequence = new NetworkVariable<uint>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        CharacterController localController;
        Transform geometryPlayer;
        bool requestTeleport, receivedPose;
        uint lastTeleport;
        double lastAnimationTime = -1;
        float nextAnimation;

        void ConfigurePuppet()
        {
            transform.localScale = Vector3.one;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; body.detectCollisions = false; }
            foreach (var networkTransform in GetComponentsInChildren<NetworkTransform>(true))
            { networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false; }
        }
        Vector3 ReferenceScale => visualReference.Value.Valid && AvatarGeometry.ValidScale(visualReference.Value.WorldScale)
            ? visualReference.Value.WorldScale : Vector3.one;

        // Called by all the existing spawn/sleep/interior warp paths via InteriorManager.Teleport.
        public static void NotifyLocalTeleport(Transform player)
        {
            if (!PartyRuntime.IsMultiplayerSession) return;
            var manager = NetworkManager.Singleton;
            var avatar = manager != null && manager.LocalClient?.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<NetworkAvatar>() : null;
            if (avatar == null || !avatar.IsSpawned || !avatar.IsOwner || player == null || player.name != "Player") return;
            avatar.localPlayer = player; avatar.requestTeleport = true;
        }
        void LateUpdate()
        {
            if (!IsSpawned || !PartyRuntime.IsMultiplayerSession) return;
            if (IsOwner) PublishGeometry();
            else RenderGeometry();
        }
        void PublishGeometry()
        {
            if (localPlayer == null) CacheLocalPlayer();
            if (localPlayer == null) return;
            if (geometryPlayer != localPlayer)
            {
                geometryPlayer = localPlayer; localController = localPlayer.GetComponent<CharacterController>(); requestTeleport = true;
            }
            var localAnimator = localPlayer.GetComponentInChildren<Animator>();
            if (localAnimator == null) return;
            Vector3 feet = localController != null ? AvatarGeometry.Feet(localController) : localPlayer.position;
            var reference = new AvatarVisualReference { Valid = true, Model = netModel.Value, WorldScale = localAnimator.transform.lossyScale,
                FeetOffset = Quaternion.Inverse(localPlayer.rotation) * (localAnimator.transform.position - feet),
                Rotation = Quaternion.Inverse(localPlayer.rotation) * localAnimator.transform.rotation };
            if (!AvatarGeometry.ValidScale(reference.WorldScale)) return;
            // The same model changes scale/offset only when swapped, not every animation frame.
            if (!visualReference.Value.Equals(reference)) visualReference.Value = reference;
            netPos.Value = feet; netYaw.Value = localPlayer.eulerAngles.y;
            netSpeed.Value = localAnimator.GetFloat("Speed");
            transform.position = feet;
            if (requestTeleport) { teleportSequence.Value++; requestTeleport = false; }
            bool grounded = localController != null && localController.isGrounded;
            if (Time.unscaledTime >= nextAnimation || animationFrame.Value.Grounded != grounded)
            {
                nextAnimation = Time.unscaledTime + .1f;
                var state = localAnimator.GetCurrentAnimatorStateInfo(0);
                animationFrame.Value = new AvatarAnimationFrame { State = state.fullPathHash, Phase = state.normalizedTime, Length = state.length,
                    SentTime = NetworkManager.ServerTime.Time, Grounded = grounded };
            }
        }
        void RenderGeometry()
        {
            var reference = visualReference.Value;
            if (!reference.Valid || reference.Model != netModel.Value || anim == null || !AvatarGeometry.ValidScale(reference.WorldScale))
            { SetVisible(false); return; }
            SetVisible(true);
            AvatarGeometry.SetWorldScale(anim.transform, reference.WorldScale);
            anim.transform.localPosition = reference.FeetOffset;
            anim.transform.localRotation = reference.Rotation;
            var animation = animationFrame.Value;
            // Matching animation phase avoids comparing opposite ends of an idle/walk cycle.
            if (!gesturing && animation.SentTime != lastAnimationTime && animation.State != 0 && anim.HasState(0, animation.State) && AvatarGeometry.Finite(animation.Phase))
            {
                lastAnimationTime = animation.SentTime;
                float elapsed = Mathf.Clamp((float)(NetworkManager.ServerTime.Time - animation.SentTime), 0, .25f);
                float phase = animation.Phase + (animation.Length > .01f ? elapsed / animation.Length : 0);
                anim.Play(animation.State, 0, phase); anim.Update(0);
            }
            bool snap = !receivedPose || lastTeleport != teleportSequence.Value;
            if (!snap && AvatarGeometry.VisualBounds(anim.transform, out var visual))
                snap = Vector3.Distance(transform.position, netPos.Value) > Mathf.Max(visual.size.y * 4, 1)
                    || Mathf.Abs(transform.position.y - netPos.Value.y) > visual.size.y;
            if (snap) { transform.position = netPos.Value; transform.rotation = Quaternion.Euler(0, netYaw.Value, 0); receivedPose = true; lastTeleport = teleportSequence.Value; }
            SnapVisualFeet(animation.Grounded);
            if (nameTag != null && AvatarGeometry.VisualBounds(anim.transform, out var bounds))
                nameTag.position = new Vector3(transform.position.x, bounds.max.y + .18f, transform.position.z);
        }
        void SnapVisualFeet(bool grounded)
        {
            if (!grounded || !AvatarGeometry.VisualBounds(anim.transform, out var bounds)) return;
            float nearest = float.MaxValue; float ground = 0; bool found = false;
            // Use the received FOOT height. Starting above the head would hit the next storey's slab.
            foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * .3f, Vector3.down, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<NetworkAvatar>() != null || hit.collider.GetComponentInParent<NisitSimulator.Player.PlayerMovement>() != null || hit.normal.y < .5f || hit.distance >= nearest) continue;
                nearest = hit.distance; ground = hit.point.y; found = true;
            }
            if (found && Mathf.Abs(bounds.min.y - ground) <= .3f)
                transform.position += Vector3.up * (ground - bounds.min.y);
        }
    }
}
