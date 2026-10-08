/*
 * Poison Menu  Mods/Extras.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Poison Software
 * https://github.com/heycanihavethis/Poison
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using GorillaLocomotion;
using GorillaTag.Rendering;
using Poison.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using static Poison.Menu.Main;
using Object = UnityEngine.Object;

namespace Poison.Mods
{
    /// <summary>Movement, fun and visual mods added alongside the originals.</summary>
    public static class Extras
    {
        private static Rigidbody Body => GorillaTagger.Instance.rigidbody;
        private static Vector3 BodyPosition => GorillaTagger.Instance.bodyCollider.transform.position;

        private static bool Grounded() =>
            Physics.Raycast(BodyPosition, Vector3.down, 1.1f, GTPlayer.Instance.locomotionEnabledLayers);

        // ── Movement ────────────────────────────────────────────────────────────

        private static bool doubleJumpUsed, doubleJumpHeld;

        /// <summary>One extra jump in the air on B, refilled on touching the ground.</summary>
        public static void DoubleJump()
        {
            bool grounded = Grounded();
            if (grounded)
                doubleJumpUsed = false;

            if (rightSecondary && !doubleJumpHeld && !grounded && !doubleJumpUsed)
            {
                Vector3 velocity = Body.linearVelocity;
                velocity.y = 7.5f;
                Body.linearVelocity = velocity;
                doubleJumpUsed = true;
            }

            doubleJumpHeld = rightSecondary;
        }

        private static bool blinkHeld;

        /// <summary>A on press: jumps four metres the way you look, stopping short of walls.</summary>
        public static void BlinkForward()
        {
            if (rightPrimary && !blinkHeld)
            {
                Transform head = GorillaTagger.Instance.headCollider.transform;
                Vector3 direction = head.forward;
                float distance = 4f;

                if (Physics.Raycast(head.position, direction, out RaycastHit hit, distance, NoInvisLayerMask()))
                    distance = Mathf.Max(0f, hit.distance - 0.5f);

                TeleportPlayer(BodyPosition + direction * distance);
            }

            blinkHeld = rightPrimary;
        }

        /// <summary>Left trigger in the air slams you straight down.</summary>
        public static void GroundPound()
        {
            if (leftTrigger > 0.5f && !Grounded())
                Body.linearVelocity = new Vector3(0f, -30f, 0f);
        }

        private static bool superJumpHeld;

        /// <summary>Y while standing launches you far higher than a normal jump.</summary>
        public static void SuperJump()
        {
            if (leftSecondary && !superJumpHeld && Grounded())
                Body.linearVelocity += Vector3.up * 16f;

            superJumpHeld = leftSecondary;
        }

        /// <summary>Right trigger pushes you the way your right hand points.</summary>
        public static void RocketHands()
        {
            if (rightTrigger > 0.5f)
                Body.linearVelocity += ControllerUtilities.GetTrueRightHand().forward * (25f * Time.deltaTime);
        }

        private static bool wallKickHeld;

        /// <summary>X beside a wall kicks you off it and upward.</summary>
        public static void WallKick()
        {
            if (leftPrimary && !wallKickHeld)
            {
                Vector3 look = GorillaTagger.Instance.headCollider.transform.forward;
                look.y = 0f;

                foreach (Vector3 direction in new[] { look.normalized, -look.normalized, Vector3.Cross(Vector3.up, look).normalized, -Vector3.Cross(Vector3.up, look).normalized })
                {
                    if (direction == Vector3.zero || !Physics.Raycast(BodyPosition, direction, out RaycastHit hit, 1.2f, GTPlayer.Instance.locomotionEnabledLayers))
                        continue;

                    Body.linearVelocity = hit.normal * 8f + Vector3.up * 8f;
                    break;
                }
            }

            wallKickHeld = leftPrimary;
        }

        /// <summary>Caps how fast you fall, so long drops end gently.</summary>
        public static void FeatherFall()
        {
            Vector3 velocity = Body.linearVelocity;
            if (velocity.y < -3f)
            {
                velocity.y = -3f;
                Body.linearVelocity = velocity;
            }
        }

        /// <summary>Both triggers held bleed your speed away, down to a hover.</summary>
        public static void AirBrake()
        {
            if (leftTrigger > 0.5f && rightTrigger > 0.5f)
                Body.linearVelocity *= 0.8f;
        }

        // ── Shared visual helpers ───────────────────────────────────────────────

        private static Material Glow(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.color = color;
            return material;
        }

        private static GameObject Primitive(PrimitiveType type, Color color, Vector3 scale)
        {
            GameObject made = GameObject.CreatePrimitive(type);
            Object.Destroy(made.GetComponent<Collider>());
            made.transform.localScale = scale;
            made.GetComponent<Renderer>().material = Glow(color);
            return made;
        }

        private static void Clear(ref GameObject made)
        {
            if (made != null)
                Object.Destroy(made);
            made = null;
        }

        // ── Fun and visual ──────────────────────────────────────────────────────

        private static GameObject halo;

        /// <summary>A soft gold ring floating above your head.</summary>
        public static void Halo()
        {
            halo ??= Primitive(PrimitiveType.Cylinder, new Color(1f, 0.9f, 0.4f, 0.75f), new Vector3(0.35f, 0.01f, 0.35f));
            halo.transform.position = GorillaTagger.Instance.headCollider.transform.position + Vector3.up * 0.3f;
        }

        public static void DisableHalo() => Clear(ref halo);

        private static readonly GameObject[] orbs = new GameObject[3];

        /// <summary>Three glowing orbs circling your head.</summary>
        public static void OrbitingOrbs()
        {
            Vector3 centre = GorillaTagger.Instance.headCollider.transform.position;

            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null)
                    orbs[i] = Primitive(PrimitiveType.Sphere, Color.HSVToRGB(i / 3f, 0.7f, 1f) * new Color(1f, 1f, 1f, 0.85f), Vector3.one * 0.08f);

                float angle = Time.time * 2f + i * Mathf.PI * 2f / orbs.Length;
                orbs[i].transform.position = centre + new Vector3(Mathf.Cos(angle) * 0.45f, Mathf.Sin(Time.time * 3f + i) * 0.1f, Mathf.Sin(angle) * 0.45f);
            }
        }

        public static void DisableOrbitingOrbs()
        {
            for (int i = 0; i < orbs.Length; i++)
                Clear(ref orbs[i]);
        }

        private static GameObject leftTrail, rightTrail;

        private static GameObject Trail(Transform hand)
        {
            GameObject holder = new GameObject("PoisonHandTrail");
            holder.transform.SetParent(hand, false);

            TrailRenderer trail = holder.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = 0.05f;
            trail.endWidth = 0f;
            trail.material = Glow(Color.white);

            Color color = VRRig.LocalRig.playerColor;
            trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            return holder;
        }

        /// <summary>Fading streaks behind both hands, in your own colour.</summary>
        public static void HandTrails()
        {
            leftTrail ??= Trail(GorillaTagger.Instance.leftHandTransform);
            rightTrail ??= Trail(GorillaTagger.Instance.rightHandTransform);
        }

        public static void DisableHandTrails()
        {
            Clear(ref leftTrail);
            Clear(ref rightTrail);
        }

        private static void SetFog(Color color) =>
            ZoneShaderSettings.activeInstance.SetGroundFogValue(color, 0f, float.MaxValue, 0f);

        private static void ResetFog() =>
            ZoneShaderSettings.activeInstance.CopySettings(ZoneShaderSettings.defaultsInstance);

        /// <summary>Fog that drifts slowly through every colour.</summary>
        public static void RainbowFog() =>
            SetFog(Color.HSVToRGB(Time.time * 0.1f % 1f, 0.8f, 1f) * new Color(1f, 1f, 1f, 0.15f));

        private static float discoNext;

        /// <summary>Fog that snaps to a new colour four times a second.</summary>
        public static void DiscoFog()
        {
            if (Time.time < discoNext)
                return;

            discoNext = Time.time + 0.25f;
            SetFog(Color.HSVToRGB(Random.value, 1f, 1f) * new Color(1f, 1f, 1f, 0.2f));
        }

        public static void DisableFog() => ResetFog();

        private static GameObject pulse;
        private static float pulseStart;

        /// <summary>A ring that ripples out from your feet every second.</summary>
        public static void PulseRing()
        {
            pulse ??= Primitive(PrimitiveType.Cylinder, Color.white, Vector3.one);

            float age = Time.time - pulseStart;
            if (age > 1f)
            {
                pulseStart = Time.time;
                age = 0f;
            }

            float radius = Mathf.Lerp(0.2f, 3f, age);
            Color color = VRRig.LocalRig.playerColor;
            pulse.GetComponent<Renderer>().material.color = new Color(color.r, color.g, color.b, 0.5f * (1f - age));
            pulse.transform.localScale = new Vector3(radius, 0.005f, radius);
            pulse.transform.position = BodyPosition + Vector3.down * 0.55f;
        }

        public static void DisablePulseRing() => Clear(ref pulse);

        private static GameObject headlamp;

        /// <summary>A torch strapped to your head.</summary>
        public static void Headlamp()
        {
            if (headlamp != null)
                return;

            headlamp = new GameObject("PoisonHeadlamp");
            headlamp.transform.SetParent(GorillaTagger.Instance.headCollider.transform, false);

            Light lamp = headlamp.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.range = 25f;
            lamp.spotAngle = 60f;
            lamp.intensity = 2f;
        }

        public static void DisableHeadlamp() => Clear(ref headlamp);

        /// <summary>Your head tipped on its side, as everyone sees it.</summary>
        public static void TiltedHead() =>
            VRRig.LocalRig.head.trackingRotationOffset.z = 90f;

        public static void DisableTiltedHead() =>
            VRRig.LocalRig.head.trackingRotationOffset.z = 0f;
    }
}
