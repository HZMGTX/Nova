/*
 * Nova Menu  Mods/CustomFun.cs
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
using Nova.Classes.Menu;
using Nova.Extensions;
using Nova.Managers;
using Nova.Menu;
using Nova.Utilities;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static Nova.Menu.Main;
using Random = UnityEngine.Random;

namespace Nova.Mods
{
    /// <summary>The second set of Custom Mods: toys, art, critters and mini-games.</summary>
    /// <remarks>
    /// Everything here is only seen by you. Anything that makes many objects keeps them in
    /// a capped list and reuses them, and bursts go through two shared particle systems,
    /// so leaving these on does not cost frames.
    /// </remarks>
    public static partial class Custom
    {
        // ── Movement toys ───────────────────────────────────────────────────────

        private static ParticleSystem jetFlame;

        /// <summary>Hold your left trigger to fly up on a jetpack.</summary>
        public static void Jetpack()
        {
            bool firing = leftTrigger > 0.5f;

            if (firing)
            {
                Vector3 velocity = Extras.Body.linearVelocity;
                if (velocity.y < 8f)
                    velocity.y += 18f * Time.deltaTime;
                Extras.Body.linearVelocity = velocity;
            }

            if (jetFlame == null)
                jetFlame = MakeParticles("Nova_JetFlame", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startLifetime = 0.35f;
                    main.startSpeed = 3f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0f));
                    main.maxParticles = 120;

                    var emission = system.emission;
                    emission.rateOverTime = 120f;

                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 12f;
                    shape.radius = 0.03f;
                });

            var flame = jetFlame.emission;
            flame.enabled = firing;
            Follow(jetFlame, Extras.BodyPosition - LookFlat() * (0.2f * Scale) + Vector3.down * (0.1f * Scale));
            jetFlame.transform.rotation = Quaternion.LookRotation(Vector3.down);
        }

        public static void DisableJetpack() => DestroyParticles(ref jetFlame);

        private static Vector3 skateVelocity;

        /// <summary>The ground turns to ice: you keep sliding instead of stopping.</summary>
        /// <remarks>
        /// Only the gentle slowing of friction is undone. A sharp stop (a wall) or a turn is
        /// left alone, so you can still steer and never get pinned against things.
        /// </remarks>
        public static void IceSkates()
        {
            Vector3 velocity = Extras.Body.linearVelocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            float kept = skateVelocity.magnitude;

            if (Extras.Grounded() && speed < kept && speed > kept * 0.5f && Vector3.Dot(flat / Mathf.Max(speed, 0.001f), skateVelocity / kept) > 0.9f)
            {
                Vector3 glide = flat / speed * (kept * Mathf.Pow(0.99f, Time.deltaTime * 72f));
                velocity.x = glide.x;
                velocity.z = glide.z;
                Extras.Body.linearVelocity = velocity;
                flat = glide;
            }

            skateVelocity = flat.sqrMagnitude > 0.04f * Scale * Scale ? flat : Vector3.zero;
        }

        private static float trampolineFall;
        private static bool trampolineGrounded = true;

        /// <summary>Every landing bounces you back up, like the floor is a trampoline.</summary>
        public static void TrampolineFeet()
        {
            bool grounded = Extras.Grounded();
            Vector3 velocity = Extras.Body.linearVelocity;

            if (grounded && !trampolineGrounded && trampolineFall > 1.5f)
                Extras.Body.linearVelocity = new Vector3(velocity.x, Mathf.Max(5f, trampolineFall * 0.9f), velocity.z);

            trampolineFall = grounded ? 0f : Mathf.Max(0f, -velocity.y);
            trampolineGrounded = grounded;
        }

        private static Piece balloon;
        private static LineRenderer balloonString;

        /// <summary>Hold a balloon that makes you float gently while you are in the air.</summary>
        public static void BalloonFloat()
        {
            Ensure(ref balloon, PrimitiveType.Sphere, Color.red);
            if (balloonString == null)
            {
                balloonString = MakeLine("Nova_BalloonString", 0.005f);
                balloonString.startColor = balloonString.endColor = Color.white;
            }

            float scale = Scale;
            Transform hand = GorillaTagger.Instance.leftHandTransform;
            Vector3 sway = new Vector3(Mathf.Sin(Time.time * 1.3f), 0f, Mathf.Cos(Time.time * 1.1f)) * (0.08f * scale);
            Vector3 top = hand.position + Vector3.up * (0.6f * scale) + sway;

            balloon.Object.transform.position = top;
            balloon.Object.transform.localScale = new Vector3(0.28f, 0.34f, 0.28f) * scale;
            SetAlpha(balloon, Color.HSVToRGB(Time.time * 0.05f % 1f, 0.8f, 1f), 0.85f);

            balloonString.startWidth = balloonString.endWidth = 0.005f * scale;
            balloonString.SetPosition(0, hand.position);
            balloonString.SetPosition(1, top - Vector3.up * (0.17f * scale));

            if (!Extras.Grounded())
                Extras.Body.linearVelocity += Vector3.up * (6f * Time.deltaTime);
        }

        public static void DisableBalloonFloat()
        {
            Discard(balloon);
            balloon = null;
            DestroyLine(ref balloonString);
        }

        private static Piece carpet;
        private static bool carpetRiding, carpetHeld;

        /// <summary>Press Y to hop on a magic carpet that flies the way you look; Y again to hop off.</summary>
        public static void MagicCarpet()
        {
            if (Pressed(leftSecondary, ref carpetHeld))
                carpetRiding = !carpetRiding;

            if (!carpetRiding)
            {
                if (carpet != null && carpet.Alive)
                    carpet.Object.SetActive(false);
                return;
            }

            Ensure(ref carpet, PrimitiveType.Cube, MyColor);
            carpet.Object.SetActive(true);

            Extras.Body.linearVelocity = LookFlat() * 5f + Vector3.up * (Head.forward.y * 4f);

            carpet.Object.transform.SetPositionAndRotation(
                Extras.BodyPosition + Vector3.down * (0.55f * Scale),
                Quaternion.LookRotation(LookFlat()) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 3f));
            carpet.Object.transform.localScale = new Vector3(0.9f, 0.03f, 1.3f) * Scale;
            SetAlpha(carpet, MyColor, 0.9f);
        }

        public static void DisableMagicCarpet()
        {
            carpetRiding = false;
            Discard(carpet);
            carpet = null;
        }

        // ── Creative ────────────────────────────────────────────────────────────

        private const int MaxStrokes = 40;
        private const int MaxStrokePoints = 400;

        private static readonly List<LineRenderer> strokes = new List<LineRenderer>();
        private static LineRenderer currentStroke;
        private static Vector3 lastPaintPoint;

        /// <summary>Hold your right trigger to draw glowing lines in the air.</summary>
        public static void AirPaint()
        {
            Vector3 tip = ControllerUtilities.GetTrueRightHand().position;

            if (rightTrigger < 0.5f)
            {
                // A tap that drew nothing leaves nothing behind, so it can't push real
                // drawings out of the list.
                if (currentStroke != null && currentStroke.positionCount <= 1)
                {
                    strokes.Remove(currentStroke);
                    Object.Destroy(currentStroke.gameObject);
                }

                currentStroke = null;
                return;
            }

            if (currentStroke == null || currentStroke.positionCount >= MaxStrokePoints)
            {
                // The oldest drawing makes room once there are too many to keep.
                if (strokes.Count >= MaxStrokes)
                {
                    if (strokes[0] != null)
                        Object.Destroy(strokes[0].gameObject);
                    strokes.RemoveAt(0);
                }

                currentStroke = MakeLine("Nova_PaintStroke", 0.012f * Scale);
                currentStroke.numCapVertices = 4;
                currentStroke.numCornerVertices = 2;
                currentStroke.startColor = currentStroke.endColor = MyColor;
                currentStroke.positionCount = 1;
                currentStroke.SetPosition(0, tip);
                strokes.Add(currentStroke);
                lastPaintPoint = tip;
                return;
            }

            if ((tip - lastPaintPoint).sqrMagnitude < 0.0001f * Scale * Scale)
                return;

            currentStroke.positionCount++;
            currentStroke.SetPosition(currentStroke.positionCount - 1, tip);
            lastPaintPoint = tip;
        }

        public static void ClearAirPaint()
        {
            foreach (LineRenderer stroke in strokes)
                if (stroke != null)
                    Object.Destroy(stroke.gameObject);

            strokes.Clear();
            currentStroke = null;
        }

        private static LineRenderer laser;
        private static Piece laserDot;

        /// <summary>A red laser from your right hand with a dot where it lands.</summary>
        public static void LaserPointer()
        {
            var hand = ControllerUtilities.GetTrueRightHand();

            if (laser == null)
            {
                laser = MakeLine("Nova_Laser", 0.004f);
                laser.startColor = laser.endColor = new Color(1f, 0.1f, 0.1f, 0.8f);
            }

            Ensure(ref laserDot, PrimitiveType.Sphere, Color.red);

            float range = 50f * Scale;
            bool hit = Physics.Raycast(hand.position, hand.forward, out RaycastHit ray, range, NoInvisLayerMask());
            Vector3 end = hit ? ray.point : hand.position + hand.forward * range;

            laser.startWidth = laser.endWidth = 0.004f * Scale;
            laser.SetPosition(0, hand.position);
            laser.SetPosition(1, end);

            laserDot.Object.SetActive(hit);
            laserDot.Object.transform.position = end;
            laserDot.Object.transform.localScale = Vector3.one * (0.03f * Scale);
        }

        public static void DisableLaserPointer()
        {
            DestroyLine(ref laser);
            Discard(laserDot);
            laserDot = null;
        }

        private static Light flashlight;

        /// <summary>A torch in your right hand.</summary>
        public static void HandFlashlight()
        {
            if (flashlight == null)
            {
                flashlight = new GameObject("Nova_Flashlight").AddComponent<Light>();
                flashlight.type = LightType.Spot;
                flashlight.spotAngle = 45f;
                flashlight.intensity = 2.5f;
            }

            var hand = ControllerUtilities.GetTrueRightHand();
            flashlight.range = 20f * Scale;
            flashlight.transform.SetPositionAndRotation(hand.position, Quaternion.LookRotation(hand.forward));
        }

        public static void DisableHandFlashlight()
        {
            if (flashlight != null)
                Object.Destroy(flashlight.gameObject);
            flashlight = null;
        }

        private static bool wandHeld;

        /// <summary>Pull your right trigger to fire a sparkle bolt that bursts where it hits.</summary>
        public static void MagicWand()
        {
            if (!Pressed(rightTrigger > 0.5f, ref wandHeld))
                return;

            var hand = ControllerUtilities.GetTrueRightHand();
            Vector3 target = Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, 40f * Scale, NoInvisLayerMask())
                ? hit.point
                : hand.position + hand.forward * (15f * Scale);

            CoroutineManager.instance.StartCoroutine(FlyBolt(hand.position, target));
        }

        private static IEnumerator FlyBolt(Vector3 from, Vector3 to)
        {
            Piece bolt = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one * (0.05f * Scale));
            float duration = Mathf.Clamp(Vector3.Distance(from, to) / 30f, 0.05f, 0.6f);
            float start = Time.time;

            while (Time.time - start < duration)
            {
                if (!bolt.Alive)
                    yield break;

                bolt.Object.transform.position = Vector3.Lerp(from, to, (Time.time - start) / duration);
                SetColor(bolt, Color.HSVToRGB(Time.time * 3f % 1f, 0.6f, 1f));
                yield return null;
            }

            Discard(bolt);
            Burst(to, 60, 3f, falling: false);
        }

        // ── Cute and silly ──────────────────────────────────────────────────────

        private const int MaxBubbles = 30;
        private static readonly List<Piece> bubbles = new List<Piece>();
        private static float nextBubble;

        /// <summary>Hold A to blow bubbles from your right hand.</summary>
        public static void BubbleBlower()
        {
            float now = Time.time;
            float scale = Scale;

            if (rightPrimary && now >= nextBubble)
            {
                nextBubble = now + 0.1f;
                var hand = ControllerUtilities.GetTrueRightHand();

                // Once there are enough, the oldest bubble is reused for the newest.
                Piece bubble = null;
                if (bubbles.Count >= MaxBubbles)
                {
                    bubble = bubbles[0];
                    bubbles.RemoveAt(0);
                }

                if (bubble == null || !bubble.Alive)
                    bubble = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one);

                bubble.Born = now;
                bubble.Life = Random.Range(3f, 5f);
                bubble.Size = Random.Range(0.05f, 0.12f) * scale;
                bubble.Seed = Random.value * 100f;
                bubble.Velocity = hand.forward * 0.8f + Vector3.up * 0.2f;
                bubble.Object.transform.position = hand.position + hand.forward * (0.08f * scale);
                bubble.Object.SetActive(true);
                bubbles.Add(bubble);
            }

            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                Piece bubble = bubbles[i];
                if (!bubble.Alive)
                {
                    bubbles.RemoveAt(i);
                    continue;
                }

                if (now - bubble.Born >= bubble.Life)
                {
                    Burst(bubble.Object.transform.position, 6, 0.6f, falling: false);
                    Discard(bubble);
                    bubbles.RemoveAt(i);
                    continue;
                }

                // Slows down, rises, and wobbles as it drifts, each on its own rhythm.
                float seed = bubble.Seed;
                bubble.Velocity = Vector3.Lerp(bubble.Velocity, Vector3.up * 0.25f, Time.deltaTime * 1.5f);
                Vector3 wobble = new Vector3(Mathf.Sin(now * 3f + seed), 0f, Mathf.Cos(now * 2.5f + seed)) * 0.05f;
                bubble.Object.transform.position += (bubble.Velocity + wobble) * (Time.deltaTime * scale);
                bubble.Object.transform.localScale = Vector3.one * bubble.Size;
                SetAlpha(bubble, Color.HSVToRGB((now * 0.2f + seed * 0.01f) % 1f, 0.25f, 1f), 0.35f);
            }
        }

        public static void DisableBubbleBlower() => ClearPieces(bubbles);

        private static readonly List<Piece> butterflies = new List<Piece>();

        private static Piece MakeButterfly()
        {
            Piece butterfly = MakePiece(PrimitiveType.Cube, Color.white, Vector3.one);
            butterfly.Object.transform.position = Head.position + Random.insideUnitSphere * (0.5f * Scale);
            return butterfly;
        }

        /// <summary>A few butterflies fluttering around you.</summary>
        public static void Butterflies()
        {
            while (butterflies.Count < 4)
                butterflies.Add(MakeButterfly());

            float now = Time.time;
            float scale = Scale;

            for (int i = 0; i < butterflies.Count; i++)
            {
                if (!butterflies[i].Alive)
                    butterflies[i] = MakeButterfly();

                Piece butterfly = butterflies[i];
                float seed = butterfly.Seed;
                Vector3 target = Head.position + new Vector3(
                    (Mathf.PerlinNoise(seed, now * 0.3f) - 0.5f) * 2.4f,
                    (Mathf.PerlinNoise(seed + 10f, now * 0.3f) - 0.3f) * 1.2f,
                    (Mathf.PerlinNoise(seed + 20f, now * 0.3f) - 0.5f) * 2.4f) * scale;

                Transform body = butterfly.Object.transform;
                Vector3 step = Vector3.Lerp(body.position, target, Time.deltaTime * 1.5f) - body.position;
                body.position += step;

                // Wings beat by squashing the body flat and open again.
                float flap = Mathf.Abs(Mathf.Sin(now * 14f + seed));
                body.localScale = new Vector3(0.1f * (0.25f + flap), 0.004f, 0.06f) * scale;
                if (step.sqrMagnitude > 0.0000001f)
                    body.rotation = Quaternion.LookRotation(step.normalized);

                SetAlpha(butterfly, Color.HSVToRGB((seed * 0.01f + now * 0.02f) % 1f, 0.5f, 1f), 0.9f);
            }
        }

        public static void DisableButterflies() => ClearPieces(butterflies);

        private static readonly Piece[] cloudPuffs = new Piece[4];
        private static ParticleSystem rain;

        /// <summary>A little rain cloud that follows you around.</summary>
        public static void RainCloud()
        {
            float scale = Scale;
            Vector3 centre = Head.position + Vector3.up * (0.55f * scale);

            for (int i = 0; i < cloudPuffs.Length; i++)
            {
                Ensure(ref cloudPuffs[i], PrimitiveType.Sphere, new Color(0.55f, 0.58f, 0.62f, 0.9f));

                float offset = (i - 1.5f) * 0.12f;
                cloudPuffs[i].Object.transform.position = centre + new Vector3(offset, Mathf.Sin(Time.time + i) * 0.02f + (i % 2) * 0.05f, (i % 2 - 0.5f) * 0.06f) * scale;
                cloudPuffs[i].Object.transform.localScale = Vector3.one * ((0.16f + (i % 2) * 0.05f) * scale);
            }

            if (rain == null)
                rain = MakeParticles("Nova_Rain", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startLifetime = 0.7f;
                    main.startSpeed = 0f;
                    main.startSize = 0.012f;
                    main.startColor = new Color(0.6f, 0.75f, 1f, 0.9f);
                    main.gravityModifier = 0.8f * Scale;
                    main.maxParticles = 150;

                    var emission = system.emission;
                    emission.rateOverTime = 70f;

                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(0.4f, 0.02f, 0.2f);
                });

            Follow(rain, centre - Vector3.up * (0.08f * scale));
        }

        public static void DisableRainCloud()
        {
            for (int i = 0; i < cloudPuffs.Length; i++)
            {
                Discard(cloudPuffs[i]);
                cloudPuffs[i] = null;
            }

            DestroyParticles(ref rain);
        }

        private static ParticleSystem fireflies;

        /// <summary>Glowing fireflies drifting around you.</summary>
        public static void FireflySwarm()
        {
            if (fireflies == null)
                fireflies = MakeParticles("Nova_Fireflies", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                    main.startSpeed = 0.1f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.3f), new Color(1f, 0.9f, 0.4f));
                    main.maxParticles = 40;

                    var emission = system.emission;
                    emission.rateOverTime = 8f;

                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 1.5f;

                    var noise = system.noise;
                    noise.enabled = true;
                    noise.strength = 0.4f;
                    noise.frequency = 0.4f;
                });

            Follow(fireflies, Extras.BodyPosition);
        }

        public static void DisableFireflySwarm() => DestroyParticles(ref fireflies);

        private const int AfterimageCount = 12;
        private static readonly List<Piece> afterimages = new List<Piece>();
        private static int nextAfterimage;
        private static float afterimageTimer;

        /// <summary>Fading copies of your head trail behind you when you move fast.</summary>
        public static void Afterimages()
        {
            float now = Time.time;

            if (afterimages.Count == 0)
                for (int i = 0; i < AfterimageCount; i++)
                {
                    Piece copy = MakePiece(PrimitiveType.Sphere, MyColor, Vector3.one);
                    copy.Born = -10f;
                    copy.Object.SetActive(false);
                    afterimages.Add(copy);
                }

            if (now >= afterimageTimer && Extras.Body.linearVelocity.sqrMagnitude > 9f * Scale * Scale)
            {
                afterimageTimer = now + 0.07f;
                if (!afterimages[nextAfterimage].Alive)
                    afterimages[nextAfterimage] = MakePiece(PrimitiveType.Sphere, MyColor, Vector3.one);

                Piece copy = afterimages[nextAfterimage];
                nextAfterimage = (nextAfterimage + 1) % afterimages.Count;

                copy.Born = now;
                copy.Object.SetActive(true);
                copy.Object.transform.position = Head.position;
                copy.Object.transform.localScale = Vector3.one * (0.3f * Scale);
            }

            Color color = MyColor;
            foreach (Piece copy in afterimages)
            {
                if (!copy.Alive || !copy.Object.activeSelf)
                    continue;

                float fade = 1f - (now - copy.Born) / 0.5f;
                if (fade <= 0f)
                    copy.Object.SetActive(false);
                else
                    SetAlpha(copy, color, 0.4f * fade);
            }
        }

        public static void DisableAfterimages() => ClearPieces(afterimages);

        private static ParticleSystem speedLines;
        private static float speedLineBudget;

        /// <summary>Streaks rush past you when you go fast.</summary>
        public static void SpeedLines()
        {
            if (speedLines == null)
            {
                speedLines = MakeParticles("Nova_SpeedLines", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startLifetime = 0.25f;
                    main.startSpeed = 0f;
                    main.startSize = 0.01f;
                    main.startColor = new Color(1f, 1f, 1f, 0.6f);
                    main.maxParticles = 200;
                    main.scalingMode = ParticleSystemScalingMode.Local;

                    var emission = system.emission;
                    emission.enabled = false;
                });

                speedLines.transform.localScale = Vector3.one;
                ParticleSystemRenderer streaks = speedLines.GetComponent<ParticleSystemRenderer>();
                streaks.renderMode = ParticleSystemRenderMode.Stretch;
                streaks.velocityScale = 0.04f;
                streaks.lengthScale = 1f;
            }

            float scale = Scale;
            Vector3 velocity = Extras.Body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 6f * scale)
            {
                speedLineBudget = 0f;
                return;
            }

            // Streaks per second, not per frame, so it looks the same at any frame rate.
            speedLineBudget += Mathf.Min(300f, 30f + (speed / scale - 6f) * 25f) * Time.deltaTime;
            Vector3 ahead = velocity / speed;
            ParticleSystem.EmitParams streak = new ParticleSystem.EmitParams { startSize = 0.01f * scale };

            while (speedLineBudget >= 1f)
            {
                speedLineBudget -= 1f;
                streak.position = Head.position + ahead * (2f * scale) + Random.insideUnitSphere * (1.2f * scale);
                streak.velocity = -ahead * (speed * 1.5f);
                speedLines.Emit(streak, 1);
            }
        }

        public static void DisableSpeedLines() => DestroyParticles(ref speedLines);

        private const int RoadTiles = 30;
        private static readonly List<Piece> road = new List<Piece>();
        private static int nextTile;
        private static float tileTimer;

        /// <summary>Rainbow tiles appear under you while you fly through the air.</summary>
        public static void RainbowRoad()
        {
            float now = Time.time;

            if (road.Count == 0)
                for (int i = 0; i < RoadTiles; i++)
                {
                    Piece tile = MakePiece(PrimitiveType.Cube, Color.white, Vector3.one);
                    tile.Born = -10f;
                    tile.Object.SetActive(false);
                    road.Add(tile);
                }

            Vector3 velocity = Extras.Body.linearVelocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);

            if (now >= tileTimer && !Extras.Grounded() && flat.sqrMagnitude > 4f * Scale * Scale)
            {
                tileTimer = now + 0.1f;
                if (!road[nextTile].Alive)
                    road[nextTile] = MakePiece(PrimitiveType.Cube, Color.white, Vector3.one);

                Piece tile = road[nextTile];
                nextTile = (nextTile + 1) % road.Count;

                tile.Born = now;
                tile.Size = now * 0.5f % 1f;
                tile.Object.SetActive(true);
                tile.Object.transform.SetPositionAndRotation(
                    Extras.BodyPosition + Vector3.down * (0.6f * Scale),
                    Quaternion.LookRotation(flat.normalized));
                tile.Object.transform.localScale = new Vector3(0.6f, 0.02f, 0.45f) * Scale;
            }

            foreach (Piece tile in road)
            {
                if (!tile.Alive || !tile.Object.activeSelf)
                    continue;

                float fade = 1f - (now - tile.Born) / 3f;
                if (fade <= 0f)
                    tile.Object.SetActive(false);
                else
                    SetAlpha(tile, Color.HSVToRGB(tile.Size, 0.8f, 1f), 0.7f * fade);
            }
        }

        public static void DisableRainbowRoad() => ClearPieces(road);

        // ── Balls ───────────────────────────────────────────────────────────────

        private const int MaxBalls = 8;
        private static readonly List<Piece> balls = new List<Piece>();
        private static bool ballHeld;
        private static Vector3 lastLeftHand, lastRightHand;
        private static int ballsFrame = -1;

        // Bouncy Ball and Basketball Hoop share the balls; each says it is on every frame, so
        // turning one off only clears the balls when the other is not still using them.
        private static bool bouncyBallOn, hoopOn;

        /// <summary>Press B to throw a bouncy ball; hit it with your hands to keep it going.</summary>
        public static void BouncyBall()
        {
            bouncyBallOn = true;
            UpdateBalls();
        }

        public static void DisableBouncyBall()
        {
            bouncyBallOn = false;
            if (!hoopOn)
                ClearPieces(balls);
        }

        /// <summary>Throws, moves and bounces the balls, once a frame for every mod that uses them.</summary>
        /// <remarks>
        /// The balls are moved here rather than by the physics engine, so they can never
        /// push you or anything else around; they only see the ground and walls.
        /// </remarks>
        private static void UpdateBalls()
        {
            if (ballsFrame == Time.frameCount)
                return;
            ballsFrame = Time.frameCount;

            float dt = Time.deltaTime;
            float scale = Scale;
            Vector3 leftHand = GorillaTagger.Instance.leftHandTransform.position;
            Vector3 rightHand = GorillaTagger.Instance.rightHandTransform.position;
            Vector3 leftHandVelocity = dt > 0f ? (leftHand - lastLeftHand) / dt : Vector3.zero;
            Vector3 rightHandVelocity = dt > 0f ? (rightHand - lastRightHand) / dt : Vector3.zero;
            lastLeftHand = leftHand;
            lastRightHand = rightHand;

            if (Pressed(rightSecondary, ref ballHeld))
            {
                var hand = ControllerUtilities.GetTrueRightHand();

                Piece ball = null;
                if (balls.Count >= MaxBalls)
                {
                    ball = balls[0];
                    balls.RemoveAt(0);
                }

                if (ball == null || !ball.Alive)
                    ball = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one);

                ball.Born = Time.time;
                ball.Size = 0.09f * scale;
                ball.Velocity = hand.forward * (7f * scale) + Extras.Body.linearVelocity;
                ball.Object.transform.position = hand.position + hand.forward * (0.15f * scale);
                ball.Object.transform.localScale = Vector3.one * (ball.Size * 2f);
                SetColor(ball, Color.HSVToRGB(Random.value, 0.8f, 1f));
                balls.Add(ball);
            }

            int layers = GTPlayer.Instance.locomotionEnabledLayers;
            float skin = 0.01f * scale;

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Piece ball = balls[i];
                if (!ball.Alive || Time.time - ball.Born > 30f)
                {
                    Discard(ball);
                    balls.RemoveAt(i);
                    continue;
                }

                Transform body = ball.Object.transform;
                ball.Anchor = body.position;
                ball.Velocity += Physics.gravity * (dt * scale);

                // A hand that touches the ball passes on its own speed, like a volleyball.
                // A just-thrown ball is still beside the throwing hand, so it is left alone
                // for a moment rather than having the throw overwritten.
                if (Time.time - ball.Born > 0.25f)
                {
                    float touch = ball.Size + 0.08f * scale;
                    if ((body.position - leftHand).sqrMagnitude < touch * touch && leftHandVelocity.sqrMagnitude > 1f)
                        ball.Velocity = leftHandVelocity * 1.2f + Vector3.up * (1.5f * scale);
                    if ((body.position - rightHand).sqrMagnitude < touch * touch && rightHandVelocity.sqrMagnitude > 1f)
                        ball.Velocity = rightHandVelocity * 1.2f + Vector3.up * (1.5f * scale);
                }

                Vector3 move = ball.Velocity * dt;
                float distance = move.magnitude;

                if (distance > 0f && Physics.SphereCast(body.position, ball.Size, move / distance, out RaycastHit hit, distance + skin, layers))
                {
                    // Stopping a hair short of the surface keeps the next cast outside it,
                    // so a resting ball can't sink into the floor.
                    body.position += move / distance * Mathf.Max(0f, hit.distance - skin) + hit.normal * skin;
                    ball.Velocity = Vector3.Reflect(ball.Velocity, hit.normal) * 0.8f;
                    if (ball.Velocity.sqrMagnitude < 0.25f * scale * scale && hit.normal.y > 0.7f)
                        ball.Velocity = Vector3.zero;
                }
                else
                    body.position += move;
            }
        }

        // ── Mini-games ──────────────────────────────────────────────────────────

        /// <summary>A spot on the ground near you that you can reach.</summary>
        /// <remarks>
        /// The ray starts just above your head, not far above you, so indoors it finds the
        /// floor instead of the roof.
        /// </remarks>
        private static Vector3 RandomSpotNearby(float radius)
        {
            float scale = Scale;
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 from = new Vector3(Extras.BodyPosition.x + offset.x * scale, Head.position.y + 0.3f * scale, Extras.BodyPosition.z + offset.y * scale);

            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 6f * scale, GTPlayer.Instance.locomotionEnabledLayers)
                ? hit.point + Vector3.up * (0.5f * scale)
                : Extras.BodyPosition + new Vector3(offset.x, 0.3f, offset.y) * scale;
        }

        /// <summary>Whether something placed for a game has been left too far behind to reach.</summary>
        private static bool TooFar(Vector3 at, float range) =>
            (at - Extras.BodyPosition).sqrMagnitude > range * range * Scale * Scale;

        private static readonly List<Piece> coins = new List<Piece>();
        private static TextMeshPro coinText;
        private static int coinScore, coinBest;

        /// <summary>Coins appear around you; touch them with your hands or head to collect them.</summary>
        public static void CoinHunt()
        {
            while (coins.Count < 10)
            {
                Piece coin = MakePiece(PrimitiveType.Cylinder, new Color(1f, 0.8f, 0.1f, 1f), Vector3.one);
                coin.Object.transform.position = RandomSpotNearby(8f);
                coins.Add(coin);
            }

            if (coinText == null)
            {
                coinText = MakeWristText("Nova_CoinHunt", new Color(1f, 0.85f, 0.2f));
                coinScore = 0;
                coinText.SafeSetText($"Coins: 0\n<size=70%>Best: {coinBest}</size>");
            }

            float scale = Scale;
            float reach = 0.35f * scale;
            Vector3 head = Head.position;
            Vector3 leftHand = GorillaTagger.Instance.leftHandTransform.position;
            Vector3 rightHand = GorillaTagger.Instance.rightHandTransform.position;
            Quaternion spin = Quaternion.Euler(90f, Time.time * 180f, 0f);

            for (int i = 0; i < coins.Count; i++)
            {
                if (!coins[i].Alive)
                {
                    coins[i] = MakePiece(PrimitiveType.Cylinder, new Color(1f, 0.8f, 0.1f, 1f), Vector3.one);
                    coins[i].Object.transform.position = RandomSpotNearby(8f);
                }

                Transform body = coins[i].Object.transform;
                body.rotation = spin;
                body.localScale = new Vector3(0.18f, 0.015f, 0.18f) * scale;

                // Coins left behind when you move on come with you.
                if (TooFar(body.position, 15f))
                {
                    body.position = RandomSpotNearby(8f);
                    continue;
                }

                Vector3 at = body.position;
                if ((at - head).sqrMagnitude < reach * reach || (at - leftHand).sqrMagnitude < reach * reach || (at - rightHand).sqrMagnitude < reach * reach)
                {
                    Burst(at, 20, 2f, falling: true);
                    body.position = RandomSpotNearby(8f);
                    coinScore++;
                    coinBest = Mathf.Max(coinBest, coinScore);
                    coinText.SafeSetText($"Coins: {coinScore}\n<size=70%>Best: {coinBest}</size>");
                }
            }

            PlaceOnWrist(coinText, 0.12f, rightWrist: true);
        }

        public static void DisableCoinHunt()
        {
            ClearPieces(coins);
            DestroyText(ref coinText);
        }

        private static readonly List<Piece> targets = new List<Piece>();
        private static TextMeshPro targetText;
        private static LineRenderer aimLine;
        private static int targetScore, targetBest;
        private static bool targetHeld;

        /// <summary>Targets float around you; aim with your right hand and pull the trigger to hit them.</summary>
        public static void TargetPractice()
        {
            float scale = Scale;

            while (targets.Count < 5)
            {
                Piece target = MakePiece(PrimitiveType.Cylinder, Color.red, Vector3.one);
                PlaceTarget(target);
                targets.Add(target);
            }

            if (targetText == null)
            {
                targetText = MakeWristText("Nova_TargetPractice", new Color(1f, 0.4f, 0.4f));
                targetScore = 0;
                targetText.SafeSetText($"Targets: 0\n<size=70%>Best: {targetBest}</size>");
            }

            if (aimLine == null)
            {
                aimLine = MakeLine("Nova_AimLine", 0.004f);
                aimLine.startColor = new Color(1f, 1f, 1f, 0.6f);
                aimLine.endColor = new Color(1f, 1f, 1f, 0f);
            }

            var hand = ControllerUtilities.GetTrueRightHand();
            aimLine.startWidth = aimLine.endWidth = 0.004f * scale;
            aimLine.SetPosition(0, hand.position);
            aimLine.SetPosition(1, hand.position + hand.forward * (12f * scale));

            // Targets turn to face you, bob gently, and come along if you move away.
            for (int i = 0; i < targets.Count; i++)
            {
                if (!targets[i].Alive)
                {
                    targets[i] = MakePiece(PrimitiveType.Cylinder, Color.red, Vector3.one);
                    PlaceTarget(targets[i]);
                }

                Piece target = targets[i];
                Transform body = target.Object.transform;

                if (TooFar(body.position, 14f))
                    PlaceTarget(target);

                Vector3 toMe = Head.position - body.position;
                if (toMe.sqrMagnitude > 0.0001f)
                    body.rotation = Quaternion.LookRotation(toMe) * Quaternion.Euler(90f, 0f, 0f);
                body.position += Vector3.up * (Mathf.Sin(Time.time * 2f + target.Seed) * 0.1f * scale * Time.deltaTime);
                body.localScale = new Vector3(target.Size * 2f, 0.01f * scale, target.Size * 2f);
            }

            if (Pressed(rightTrigger > 0.5f, ref targetHeld))
            {
                // A shot hits the nearest target the aim line passes through.
                Piece struck = null;
                float nearest = float.MaxValue;

                foreach (Piece target in targets)
                {
                    Vector3 toTarget = target.Object.transform.position - hand.position;
                    float along = Vector3.Dot(toTarget, hand.forward);
                    if (along <= 0f || along >= nearest)
                        continue;

                    if ((toTarget - hand.forward * along).sqrMagnitude <= target.Size * target.Size)
                    {
                        struck = target;
                        nearest = along;
                    }
                }

                if (struck != null)
                {
                    Burst(struck.Object.transform.position, 40, 3f, falling: true);
                    PlaceTarget(struck);
                    targetScore++;
                    targetBest = Mathf.Max(targetBest, targetScore);
                    targetText.SafeSetText($"Targets: {targetScore}\n<size=70%>Best: {targetBest}</size>");
                }
            }

            PlaceOnWrist(targetText, 0.2f, rightWrist: true);
        }

        private static void PlaceTarget(Piece target)
        {
            float scale = Scale;
            Vector2 around = Random.insideUnitCircle.normalized * Random.Range(4f, 10f);
            target.Size = 0.25f * scale;
            target.Object.transform.position = Head.position + new Vector3(around.x, Random.Range(-0.5f, 2f), around.y) * scale;
            SetColor(target, Random.value < 0.5f ? new Color(1f, 0.15f, 0.15f) : new Color(1f, 0.45f, 0.1f));
        }

        public static void DisableTargetPractice()
        {
            ClearPieces(targets);
            DestroyText(ref targetText);
            DestroyLine(ref aimLine);
        }

        // ── Utility ─────────────────────────────────────────────────────────────

        /// <summary>Turns off every Custom mod that is on, and clears your air paintings.</summary>
        public static void TurnOffAll()
        {
            foreach (ButtonInfo button in Buttons.buttons[Buttons.GetCategory(Category)])
                if (button.enabled && button.isTogglable)
                    Main.Toggle(button);

            ClearAirPaint();
            NotificationManager.SendNotification("Every Custom mod is off.", 3000);
        }
    }
}
