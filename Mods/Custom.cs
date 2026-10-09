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
    /// Everything here acts on you alone: how you move, and things only you see. The
    /// class is split over four files; this one holds the shared helpers and the first
    /// set of mods.
    /// </remarks>
    public static partial class Custom
    {
        public const string Category = "Custom Mods";

        private static Transform Head => GorillaTagger.Instance.headCollider.transform;
        private static float Scale => GTPlayer.Instance.scale;

        /// <summary>Where you are looking, flattened onto the ground.</summary>
        private static Vector3 LookFlat()
        {
            Vector3 look = Head.forward;
            look.y = 0f;
            return look.sqrMagnitude < 0.0001f ? Vector3.forward : look.normalized;
        }

        // ── Colour ──────────────────────────────────────────────────────────────

        public static readonly string[] ColorNames = { "Mine", "Rainbow", "Gold", "Ice", "Fire", "Galaxy" };
        private static int colorMode;

        public static void SetColorMode(int mode) =>
            colorMode = Mathf.Clamp(mode, 0, ColorNames.Length - 1);

        /// <summary>The colour every Custom mod draws in, chosen with Custom Mods Colour.</summary>
        private static Color MyColor
        {
            get
            {
                float t = Time.time;
                switch (colorMode)
                {
                    case 1: return Color.HSVToRGB(t * 0.15f % 1f, 0.85f, 1f);
                    case 2: return Color.Lerp(new Color(1f, 0.75f, 0.1f), new Color(1f, 0.95f, 0.5f), Mathf.PingPong(t * 0.5f, 1f));
                    case 3: return Color.Lerp(new Color(0.55f, 0.85f, 1f), Color.white, Mathf.PingPong(t * 0.4f, 1f));
                    case 4: return Color.Lerp(new Color(1f, 0.25f, 0f), new Color(1f, 0.8f, 0.1f), Mathf.PingPong(t * 1.5f, 1f));
                    case 5: return Color.Lerp(new Color(0.45f, 0.1f, 0.9f), new Color(0.1f, 0.4f, 1f), Mathf.PingPong(t * 0.3f, 1f));
                    default: return VRRig.LocalRig.playerColor;
                }
            }
        }

        // ── Materials and pieces ────────────────────────────────────────────────

        private static Material vertexColored;

        /// <summary>A material that takes its colour from what it draws, for particles and lines.</summary>
        private static Material VertexColored
        {
            get
            {
                if (vertexColored == null)
                {
                    Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    vertexColored = shader != null ? new Material(shader) : Extras.Glow(Color.white);
                }

                return vertexColored;
            }
        }

        /// <summary>One visual thing these mods keep track of, with its renderer kept to hand.</summary>
        private sealed class Piece
        {
            public GameObject Object;
            public Renderer Renderer;
            public Vector3 Velocity;
            public float Born;
            public float Life;
            public float Size;
            public float Seed;
            public Vector3 Anchor;

            public bool Alive => Object != null;
        }

        private static Piece MakePiece(PrimitiveType type, Color color, Vector3 scale)
        {
            GameObject made = Extras.Primitive(type, color, scale);
            return new Piece { Object = made, Renderer = made.GetComponent<Renderer>(), Born = Time.time, Seed = Random.value * 100f };
        }

        /// <summary>Destroys a piece's object and the material it was given.</summary>
        private static void Discard(Piece piece)
        {
            if (piece == null)
                return;

            GameObject made = piece.Object;
            Extras.Clear(ref made);
            piece.Object = null;
        }

        private static void ClearPieces(List<Piece> pieces)
        {
            foreach (Piece piece in pieces)
                Discard(piece);
            pieces.Clear();
        }

        // Each primitive owns the material Extras.Primitive gave it, so the shared material
        // is coloured directly; reading .material would make a copy and leak the original.
        private static void SetColor(Piece piece, Color color) =>
            piece.Renderer.sharedMaterial.color = color;

        private static void SetAlpha(Piece piece, Color color, float alpha) =>
            piece.Renderer.sharedMaterial.color = new Color(color.r, color.g, color.b, alpha);

        /// <summary>A live piece: the one given if it still exists, otherwise a new one.</summary>
        private static Piece Ensure(ref Piece piece, PrimitiveType type, Color color)
        {
            if (piece == null || !piece.Alive)
                piece = MakePiece(type, color, Vector3.one);
            return piece;
        }

        /// <summary>True on the frame a button goes down.</summary>
        private static bool Pressed(bool down, ref bool held)
        {
            bool pressed = down && !held;
            held = down;
            return pressed;
        }

        private static LineRenderer MakeLine(string name, float width)
        {
            LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
            line.material = VertexColored;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            return line;
        }

        private static void DestroyLine(ref LineRenderer line)
        {
            if (line != null)
                Object.Destroy(line.gameObject);
            line = null;
        }

        // ── State reset ─────────────────────────────────────────────────────────

        /// <summary>Forgets what the stateful mods remembered, run whenever one of them is turned on.</summary>
        /// <remarks>
        /// Without it a mod picked up where it left off: Ice Skates relaunched you at an old
        /// speed, the bounce mods bounced off a fall from before, and a button held while
        /// turning a mod on counted as a press.
        /// </remarks>
        public static void ResetState()
        {
            skateVelocity = Vector3.zero;
            trampolineFall = lastFallSpeed = 0f;
            trampolineGrounded = wasGrounded = true;
            airDashUsed = false;

            airDashHeld = fireworkHeld = confettiHeld = true;
            carpetHeld = wandHeld = ballHeld = targetHeld = reactionHeld = pianoHeld = true;
            stopwatchHeld = stopwatchResetHeld = true;
            fishHeld = splatHeld = true;

            lastLeftHand = GorillaTagger.Instance.leftHandTransform.position;
            lastRightHand = GorillaTagger.Instance.rightHandTransform.position;
        }

        // ── Particles ───────────────────────────────────────────────────────────

        /// <summary>A particle system that grows and shrinks with you through its transform.</summary>
        private static ParticleSystem MakeParticles(string name, Transform parent, Action<ParticleSystem> configure)
        {
            GameObject holder = new GameObject(name);
            if (parent != null)
                holder.transform.SetParent(parent, false);
            else
                holder.transform.localScale = Vector3.one * Scale;

            ParticleSystem particles = holder.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            holder.GetComponent<ParticleSystemRenderer>().material = VertexColored;

            var main = particles.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            configure(particles);
            particles.Play();
            return particles;
        }

        private static void DestroyParticles(ref ParticleSystem particles)
        {
            if (particles != null)
                Object.Destroy(particles.gameObject);
            particles = null;
        }

        /// <summary>Moves a free-standing particle system and keeps it at your size.</summary>
        private static void Follow(ParticleSystem particles, Vector3 position)
        {
            particles.transform.position = position;
            particles.transform.localScale = Vector3.one * Scale;
        }

        private static ParticleSystem.MinMaxGradient? rainbow;

        private static ParticleSystem.MinMaxGradient Rainbow
        {
            get
            {
                if (rainbow == null)
                {
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new[]
                        {
                            new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 0.2f), new GradientColorKey(Color.green, 0.4f),
                            new GradientColorKey(Color.cyan, 0.6f), new GradientColorKey(Color.blue, 0.8f), new GradientColorKey(Color.magenta, 1f)
                        },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

                    rainbow = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
                }

                return rainbow.Value;
            }
        }

        // Every burst in every Custom mod is drawn by these two systems, one for sparks that
        // float and one for sparks that fall, instead of a new particle system per pop.
        private static ParticleSystem floatingSparks, fallingSparks;

        private static ParticleSystem Sparks(bool falling)
        {
            ref ParticleSystem system = ref falling ? ref fallingSparks : ref floatingSparks;
            if (system == null)
            {
                system = MakeParticles(falling ? "Nova_FallingSparks" : "Nova_FloatingSparks", null, particles =>
                {
                    var main = particles.main;
                    main.loop = true;
                    main.gravityModifier = falling ? 0.5f : 0f;
                    main.maxParticles = 1500;
                    main.scalingMode = ParticleSystemScalingMode.Local;

                    var emission = particles.emission;
                    emission.enabled = false;
                });

                system.transform.localScale = Vector3.one;
                Object.DontDestroyOnLoad(system.gameObject);
            }

            return system;
        }

        /// <summary>A one-off spray of rainbow sparks.</summary>
        /// <param name="direction">Sprays in a cone this way; without one, in every direction.</param>
        private static void Burst(Vector3 at, int count, float speed, bool falling, Vector3? direction = null)
        {
            ParticleSystem system = Sparks(falling);
            float scale = Scale;
            ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams { position = at };

            for (int i = 0; i < count; i++)
            {
                Vector3 heading = direction == null
                    ? Random.onUnitSphere
                    : Vector3.Slerp(direction.Value.normalized, Random.onUnitSphere, Random.Range(0f, 0.3f)).normalized;

                spark.velocity = heading * (Random.Range(0.4f, 1f) * speed * scale);
                spark.startSize = Random.Range(0.02f, 0.05f) * scale;
                spark.startLifetime = Random.Range(0.8f, 1.6f);
                spark.startColor = Color.HSVToRGB(Random.value, 0.85f, 1f);
                system.Emit(spark, 1);
            }
        }

        // ── Wrist text ──────────────────────────────────────────────────────────

        private static TextMeshPro MakeWristText(string name, Color color)
        {
            TextMeshPro text = new GameObject(name).AddComponent<TextMeshPro>();
            text.fontSize = 4.8f;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            text.color = color;
            return text;
        }

        private static void DestroyText(ref TextMeshPro text)
        {
            if (text != null)
                Object.Destroy(text.gameObject);
            text = null;
        }

        /// <summary>Floats text above a wrist, turned to face you.</summary>
        /// <remarks>Each gadget has its own height so several can be on at once.</remarks>
        private static void PlaceOnWrist(TextMeshPro text, float height, bool rightWrist = false)
        {
            Transform wrist = rightWrist ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;

            text.SafeSetFont(activeFont);
            text.transform.localScale = Vector3.one * (0.08f * Scale);
            text.transform.position = wrist.position + Vector3.up * (height * Scale);
            text.transform.LookAt(Head.position);
            text.transform.Rotate(0f, 180f, 0f);
        }

        // ── Movement ────────────────────────────────────────────────────────────

        private static Vector3? ropePoint;
        private static float ropeLength;
        private static LineRenderer rope;

        /// <summary>Hold the right trigger to fire a rope from your right hand and swing on it; squeeze fully to reel in.</summary>
        public static void RopeSwing()
        {
            var hand = ControllerUtilities.GetTrueRightHand();
            float scale = Scale;

            if (rightTrigger < 0.5f)
            {
                ropePoint = null;
                if (rope != null)
                    rope.enabled = false;
                return;
            }

            if (ropePoint == null)
            {
                if (!Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, 60f * scale, NoInvisLayerMask()))
                    return;

                ropePoint = hit.point;
                ropeLength = Vector3.Distance(Extras.BodyPosition, hit.point);
            }

            Vector3 toPoint = ropePoint.Value - Extras.BodyPosition;
            float distance = toPoint.magnitude;
            Vector3 along = toPoint / Mathf.Max(distance, 0.001f);

            if (rightTrigger > 0.9f)
                ropeLength = Mathf.Max(1f * scale, ropeLength - 4f * scale * Time.deltaTime);

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
                rope = MakeLine("Nova_Rope", 0.025f);

            rope.enabled = true;
            rope.startWidth = rope.endWidth = 0.025f * scale;
            rope.startColor = rope.endColor = MyColor;
            rope.SetPosition(0, hand.position);
            rope.SetPosition(1, ropePoint.Value);
        }

        public static void DisableRopeSwing()
        {
            ropePoint = null;
            DestroyLine(ref rope);
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

            if (Pressed(rightSecondary, ref airDashHeld) && !grounded && !airDashUsed)
            {
                Extras.Body.linearVelocity = Head.forward * 12f;
                airDashUsed = true;
            }
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

        private static Piece leftWing, rightWing;

        /// <summary>Glowing wings on your back that flap while you are in the air.</summary>
        public static void Wings()
        {
            Ensure(ref leftWing, PrimitiveType.Cube, MyColor);
            Ensure(ref rightWing, PrimitiveType.Cube, MyColor);

            float flap = Extras.Grounded() ? 10f : 15f + 30f * Mathf.Sin(Time.time * 8f);
            Quaternion facing = Quaternion.LookRotation(LookFlat());
            Color color = MyColor;

            PlaceWing(leftWing, facing, -1f, flap, color);
            PlaceWing(rightWing, facing, 1f, flap, color);
        }

        private static void PlaceWing(Piece wing, Quaternion facing, float side, float flap, Color color)
        {
            float scale = Scale;
            Transform body = wing.Object.transform;

            // Hinged at the inner edge, so a flap lifts the tip rather than spinning the middle.
            Vector3 root = Extras.BodyPosition + facing * new Vector3(side * 0.12f, 0.15f, -0.18f) * scale;
            body.rotation = facing * Quaternion.Euler(0f, side * -20f, side * flap);
            body.position = root + body.rotation * (Vector3.right * (side * 0.3f * scale));
            body.localScale = new Vector3(0.6f, 0.02f, 0.25f) * scale;
            SetAlpha(wing, color, 0.6f);
        }

        public static void DisableWings()
        {
            Discard(leftWing);
            Discard(rightWing);
            leftWing = rightWing = null;
        }

        private static Piece pet;

        /// <summary>A glowing orb in your colour that floats along beside you.</summary>
        public static void PetOrb()
        {
            Vector3 target = Head.position
                + Quaternion.LookRotation(LookFlat()) * new Vector3(0.45f, 0.1f, 0.3f) * Scale
                + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.06f * Scale);

            if (pet == null || !pet.Alive)
            {
                pet = MakePiece(PrimitiveType.Sphere, MyColor, Vector3.one);
                pet.Object.transform.position = target;
            }

            Transform body = pet.Object.transform;
            body.position = Vector3.Lerp(body.position, target, Time.deltaTime * 4f);
            body.localScale = Vector3.one * (0.12f * Scale);
            SetColor(pet, MyColor);
        }

        public static void DisablePetOrb()
        {
            Discard(pet);
            pet = null;
        }

        private static Piece saber;

        /// <summary>A glowing blade in your colour out of your right hand.</summary>
        public static void Lightsaber()
        {
            Ensure(ref saber, PrimitiveType.Cylinder, MyColor);

            var hand = ControllerUtilities.GetTrueRightHand();
            float length = 0.9f * Scale;
            Transform blade = saber.Object.transform;

            // A cylinder is two units tall along its own up, so it is turned to point forward.
            blade.rotation = Quaternion.LookRotation(hand.forward) * Quaternion.Euler(90f, 0f, 0f);
            blade.localScale = new Vector3(0.035f * Scale, length / 2f, 0.035f * Scale);
            blade.position = hand.position + hand.forward * (length / 2f + 0.05f * Scale);

            SetAlpha(saber, MyColor, 0.75f + 0.15f * Mathf.Sin(Time.time * 20f));
        }

        public static void DisableLightsaber()
        {
            Discard(saber);
            saber = null;
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
                main.startColor = Rainbow;
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
            DestroyParticles(ref leftSparkles);
            DestroyParticles(ref rightSparkles);
        }

        private static bool fireworkHeld;

        /// <summary>B launches a firework from your right hand.</summary>
        public static void Fireworks()
        {
            if (Pressed(rightSecondary, ref fireworkHeld))
                CoroutineManager.instance.StartCoroutine(LaunchFirework(ControllerUtilities.GetTrueRightHand().position));
        }

        private static IEnumerator LaunchFirework(Vector3 from)
        {
            Piece rocket = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one * (0.06f * Scale));
            Vector3 drift = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            float start = Time.time;
            float scale = Scale;

            while (Time.time - start < 1.1f)
            {
                if (!rocket.Alive)
                    yield break;

                rocket.Object.transform.position = from + (Vector3.up * 9f + drift) * ((Time.time - start) * scale);
                yield return null;
            }

            if (!rocket.Alive)
                yield break;

            Vector3 at = rocket.Object.transform.position;
            Discard(rocket);
            Burst(at, 150, 6f, falling: true);
        }

        private static bool confettiHeld;

        /// <summary>A pops a burst of confetti from your right hand.</summary>
        public static void ConfettiPopper()
        {
            if (!Pressed(rightPrimary, ref confettiHeld))
                return;

            var hand = ControllerUtilities.GetTrueRightHand();
            Burst(hand.position + hand.forward * (0.1f * Scale), 90, 4f, falling: true, hand.forward);
        }

        private const int FootprintCount = 14;
        private static readonly List<Piece> prints = new List<Piece>();
        private static int nextPrint;
        private static float printTimer;
        private static bool leftFoot;

        /// <summary>Glowing prints where you walk, fading after a few seconds.</summary>
        public static void GlowingFootprints()
        {
            float now = Time.time;
            Color color = MyColor;

            if (prints.Count == 0)
                for (int i = 0; i < FootprintCount; i++)
                {
                    Piece print = MakePiece(PrimitiveType.Cylinder, color, Vector3.one);
                    print.Object.SetActive(false);
                    prints.Add(print);
                }

            foreach (Piece print in prints)
            {
                if (!print.Alive || !print.Object.activeSelf)
                    continue;

                float age = (now - print.Born) / 4f;
                if (age >= 1f)
                    print.Object.SetActive(false);
                else
                    SetAlpha(print, color, 0.8f * (1f - age));
            }

            Vector3 velocity = Extras.Body.linearVelocity;
            velocity.y = 0f;

            if (now < printTimer || velocity.magnitude < 1f)
                return;

            if (!Physics.Raycast(Extras.BodyPosition, Vector3.down, out RaycastHit hit, 1.5f * Scale, GTPlayer.Instance.locomotionEnabledLayers))
                return;

            Vector3 forward = Vector3.ProjectOnPlane(velocity, hit.normal);
            if (forward.sqrMagnitude < 0.0001f)
                return;

            printTimer = now + 0.3f;
            leftFoot = !leftFoot;

            // The oldest print is reused for the newest step.
            int index = nextPrint;
            nextPrint = (nextPrint + 1) % prints.Count;
            if (!prints[index].Alive)
                prints[index] = MakePiece(PrimitiveType.Cylinder, color, Vector3.one);
            Piece step = prints[index];

            Vector3 side = Vector3.Cross(hit.normal, forward.normalized) * ((leftFoot ? -0.1f : 0.1f) * Scale);
            step.Born = now;
            step.Object.SetActive(true);
            step.Object.transform.SetPositionAndRotation(hit.point + side + hit.normal * 0.01f, Quaternion.LookRotation(forward.normalized, hit.normal));
            step.Object.transform.localScale = new Vector3(0.12f, 0.003f, 0.18f) * Scale;
        }

        public static void DisableGlowingFootprints() => ClearPieces(prints);

        private static readonly List<Piece> discoOrbs = new List<Piece>();
        private static readonly List<Light> discoLights = new List<Light>();

        /// <summary>Three coloured lights circling you.</summary>
        public static void DiscoLights()
        {
            if (discoOrbs.Count == 0)
                for (int i = 0; i < 3; i++)
                {
                    Piece orb = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one);
                    Light light = orb.Object.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 6f;
                    light.intensity = 3f;
                    discoOrbs.Add(orb);
                    discoLights.Add(light);
                }

            for (int i = 0; i < discoOrbs.Count; i++)
            {
                Piece orb = discoOrbs[i];
                if (!orb.Alive)
                    continue;

                float angle = Time.time * 1.5f + i * Mathf.PI * 2f / discoOrbs.Count;
                Color color = Color.HSVToRGB((Time.time * 0.3f + (float)i / discoOrbs.Count) % 1f, 1f, 1f);

                orb.Object.transform.position = Extras.BodyPosition + new Vector3(Mathf.Cos(angle) * 1.5f, 1.2f, Mathf.Sin(angle) * 1.5f) * Scale;
                orb.Object.transform.localScale = Vector3.one * (0.1f * Scale);
                discoLights[i].color = color;
                discoLights[i].range = 6f * Scale;
                SetColor(orb, color);
            }
        }

        public static void DisableDiscoLights()
        {
            ClearPieces(discoOrbs);
            discoLights.Clear();
        }

        private static Light aura;

        /// <summary>A soft light in your colour around you.</summary>
        public static void GlowAura()
        {
            if (aura == null)
            {
                aura = new GameObject("Nova_GlowAura").AddComponent<Light>();
                aura.type = LightType.Point;
                aura.intensity = 2f;
            }

            aura.transform.position = Extras.BodyPosition;
            aura.range = 4f * Scale;
            aura.color = MyColor;
        }

        public static void DisableGlowAura()
        {
            if (aura != null)
                Object.Destroy(aura.gameObject);
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
                    // Particle speed and size grow with you through the transform, gravity doesn't.
                    main.gravityModifier = 0.06f * Scale;
                    main.maxParticles = 600;

                    var emission = system.emission;
                    emission.rateOverTime = 90f;

                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(6f, 0.1f, 6f);
                });

            Follow(snow, Head.position + Vector3.up * (2.5f * Scale));
        }

        public static void DisablePersonalSnow() => DestroyParticles(ref snow);

        // ── Wrist gadgets ───────────────────────────────────────────────────────

        private static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private static string Heading()
        {
            Vector3 look = LookFlat();
            float degrees = (Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg + 360f) % 360f;
            return Compass[Mathf.RoundToInt(degrees / 45f) % Compass.Length];
        }

        private static TextMeshPro watch;
        private static float watchNextText;
        private static float fpsSmoothed = 72f;

        /// <summary>The time, your speed, the way you face and your frame rate, above your left wrist.</summary>
        public static void WristWatch()
        {
            if (watch == null)
            {
                watch = MakeWristText("Nova_WristWatch", Color.white);
                watchNextText = 0f;
            }

            if (Time.unscaledDeltaTime > 0f)
                fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / Time.unscaledDeltaTime, 0.05f);

            // The text changes ten times a second; rebuilding it every frame only made garbage.
            if (Time.time >= watchNextText)
            {
                watchNextText = Time.time + 0.1f;
                watch.SafeSetText($"{DateTime.Now:HH:mm}\n<size=70%>{Extras.Body.linearVelocity.magnitude:0.0} m/s  ·  {Heading()}  ·  {fpsSmoothed:0} FPS</size>");
            }

            PlaceOnWrist(watch, 0.12f);
        }

        public static void DisableWristWatch() => DestroyText(ref watch);

        private static TextMeshPro stopwatchText;
        private static float stopwatchStart, stopwatchElapsed, stopwatchNextText;
        private static bool stopwatchRunning, stopwatchHeld, stopwatchResetHeld;

        /// <summary>X starts and stops a stopwatch on your wrist; Y resets it.</summary>
        public static void Stopwatch()
        {
            if (Pressed(leftPrimary, ref stopwatchHeld))
            {
                if (stopwatchRunning)
                    stopwatchElapsed += Time.time - stopwatchStart;
                else
                    stopwatchStart = Time.time;

                stopwatchRunning = !stopwatchRunning;
                stopwatchNextText = 0f;
            }

            if (Pressed(leftSecondary, ref stopwatchResetHeld))
            {
                stopwatchElapsed = 0f;
                stopwatchStart = Time.time;
                stopwatchNextText = 0f;
            }

            if (stopwatchText == null)
                stopwatchText = MakeWristText("Nova_Stopwatch", Color.white);

            // Hundredths can't be read at a glance anyway; twenty updates a second is plenty.
            if (Time.time >= stopwatchNextText)
            {
                stopwatchNextText = Time.time + 0.05f;
                TimeSpan time = TimeSpan.FromSeconds(stopwatchElapsed + (stopwatchRunning ? Time.time - stopwatchStart : 0f));
                stopwatchText.SafeSetText($"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}");
                stopwatchText.color = stopwatchRunning ? Color.green : Color.white;
            }

            PlaceOnWrist(stopwatchText, 0.2f);
        }

        public static void DisableStopwatch()
        {
            DestroyText(ref stopwatchText);
            stopwatchRunning = false;
            stopwatchElapsed = 0f;
        }
    }
}
