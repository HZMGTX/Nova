/*
 * Nova Menu  Mods/Custom.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  HZMGTX
 * https://github.com/HZMGTX/Nova
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
using Nova.Extensions;
using Nova.Managers;
using Nova.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static Nova.Menu.Main;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Nova.Mods
{
    /// <summary>Nova's own mods, gathered in the Custom Mods category.</summary>
    /// <remarks>
    /// Everything here acts on you alone: how you move, and things only you see.
    /// </remarks>
    public static partial class Custom
    {
        public const string Category = "Custom Mods";

        private static Transform Head => GorillaTagger.Instance.headCollider.transform;
        private static Color MyColor => VRRig.LocalRig.playerColor;
        private static float Scale => GTPlayer.Instance.scale;

        /// <summary>Where you are looking, flattened onto the ground.</summary>
        private static Vector3 LookFlat()
        {
            Vector3 look = Head.forward;
            look.y = 0f;
            return look.sqrMagnitude < 0.0001f ? Vector3.forward : look.normalized;
        }

        private static Material vertexColored;

        /// <summary>A material that takes its colour from what it draws, for particles and lines.</summary>
        private static Material VertexColored
        {
            get
            {
                if (vertexColored == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    vertexColored = shader != null ? new Material(shader) : Extras.Glow(Color.white);
                }

                return vertexColored;
            }
        }

        private static void Destroy(GameObject made)
        {
            if (made == null)
                return;

            // Primitives carry their own material instance, which outlives the object.
            if (made.TryGetComponent(out Renderer renderer) && renderer.sharedMaterial != null && renderer.sharedMaterial != vertexColored)
                Object.Destroy(renderer.sharedMaterial);

            Object.Destroy(made);
        }

        private static void Tint(GameObject made, Color color) =>
            made.GetComponent<Renderer>().material.color = color;

        // ── Movement ────────────────────────────────────────────────────────────

        private static Vector3? ropePoint;
        private static float ropeLength;
        private static LineRenderer rope;

        /// <summary>Hold the right trigger to fire a rope from your right hand and swing on it; squeeze fully to reel in.</summary>
        public static void RopeSwing()
        {
            var hand = ControllerUtilities.GetTrueRightHand();

            if (rightTrigger < 0.5f)
            {
                ropePoint = null;
                if (rope != null)
                    rope.enabled = false;
                return;
            }

            if (ropePoint == null)
            {
                if (!Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, 60f, NoInvisLayerMask()))
                    return;

                ropePoint = hit.point;
                ropeLength = Vector3.Distance(Extras.BodyPosition, hit.point);
            }

            Vector3 toPoint = ropePoint.Value - Extras.BodyPosition;
            float distance = toPoint.magnitude;
            Vector3 along = toPoint / Mathf.Max(distance, 0.001f);

            if (rightTrigger > 0.9f)
                ropeLength = Mathf.Max(1f, ropeLength - 4f * Time.deltaTime);

            // A rope only pulls when taut: it takes away the speed that would stretch it,
            // and springs you back in if you are already past its length.
            if (distance > ropeLength)
            {
                Vector3 velocity = Extras.Body.linearVelocity;
                float outward = Vector3.Dot(velocity, -along);
                if (outward > 0f)
                    velocity += along * outward;

                velocity += along * ((distance - ropeLength) * 10f * Time.deltaTime);
                Extras.Body.linearVelocity = velocity;
            }

            if (rope == null)
            {
                rope = new GameObject("Nova_Rope").AddComponent<LineRenderer>();
                rope.material = VertexColored;
                rope.positionCount = 2;
                rope.useWorldSpace = true;
            }

            rope.enabled = true;
            rope.startWidth = rope.endWidth = 0.025f * Scale;
            rope.startColor = rope.endColor = MyColor;
            rope.SetPosition(0, hand.position);
            rope.SetPosition(1, ropePoint.Value);
        }

        public static void DisableRopeSwing()
        {
            ropePoint = null;
            if (rope != null)
                Object.Destroy(rope.gameObject);
            rope = null;
        }

        /// <summary>Spread your arms wide in the air to glide the way you look.</summary>
        public static void ArmGlide()
        {
            float span = Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, GorillaTagger.Instance.rightHandTransform.position);
            if (Extras.Grounded() || span < 1.1f * Scale)
                return;

            Vector3 velocity = Extras.Body.linearVelocity;
            if (velocity.y < -2f)
                velocity.y = -2f;

            if (new Vector3(velocity.x, 0f, velocity.z).magnitude < 12f)
                velocity += LookFlat() * (8f * Time.deltaTime);

            Extras.Body.linearVelocity = velocity;
        }

        private static bool airDashUsed, airDashHeld;

        /// <summary>B in the air bursts you the way you look, once per jump.</summary>
        public static void AirDash()
        {
            bool grounded = Extras.Grounded();
            if (grounded)
                airDashUsed = false;

            if (rightSecondary && !airDashHeld && !grounded && !airDashUsed)
            {
                Extras.Body.linearVelocity = Head.forward * 12f;
                airDashUsed = true;
            }

            airDashHeld = rightSecondary;
        }

        /// <summary>Hold your left grip in the air to hang where you are.</summary>
        public static void Hover()
        {
            if (!leftGrab || Extras.Grounded())
                return;

            // Worked in velocity and frame time, so it holds you the same at any frame rate;
            // a force added every frame piles up between physics steps on faster headsets.
            Vector3 velocity = Extras.Body.linearVelocity;
            velocity -= Physics.gravity * Time.deltaTime;
            velocity.y = Mathf.MoveTowards(velocity.y, 0f, 20f * Time.deltaTime);
            Extras.Body.linearVelocity = velocity;
        }

        private static float lastFallSpeed;
        private static bool wasGrounded = true;

        /// <summary>Hard landings bounce you back up.</summary>
        public static void BouncyLanding()
        {
            bool grounded = Extras.Grounded();
            Vector3 velocity = Extras.Body.linearVelocity;

            if (grounded && !wasGrounded && lastFallSpeed > 6f)
                Extras.Body.linearVelocity = new Vector3(velocity.x, lastFallSpeed * 0.7f, velocity.z);

            lastFallSpeed = grounded ? 0f : Mathf.Max(0f, -velocity.y);
            wasGrounded = grounded;
        }

        // ── Things only you see ─────────────────────────────────────────────────

        private static GameObject leftWing, rightWing;

        /// <summary>Glowing wings on your back that flap while you are in the air.</summary>
        public static void Wings()
        {
            if (leftWing == null)
                leftWing = Extras.Primitive(PrimitiveType.Cube, MyColor, Vector3.one);
            if (rightWing == null)
                rightWing = Extras.Primitive(PrimitiveType.Cube, MyColor, Vector3.one);

            float flap = Extras.Grounded() ? 10f : 15f + 30f * Mathf.Sin(Time.time * 8f);
            Quaternion facing = Quaternion.LookRotation(LookFlat());

            PlaceWing(leftWing, facing, -1f, flap);
            PlaceWing(rightWing, facing, 1f, flap);
        }

        private static void PlaceWing(GameObject wing, Quaternion facing, float side, float flap)
        {
            float scale = Scale;

            // Hinged at the inner edge, so a flap lifts the tip rather than spinning the middle.
            Vector3 root = Extras.BodyPosition + facing * new Vector3(side * 0.12f, 0.15f, -0.18f) * scale;
            wing.transform.rotation = facing * Quaternion.Euler(0f, side * -20f, side * flap);
            wing.transform.position = root + wing.transform.rotation * (Vector3.right * (side * 0.3f * scale));
            wing.transform.localScale = new Vector3(0.6f, 0.02f, 0.25f) * scale;

            Color color = MyColor;
            Tint(wing, new Color(color.r, color.g, color.b, 0.6f));
        }

        public static void DisableWings()
        {
            Destroy(leftWing);
            Destroy(rightWing);
            leftWing = rightWing = null;
        }

        private static GameObject pet;

        /// <summary>A glowing orb in your colour that floats along beside you.</summary>
        public static void PetOrb()
        {
            Vector3 target = Head.position
                + Quaternion.LookRotation(LookFlat()) * new Vector3(0.45f, 0.1f, 0.3f) * Scale
                + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.06f * Scale);

            if (pet == null)
            {
                pet = Extras.Primitive(PrimitiveType.Sphere, MyColor, Vector3.one);
                pet.transform.position = target;
            }

            pet.transform.position = Vector3.Lerp(pet.transform.position, target, Time.deltaTime * 4f);
            pet.transform.localScale = Vector3.one * (0.12f * Scale);
            Tint(pet, MyColor);
        }

        public static void DisablePetOrb()
        {
            Destroy(pet);
            pet = null;
        }

        private static GameObject saber;

        /// <summary>A glowing blade in your colour out of your right hand.</summary>
        public static void Lightsaber()
        {
            if (saber == null)
                saber = Extras.Primitive(PrimitiveType.Cylinder, MyColor, Vector3.one);

            var hand = ControllerUtilities.GetTrueRightHand();
            float length = 0.9f * Scale;

            // A cylinder is two units tall along its own up, so it is turned to point forward.
            saber.transform.rotation = Quaternion.LookRotation(hand.forward) * Quaternion.Euler(90f, 0f, 0f);
            saber.transform.localScale = new Vector3(0.035f * Scale, length / 2f, 0.035f * Scale);
            saber.transform.position = hand.position + hand.forward * (length / 2f + 0.05f * Scale);

            Color color = MyColor;
            Tint(saber, new Color(color.r, color.g, color.b, 0.75f + 0.15f * Mathf.Sin(Time.time * 20f)));
        }

        public static void DisableLightsaber()
        {
            Destroy(saber);
            saber = null;
        }

        private static ParticleSystem MakeParticles(string name, Transform parent, Action<ParticleSystem> configure)
        {
            GameObject holder = new GameObject(name);
            if (parent != null)
                holder.transform.SetParent(parent, false);

            ParticleSystem particles = holder.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            holder.GetComponent<ParticleSystemRenderer>().material = VertexColored;

            var main = particles.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            configure(particles);
            particles.Play();
            return particles;
        }

        private static ParticleSystem.MinMaxGradient Rainbow()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 0.2f), new GradientColorKey(Color.green, 0.4f),
                    new GradientColorKey(Color.cyan, 0.6f), new GradientColorKey(Color.blue, 0.8f), new GradientColorKey(Color.magenta, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        /// <summary>A one-off spray of particles, cleaned up once they have faded.</summary>
        /// <param name="direction">Sprays in a cone this way; without one, in every direction.</param>
        private static void Burst(Vector3 at, int count, float speed, float gravity, Vector3? direction = null)
        {
            float scale = Scale;
            ParticleSystem particles = MakeParticles("Nova_Burst", null, system =>
            {
                var main = system.main;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f * scale, speed * scale);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f * scale, 0.05f * scale);
                main.startColor = Rainbow();
                main.gravityModifier = gravity;
                main.maxParticles = count;

                var emission = system.emission;
                emission.enabled = false;

                var shape = system.shape;
                shape.shapeType = direction == null ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
                shape.angle = 25f;
                shape.radius = 0.05f * scale;
            });

            particles.transform.position = at;
            if (direction != null && direction.Value.sqrMagnitude > 0.0001f)
                particles.transform.rotation = Quaternion.LookRotation(direction.Value);

            particles.Emit(count);
            Object.Destroy(particles.gameObject, 3f);
        }

        private static ParticleSystem leftSparkles, rightSparkles;

        private static ParticleSystem Sparkles(Transform hand) =>
            MakeParticles("Nova_Sparkles", hand, system =>
            {
                var main = system.main;
                main.loop = true;
                main.startLifetime = 0.6f;
                main.startSpeed = 0.15f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
                main.startColor = Rainbow();
                main.maxParticles = 200;

                var emission = system.emission;
                emission.rateOverTime = 40f;

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.04f;
            });

        /// <summary>A trail of sparkles from both hands.</summary>
        public static void SparkleHands()
        {
            if (leftSparkles == null)
                leftSparkles = Sparkles(GorillaTagger.Instance.leftHandTransform);
            if (rightSparkles == null)
                rightSparkles = Sparkles(GorillaTagger.Instance.rightHandTransform);
        }

        public static void DisableSparkleHands()
        {
            if (leftSparkles != null)
                Object.Destroy(leftSparkles.gameObject);
            if (rightSparkles != null)
                Object.Destroy(rightSparkles.gameObject);
            leftSparkles = rightSparkles = null;
        }

        private static bool fireworkHeld;

        /// <summary>B launches a firework from your right hand.</summary>
        public static void Fireworks()
        {
            if (rightSecondary && !fireworkHeld)
                CoroutineManager.instance.StartCoroutine(LaunchFirework(ControllerUtilities.GetTrueRightHand().position));

            fireworkHeld = rightSecondary;
        }

        private static IEnumerator LaunchFirework(Vector3 from)
        {
            GameObject rocket = Extras.Primitive(PrimitiveType.Sphere, Color.white, Vector3.one * (0.06f * Scale));
            Vector3 drift = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            float start = Time.time;

            while (Time.time - start < 1.1f)
            {
                if (rocket == null)
                    yield break;

                rocket.transform.position = from + (Vector3.up * 9f + drift) * ((Time.time - start) * Scale);
                yield return null;
            }

            Vector3 at = rocket.transform.position;
            Destroy(rocket);
            Burst(at, 150, 6f, 0.4f);
        }

        private static bool confettiHeld;

        /// <summary>A pops a burst of confetti from your right hand.</summary>
        public static void ConfettiPopper()
        {
            if (rightPrimary && !confettiHeld)
            {
                var hand = ControllerUtilities.GetTrueRightHand();
                Burst(hand.position + hand.forward * (0.1f * Scale), 90, 4f, 0.8f, hand.forward);
            }

            confettiHeld = rightPrimary;
        }

        private static readonly List<(GameObject print, float born)> prints = new List<(GameObject, float)>();
        private static float nextPrint;
        private static bool leftFoot;

        /// <summary>Glowing prints where you walk, fading after a few seconds.</summary>
        public static void GlowingFootprints()
        {
            float now = Time.time;
            Color color = MyColor;

            for (int i = prints.Count - 1; i >= 0; i--)
            {
                var (print, born) = prints[i];
                float age = (now - born) / 4f;

                if (print == null || age >= 1f)
                {
                    Destroy(print);
                    prints.RemoveAt(i);
                    continue;
                }

                Tint(print, new Color(color.r, color.g, color.b, 0.8f * (1f - age)));
            }

            Vector3 velocity = Extras.Body.linearVelocity;
            velocity.y = 0f;

            if (now < nextPrint || velocity.magnitude < 1f)
                return;

            if (!Physics.Raycast(Extras.BodyPosition, Vector3.down, out RaycastHit hit, 1.5f * Scale, GTPlayer.Instance.locomotionEnabledLayers))
                return;

            Vector3 forward = Vector3.ProjectOnPlane(velocity, hit.normal);
            if (forward.sqrMagnitude < 0.0001f)
                return;

            nextPrint = now + 0.3f;
            leftFoot = !leftFoot;

            Vector3 side = Vector3.Cross(hit.normal, forward.normalized) * ((leftFoot ? -0.1f : 0.1f) * Scale);
            GameObject footprint = Extras.Primitive(PrimitiveType.Cylinder, color, new Vector3(0.12f, 0.003f, 0.18f) * Scale);
            footprint.transform.SetPositionAndRotation(hit.point + side + hit.normal * 0.01f, Quaternion.LookRotation(forward.normalized, hit.normal));
            prints.Add((footprint, now));
        }

        public static void DisableGlowingFootprints()
        {
            foreach (var (print, _) in prints)
                Destroy(print);
            prints.Clear();
        }

        private static readonly GameObject[] discoOrbs = new GameObject[3];

        /// <summary>Three coloured lights circling you.</summary>
        public static void DiscoLights()
        {
            for (int i = 0; i < discoOrbs.Length; i++)
            {
                if (discoOrbs[i] == null)
                {
                    discoOrbs[i] = Extras.Primitive(PrimitiveType.Sphere, Color.white, Vector3.one);
                    Light light = discoOrbs[i].AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 6f;
                    light.intensity = 3f;
                }

                float angle = Time.time * 1.5f + i * Mathf.PI * 2f / discoOrbs.Length;
                Color color = Color.HSVToRGB((Time.time * 0.3f + (float)i / discoOrbs.Length) % 1f, 1f, 1f);

                discoOrbs[i].transform.position = Extras.BodyPosition + new Vector3(Mathf.Cos(angle) * 1.5f, 1.2f, Mathf.Sin(angle) * 1.5f) * Scale;
                discoOrbs[i].transform.localScale = Vector3.one * (0.1f * Scale);
                discoOrbs[i].GetComponent<Light>().color = color;
                Tint(discoOrbs[i], color);
            }
        }

        public static void DisableDiscoLights()
        {
            for (int i = 0; i < discoOrbs.Length; i++)
            {
                Destroy(discoOrbs[i]);
                discoOrbs[i] = null;
            }
        }

        private static GameObject aura;

        /// <summary>A soft light in your colour around you.</summary>
        public static void GlowAura()
        {
            if (aura == null)
            {
                aura = new GameObject("Nova_GlowAura");
                Light light = aura.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 4f;
                light.intensity = 2f;
            }

            aura.transform.position = Extras.BodyPosition;
            aura.GetComponent<Light>().color = MyColor;
        }

        public static void DisableGlowAura()
        {
            if (aura != null)
                Object.Destroy(aura);
            aura = null;
        }

        private static ParticleSystem snow;

        /// <summary>Snow falling just around you.</summary>
        public static void PersonalSnow()
        {
            if (snow == null)
                snow = MakeParticles("Nova_Snow", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startLifetime = 4f;
                    main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
                    main.startColor = Color.white;
                    main.gravityModifier = 0.06f;
                    main.maxParticles = 600;

                    var emission = system.emission;
                    emission.rateOverTime = 90f;

                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(6f, 0.1f, 6f);
                });

            snow.transform.position = Head.position + Vector3.up * 2.5f;
        }

        public static void DisablePersonalSnow()
        {
            if (snow != null)
                Object.Destroy(snow.gameObject);
            snow = null;
        }

        // ── Wrist gadgets ───────────────────────────────────────────────────────

        private static TextMeshPro MakeWristText(string name)
        {
            TextMeshPro text = new GameObject(name).AddComponent<TextMeshPro>();
            text.fontSize = 4.8f;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            return text;
        }

        /// <summary>Floats text above your left wrist, turned to face you.</summary>
        private static void PlaceOnWrist(TextMeshPro text, float height, bool rightWrist = false)
        {
            Transform wrist = rightWrist ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;

            text.SafeSetFont(activeFont);
            text.transform.localScale = Vector3.one * (0.08f * Scale);
            text.transform.position = wrist.position + Vector3.up * (height * Scale);
            text.transform.LookAt(Camera.main.transform.position);
            text.transform.Rotate(0f, 180f, 0f);
        }

        private static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private static string Heading()
        {
            Vector3 look = LookFlat();
            float degrees = (Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg + 360f) % 360f;
            return Compass[Mathf.RoundToInt(degrees / 45f) % Compass.Length];
        }

        private static TextMeshPro watch;
        private static float watchNextText;

        /// <summary>The time, your speed and the way you face, above your left wrist.</summary>
        public static void WristWatch()
        {
            if (watch == null)
            {
                watch = MakeWristText("Nova_WristWatch");
                watch.color = Color.white;
                watchNextText = 0f;
            }

            // The text changes ten times a second; rebuilding it every frame only made garbage.
            if (Time.time >= watchNextText)
            {
                watchNextText = Time.time + 0.1f;
                watch.SafeSetText($"{DateTime.Now:HH:mm}\n<size=70%>{Extras.Body.linearVelocity.magnitude:0.0} m/s  ·  {Heading()}</size>");
            }

            PlaceOnWrist(watch, 0.12f);
        }

        public static void DisableWristWatch()
        {
            if (watch != null)
                Object.Destroy(watch.gameObject);
            watch = null;
        }

        private static TextMeshPro stopwatchText;
        private static float stopwatchStart, stopwatchElapsed, stopwatchNextText;
        private static bool stopwatchRunning, stopwatchHeld, stopwatchResetHeld;

        /// <summary>X starts and stops a stopwatch on your wrist; Y resets it.</summary>
        public static void Stopwatch()
        {
            if (leftPrimary && !stopwatchHeld)
            {
                if (stopwatchRunning)
                    stopwatchElapsed += Time.time - stopwatchStart;
                else
                    stopwatchStart = Time.time;

                stopwatchRunning = !stopwatchRunning;
            }

            if (leftSecondary && !stopwatchResetHeld)
            {
                stopwatchElapsed = 0f;
                stopwatchStart = Time.time;
            }

            stopwatchHeld = leftPrimary;
            stopwatchResetHeld = leftSecondary;

            if (stopwatchText == null)
                stopwatchText = MakeWristText("Nova_Stopwatch");

            // Hundredths can't be read at a glance anyway; twenty updates a second is plenty.
            if (Time.time >= stopwatchNextText)
            {
                stopwatchNextText = Time.time + 0.05f;
                TimeSpan time = TimeSpan.FromSeconds(stopwatchElapsed + (stopwatchRunning ? Time.time - stopwatchStart : 0f));
                stopwatchText.SafeSetText($"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}");
                stopwatchText.color = stopwatchRunning ? Color.green : Color.white;
            }

            // Sits above the watch when both are on.
            PlaceOnWrist(stopwatchText, watch != null ? 0.2f : 0.12f);
        }

        public static void DisableStopwatch()
        {
            if (stopwatchText != null)
                Object.Destroy(stopwatchText.gameObject);
            stopwatchText = null;
            stopwatchRunning = false;
            stopwatchElapsed = 0f;
        }
    }
}
