/*
 * Nova Menu  Mods/CustomMore.cs
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
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static Nova.Menu.Main;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Nova.Mods
{
    /// <summary>The third set of Custom Mods: more games, a pet, hats, scenery, music and tools.</summary>
    public static partial class Custom
    {
        /// <summary>A spot on the ground just in front of you.</summary>
        private static Vector3 GroundInFront(float distance)
        {
            float scale = Scale;
            Vector3 from = Head.position + LookFlat() * (distance * scale);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 4f * scale, GTPlayer.Instance.locomotionEnabledLayers)
                ? hit.point
                : Extras.BodyPosition + LookFlat() * (distance * scale) + Vector3.down * (0.6f * scale);
        }

        // ── Mini-games ──────────────────────────────────────────────────────────

        private const int HoopBeads = 12;
        private static readonly Piece[] hoopRing = new Piece[HoopBeads];
        private static Piece backboard;
        private static Vector3 hoopCentre, hoopFacing;
        private static bool hoopPlaced;
        private static float hoopLayoutScale;
        private static TextMeshPro hoopText;
        private static int hoopScore, hoopBest;

        /// <summary>A basketball hoop in front of you; throw bouncy balls (B) through it to score.</summary>
        public static void BasketballHoop()
        {
            float scale = Scale;
            hoopOn = true;

            // The hoop stands still, so it is only laid out when placed, rebuilt or resized.
            bool layout = !hoopPlaced || TooFar(hoopCentre, 30f) || !Mathf.Approximately(hoopLayoutScale, scale);
            if (!hoopPlaced || TooFar(hoopCentre, 30f))
            {
                hoopFacing = LookFlat();
                hoopCentre = Head.position + hoopFacing * (3f * scale) + Vector3.up * (0.4f * scale);
                hoopPlaced = true;
            }

            float radius = 0.25f * scale;
            for (int i = 0; i < HoopBeads; i++)
                if (hoopRing[i] == null || !hoopRing[i].Alive)
                {
                    Ensure(ref hoopRing[i], PrimitiveType.Sphere, new Color(1f, 0.45f, 0.1f));
                    layout = true;
                }

            if (backboard == null || !backboard.Alive)
            {
                Ensure(ref backboard, PrimitiveType.Cube, new Color(1f, 1f, 1f, 0.6f));
                layout = true;
            }

            if (layout)
            {
                hoopLayoutScale = scale;
                for (int i = 0; i < HoopBeads; i++)
                {
                    float angle = i * Mathf.PI * 2f / HoopBeads;
                    hoopRing[i].Object.transform.position = hoopCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    hoopRing[i].Object.transform.localScale = Vector3.one * (0.04f * scale);
                }

                backboard.Object.transform.SetPositionAndRotation(hoopCentre + hoopFacing * (radius + 0.05f * scale) + Vector3.up * (0.25f * scale), Quaternion.LookRotation(hoopFacing));
                backboard.Object.transform.localScale = new Vector3(0.9f, 0.6f, 0.03f) * scale;
            }

            if (hoopText == null)
            {
                hoopText = MakeWristText("Nova_Basketball", new Color(1f, 0.6f, 0.2f));
                hoopScore = 0;
                hoopText.SafeSetText($"Hoops: 0\n<size=70%>Best: {hoopBest}</size>");
            }

            // The hoop brings its own balls, so it works whether or not Bouncy Ball is on.
            UpdateBalls();

            foreach (Piece ball in balls)
            {
                if (!ball.Alive)
                    continue;

                // Where the ball really was before this frame's move, not worked back from a
                // velocity a bounce or a hand may already have changed.
                Vector3 now = ball.Object.transform.position;
                Vector3 before = ball.Anchor;
                Vector3 flat = now - hoopCentre;
                flat.y = 0f;

                // A basket is a ball dropping down through the ring.
                if (before.y > hoopCentre.y && now.y <= hoopCentre.y && flat.magnitude < radius - ball.Size * 0.5f)
                {
                    Burst(hoopCentre, 50, 3f, falling: true);
                    hoopScore++;
                    hoopBest = Mathf.Max(hoopBest, hoopScore);
                    hoopText.SafeSetText($"Hoops: {hoopScore}\n<size=70%>Best: {hoopBest}</size>");
                }
            }

            PlaceOnWrist(hoopText, 0.28f, rightWrist: true);
        }

        public static void DisableBasketballHoop()
        {
            for (int i = 0; i < hoopRing.Length; i++)
            {
                Discard(hoopRing[i]);
                hoopRing[i] = null;
            }

            Discard(backboard);
            backboard = null;
            hoopPlaced = false;
            DestroyText(ref hoopText);

            hoopOn = false;
            if (!bouncyBallOn)
                ClearPieces(balls);
        }

        private enum Reaction { Waiting, Go, Shown }
        private static Reaction reactionState = Reaction.Shown;
        private static float reactionAt, reactionBest = float.MaxValue;
        private static bool reactionHeld;
        private static TextMeshPro reactionText;

        /// <summary>Wait for green, then press A as fast as you can.</summary>
        public static void ReactionTest()
        {
            float now = Time.realtimeSinceStartup;

            if (reactionText == null)
            {
                reactionText = MakeWristText("Nova_ReactionTest", Color.white);
                reactionText.SafeSetText("Get ready...");
                reactionState = Reaction.Shown;
                reactionAt = now;
            }

            bool pressed = Pressed(rightPrimary, ref reactionHeld);

            switch (reactionState)
            {
                case Reaction.Shown when now - reactionAt > 1.5f:
                    reactionState = Reaction.Waiting;
                    reactionAt = now + Random.Range(1.5f, 4f);
                    reactionText.SafeSetText("<color=red>Wait...</color>");
                    break;

                case Reaction.Waiting when pressed:
                    reactionState = Reaction.Shown;
                    reactionAt = now;
                    reactionText.SafeSetText("<color=orange>Too soon!</color>");
                    break;

                case Reaction.Waiting when now >= reactionAt:
                    reactionState = Reaction.Go;
                    reactionAt = now;
                    reactionText.SafeSetText("<color=green>NOW!</color>");
                    break;

                case Reaction.Go when pressed:
                    float taken = now - reactionAt;
                    reactionBest = Mathf.Min(reactionBest, taken);
                    reactionState = Reaction.Shown;
                    reactionAt = now;
                    reactionText.SafeSetText($"{taken * 1000f:0} ms\n<size=70%>Best: {reactionBest * 1000f:0} ms</size>");
                    break;
            }

            PlaceOnWrist(reactionText, 0.36f, rightWrist: true);
        }

        public static void DisableReactionTest() => DestroyText(ref reactionText);

        private const int MoleHoles = 6;
        private static readonly Piece[] holes = new Piece[MoleHoles];
        private static Piece mole;
        private static Vector3 molesCentre;
        private static bool molesPlaced;
        private static float molesLayoutScale;
        private static int moleHole = -1;
        private static float moleUntil, moleNext;
        private static TextMeshPro moleText;
        private static int moleScore, moleBest;

        /// <summary>Moles pop out of holes around you; hit them with your hands before they hide.</summary>
        public static void WhackAMole()
        {
            float scale = Scale;
            float now = Time.time;

            bool layout = !Mathf.Approximately(molesLayoutScale, scale);
            if (!molesPlaced || TooFar(molesCentre, 8f))
            {
                molesCentre = GroundInFront(0f);
                molesPlaced = true;
                moleHole = -1;
                layout = true;
            }

            for (int i = 0; i < MoleHoles; i++)
                if (holes[i] == null || !holes[i].Alive)
                {
                    Ensure(ref holes[i], PrimitiveType.Cylinder, new Color(0.15f, 0.1f, 0.05f, 0.95f));
                    layout = true;
                }

            // The holes stay put, so they are only laid out when placed, rebuilt or resized.
            if (layout)
            {
                molesLayoutScale = scale;
                float ring = 1.2f * scale;
                for (int i = 0; i < MoleHoles; i++)
                {
                    float angle = i * Mathf.PI * 2f / MoleHoles;
                    holes[i].Object.transform.position = molesCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring + Vector3.up * (0.01f * scale);
                    holes[i].Object.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f) * scale;
                }
            }

            Ensure(ref mole, PrimitiveType.Sphere, new Color(0.5f, 0.32f, 0.18f));

            if (moleText == null)
            {
                moleText = MakeWristText("Nova_WhackAMole", new Color(0.9f, 0.7f, 0.4f));
                moleScore = 0;
                moleText.SafeSetText($"Moles: 0\n<size=70%>Best: {moleBest}</size>");
            }

            if (moleHole < 0 && now >= moleNext)
            {
                moleHole = Random.Range(0, MoleHoles);
                moleUntil = now + 1.2f;
            }

            mole.Object.SetActive(moleHole >= 0);
            if (moleHole >= 0)
            {
                // Pops up, waits, and ducks back down.
                float left = moleUntil - now;
                float rise = Mathf.Clamp01(Mathf.Min(1.2f - left, left) * 6f);
                Vector3 top = holes[moleHole].Object.transform.position + Vector3.up * (rise * 0.18f * scale);
                mole.Object.transform.position = top;
                mole.Object.transform.localScale = new Vector3(0.2f, 0.24f, 0.2f) * scale;

                float reach = 0.2f * scale;
                bool hit = rise > 0.5f && ((GorillaTagger.Instance.leftHandTransform.position - top).sqrMagnitude < reach * reach
                    || (GorillaTagger.Instance.rightHandTransform.position - top).sqrMagnitude < reach * reach);

                if (hit)
                {
                    Burst(top, 30, 2f, falling: true);
                    moleScore++;
                    moleBest = Mathf.Max(moleBest, moleScore);
                    moleText.SafeSetText($"Moles: {moleScore}\n<size=70%>Best: {moleBest}</size>");
                }

                if (hit || left <= 0f)
                {
                    moleHole = -1;
                    moleNext = now + Random.Range(0.3f, 0.9f);
                }
            }

            PlaceOnWrist(moleText, 0.28f);
        }

        public static void DisableWhackAMole()
        {
            for (int i = 0; i < holes.Length; i++)
            {
                Discard(holes[i]);
                holes[i] = null;
            }

            Discard(mole);
            mole = null;
            molesPlaced = false;
            DestroyText(ref moleText);
        }

        // ── Pet and hats ────────────────────────────────────────────────────────

        private static Piece duckBody, duckHead, duckBeak;
        private static Vector3 duckPosition, duckFacing = Vector3.forward;
        private static bool duckPlaced;

        /// <summary>A little duck that waddles after you.</summary>
        public static void PetDuck()
        {
            float scale = Scale;
            Ensure(ref duckBody, PrimitiveType.Sphere, new Color(1f, 0.9f, 0.2f));
            Ensure(ref duckHead, PrimitiveType.Sphere, new Color(1f, 0.9f, 0.2f));
            Ensure(ref duckBeak, PrimitiveType.Cube, new Color(1f, 0.5f, 0.1f));

            Vector3 behind = Extras.BodyPosition - LookFlat() * (0.9f * scale);
            if (!duckPlaced || TooFar(duckPosition, 10f))
            {
                duckPosition = behind;
                duckPlaced = true;
            }

            // Walks towards a spot behind you, and only turns while it is walking, so it
            // doesn't spin on the spot as you look around.
            Vector3 toSpot = behind - duckPosition;
            toSpot.y = 0f;
            bool walking = toSpot.sqrMagnitude > 0.04f * scale * scale;
            if (walking)
            {
                duckPosition += toSpot.normalized * Mathf.Min(toSpot.magnitude, 3f * scale * Time.deltaTime);
                duckFacing = toSpot.normalized;
            }

            // The ground is looked for from your head height down, so it climbs ledges and
            // follows you down drops instead of floating.
            Vector3 probe = new Vector3(duckPosition.x, Head.position.y + 0.3f * scale, duckPosition.z);
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 6f * scale, GTPlayer.Instance.locomotionEnabledLayers))
                duckPosition.y = ground.point.y;

            float waddle = walking ? Mathf.Sin(Time.time * 12f) * 12f : 0f;
            Quaternion turn = Quaternion.LookRotation(duckFacing) * Quaternion.Euler(0f, 0f, waddle);

            duckBody.Object.transform.SetPositionAndRotation(duckPosition + Vector3.up * (0.08f * scale), turn);
            duckBody.Object.transform.localScale = new Vector3(0.16f, 0.13f, 0.2f) * scale;
            duckHead.Object.transform.position = duckPosition + turn * new Vector3(0f, 0.19f, 0.08f) * scale;
            duckHead.Object.transform.localScale = Vector3.one * (0.1f * scale);
            duckBeak.Object.transform.SetPositionAndRotation(duckPosition + turn * new Vector3(0f, 0.18f, 0.145f) * scale, turn);
            duckBeak.Object.transform.localScale = new Vector3(0.05f, 0.02f, 0.05f) * scale;
        }

        public static void DisablePetDuck()
        {
            Discard(duckBody);
            Discard(duckHead);
            Discard(duckBeak);
            duckBody = duckHead = duckBeak = null;
            duckPlaced = false;
        }

        private static Piece hatBrim, hatTop;

        /// <summary>A top hat on your head, for mirrors and cameras.</summary>
        public static void TopHat()
        {
            float scale = Scale;
            Ensure(ref hatBrim, PrimitiveType.Cylinder, new Color(0.08f, 0.08f, 0.08f));
            Ensure(ref hatTop, PrimitiveType.Cylinder, new Color(0.08f, 0.08f, 0.08f));

            Transform head = Head;
            hatBrim.Object.transform.SetPositionAndRotation(head.position + head.up * (0.17f * scale), head.rotation);
            hatBrim.Object.transform.localScale = new Vector3(0.36f, 0.006f, 0.36f) * scale;
            hatTop.Object.transform.SetPositionAndRotation(head.position + head.up * (0.27f * scale), head.rotation);
            hatTop.Object.transform.localScale = new Vector3(0.22f, 0.1f, 0.22f) * scale;
        }

        public static void DisableTopHat()
        {
            Discard(hatBrim);
            Discard(hatTop);
            hatBrim = hatTop = null;
        }

        private static readonly Piece[] crownPoints = new Piece[6];
        private static Piece crownBand;

        /// <summary>A gold crown on your head, for mirrors and cameras.</summary>
        public static void RoyalCrown()
        {
            float scale = Scale;
            Transform head = Head;
            Color gold = new Color(1f, 0.8f, 0.15f);

            Ensure(ref crownBand, PrimitiveType.Cylinder, gold);
            crownBand.Object.transform.SetPositionAndRotation(head.position + head.up * (0.17f * scale), head.rotation);
            crownBand.Object.transform.localScale = new Vector3(0.22f, 0.03f, 0.22f) * scale;

            for (int i = 0; i < crownPoints.Length; i++)
            {
                Ensure(ref crownPoints[i], PrimitiveType.Sphere, i % 2 == 0 ? gold : new Color(0.9f, 0.1f, 0.2f));
                float angle = i * Mathf.PI * 2f / crownPoints.Length;
                crownPoints[i].Object.transform.position = head.position + head.rotation * new Vector3(Mathf.Cos(angle) * 0.1f, 0.24f, Mathf.Sin(angle) * 0.1f) * scale;
                crownPoints[i].Object.transform.localScale = Vector3.one * (0.045f * scale);
            }
        }

        public static void DisableRoyalCrown()
        {
            Discard(crownBand);
            crownBand = null;
            for (int i = 0; i < crownPoints.Length; i++)
            {
                Discard(crownPoints[i]);
                crownPoints[i] = null;
            }
        }

        private static Piece shield;

        /// <summary>A round shield on your left hand.</summary>
        public static void HandShield()
        {
            Ensure(ref shield, PrimitiveType.Cylinder, MyColor);

            var hand = ControllerUtilities.GetTrueLeftHand();
            // Follows the hand's own turn and twist, and never flips when you point up or down.
            shield.Object.transform.SetPositionAndRotation(hand.position + hand.forward * (0.06f * Scale), hand.rotation * Quaternion.Euler(90f, 0f, 0f));
            shield.Object.transform.localScale = new Vector3(0.35f, 0.01f, 0.35f) * Scale;
            SetAlpha(shield, MyColor, 0.8f);
        }

        public static void DisableHandShield()
        {
            Discard(shield);
            shield = null;
        }

        // ── Scenery ─────────────────────────────────────────────────────────────

        private static Piece logA, logB;
        private static ParticleSystem fire;
        private static Light fireLight;

        /// <summary>A campfire on the ground in front of you; turn it off and on to move it.</summary>
        public static void Campfire()
        {
            float scale = Scale;

            if (fire == null)
            {
                Vector3 at = GroundInFront(1.2f);

                logA = MakePiece(PrimitiveType.Cube, new Color(0.35f, 0.2f, 0.1f), Vector3.one);
                logB = MakePiece(PrimitiveType.Cube, new Color(0.35f, 0.2f, 0.1f), Vector3.one);
                logA.Object.transform.SetPositionAndRotation(at + Vector3.up * (0.04f * scale), Quaternion.Euler(0f, 45f, 0f));
                logB.Object.transform.SetPositionAndRotation(at + Vector3.up * (0.04f * scale), Quaternion.Euler(0f, -45f, 0f));
                logA.Object.transform.localScale = logB.Object.transform.localScale = new Vector3(0.08f, 0.08f, 0.5f) * scale;

                fire = MakeParticles("Nova_Campfire", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.25f, 0f));
                    main.gravityModifier = -0.05f;
                    main.maxParticles = 150;

                    var emission = system.emission;
                    emission.rateOverTime = 80f;

                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 10f;
                    shape.radius = 0.12f;

                    var noise = system.noise;
                    noise.enabled = true;
                    noise.strength = 0.3f;
                });

                fire.transform.SetPositionAndRotation(at + Vector3.up * (0.06f * scale), Quaternion.LookRotation(Vector3.up));
                fireLight = fire.gameObject.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1f, 0.55f, 0.2f);
                fireLight.range = 5f * scale;
            }

            // A flicker, not a steady glow.
            fireLight.intensity = 1.6f + Mathf.PerlinNoise(Time.time * 6f, 0f) * 1.2f;
        }

        public static void DisableCampfire()
        {
            Discard(logA);
            Discard(logB);
            logA = logB = null;
            DestroyParticles(ref fire);
            fireLight = null;
        }

        private static Piece fountainBase;
        private static ParticleSystem fountain;

        /// <summary>A fountain on the ground in front of you; turn it off and on to move it.</summary>
        public static void Fountain()
        {
            if (fountain != null)
                return;

            float scale = Scale;
            Vector3 at = GroundInFront(2f);

            fountainBase = MakePiece(PrimitiveType.Cylinder, new Color(0.75f, 0.75f, 0.8f), Vector3.one);
            fountainBase.Object.transform.position = at + Vector3.up * (0.1f * scale);
            fountainBase.Object.transform.localScale = new Vector3(0.6f, 0.1f, 0.6f) * scale;

            fountain = MakeParticles("Nova_Fountain", null, system =>
            {
                var main = system.main;
                main.loop = true;
                main.startLifetime = 1.4f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 3.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.85f, 1f, 0.9f), Color.white);
                main.gravityModifier = 1f * Scale;
                main.maxParticles = 400;

                var emission = system.emission;
                emission.rateOverTime = 200f;

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 8f;
                shape.radius = 0.03f;
            });

            fountain.transform.SetPositionAndRotation(at + Vector3.up * (0.2f * scale), Quaternion.LookRotation(Vector3.up));
        }

        public static void DisableFountain()
        {
            Discard(fountainBase);
            fountainBase = null;
            DestroyParticles(ref fountain);
        }

        private const int FlowerCount = 24;
        private static readonly List<Piece> blooms = new List<Piece>();
        private static readonly List<Piece> stems = new List<Piece>();
        private static int nextFlower;
        private static float flowerTimer;

        /// <summary>Flowers grow where you walk.</summary>
        public static void FlowerTrail()
        {
            float now = Time.time;
            float scale = Scale;

            if (blooms.Count == 0)
                for (int i = 0; i < FlowerCount; i++)
                {
                    Piece bloom = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one);
                    Piece stem = MakePiece(PrimitiveType.Cylinder, new Color(0.2f, 0.7f, 0.25f), Vector3.one);
                    bloom.Object.SetActive(false);
                    stem.Object.SetActive(false);
                    blooms.Add(bloom);
                    stems.Add(stem);
                }

            // Each new flower grows in over a moment.
            for (int i = 0; i < blooms.Count; i++)
            {
                if (!blooms[i].Alive || !stems[i].Alive || !blooms[i].Object.activeSelf)
                    continue;

                // Finished flowers stand still; only ones still growing are touched.
                if (blooms[i].Life >= 1f)
                    continue;

                float grow = Mathf.Clamp01((now - blooms[i].Born) / 0.4f);
                blooms[i].Life = grow;
                Vector3 root = blooms[i].Anchor;
                stems[i].Object.transform.position = root + Vector3.up * (0.06f * grow * scale);
                stems[i].Object.transform.localScale = new Vector3(0.012f, 0.06f * grow, 0.012f) * scale;
                blooms[i].Object.transform.position = root + Vector3.up * (0.13f * grow * scale);
                blooms[i].Object.transform.localScale = Vector3.one * (0.06f * grow * scale);
            }

            Vector3 velocity = Extras.Body.linearVelocity;
            velocity.y = 0f;
            if (now < flowerTimer || velocity.sqrMagnitude < 1f * scale * scale || !Extras.Grounded())
                return;

            if (!Physics.Raycast(Extras.BodyPosition, Vector3.down, out RaycastHit hit, 1.5f * scale, GTPlayer.Instance.locomotionEnabledLayers))
                return;

            flowerTimer = now + 0.35f;
            int index = nextFlower;
            nextFlower = (nextFlower + 1) % blooms.Count;

            if (!blooms[index].Alive)
                blooms[index] = MakePiece(PrimitiveType.Sphere, Color.white, Vector3.one);
            if (!stems[index].Alive)
                stems[index] = MakePiece(PrimitiveType.Cylinder, new Color(0.2f, 0.7f, 0.25f), Vector3.one);

            Vector2 jitter = Random.insideUnitCircle * (0.2f * scale);
            blooms[index].Born = now;
            blooms[index].Life = 0f;
            blooms[index].Anchor = hit.point + new Vector3(jitter.x, 0f, jitter.y);
            blooms[index].Object.SetActive(true);
            stems[index].Object.SetActive(true);
            SetColor(blooms[index], Color.HSVToRGB(Random.value, 0.6f, 1f));
        }

        public static void DisableFlowerTrail()
        {
            ClearPieces(blooms);
            ClearPieces(stems);
        }

        private const int LanternCount = 10;
        private static readonly List<Piece> lanterns = new List<Piece>();

        /// <summary>Glowing paper lanterns drift up into the sky around you.</summary>
        public static void SkyLanterns()
        {
            float now = Time.time;
            float scale = Scale;

            while (lanterns.Count < LanternCount)
                lanterns.Add(MakeLantern(now - Random.Range(0f, 12f)));

            for (int i = 0; i < lanterns.Count; i++)
            {
                if (!lanterns[i].Alive)
                    lanterns[i] = MakeLantern(now);

                Piece lantern = lanterns[i];
                float age = now - lantern.Born;

                // Each lantern rises for twelve seconds from where it was let go, then is
                // let go again beside you.
                if (age > 12f)
                {
                    ReleaseLantern(lantern, now);
                    age = 0f;
                }

                Vector3 start = lantern.Anchor;
                Vector3 drift = new Vector3(Mathf.Sin(age * 0.6f + lantern.Seed), 0f, Mathf.Cos(age * 0.5f + lantern.Seed)) * 0.3f;
                lantern.Object.transform.position = start + (Vector3.up * (age * 0.8f) + drift) * scale;
                lantern.Object.transform.localScale = new Vector3(0.14f, 0.2f, 0.14f) * scale;
                SetAlpha(lantern, new Color(1f, 0.55f + 0.1f * Mathf.Sin(now * 3f + lantern.Seed), 0.2f), 0.85f * Mathf.Clamp01((12f - age) / 2f));
            }
        }

        private static Piece MakeLantern(float born)
        {
            Piece lantern = MakePiece(PrimitiveType.Cube, new Color(1f, 0.6f, 0.2f, 0.85f), Vector3.one);
            ReleaseLantern(lantern, born);
            return lantern;
        }

        private static void ReleaseLantern(Piece lantern, float born)
        {
            Vector2 around = Random.insideUnitCircle * 4f;
            lantern.Born = born;
            lantern.Anchor = Extras.BodyPosition + new Vector3(around.x, 0f, around.y) * Scale;
        }

        public static void DisableSkyLanterns() => ClearPieces(lanterns);

        private static ParticleSystem meteors;
        private static float nextMeteor;

        /// <summary>Shooting stars streak across the sky now and then.</summary>
        public static void MeteorShower()
        {
            if (meteors == null)
            {
                meteors = MakeParticles("Nova_Meteors", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.startSpeed = 0f;
                    main.maxParticles = 30;
                    main.scalingMode = ParticleSystemScalingMode.Local;

                    var emission = system.emission;
                    emission.enabled = false;
                });

                meteors.transform.localScale = Vector3.one;
                ParticleSystemRenderer streaks = meteors.GetComponent<ParticleSystemRenderer>();
                streaks.renderMode = ParticleSystemRenderMode.Stretch;
                streaks.velocityScale = 0.08f;
                streaks.lengthScale = 2f;
            }

            if (Time.time < nextMeteor)
                return;

            nextMeteor = Time.time + Random.Range(0.6f, 2.5f);

            Vector2 around = Random.insideUnitCircle.normalized * Random.Range(20f, 40f);
            Vector3 heading = new Vector3(Random.Range(-1f, 1f), -0.4f, Random.Range(-1f, 1f)).normalized;

            meteors.Emit(new ParticleSystem.EmitParams
            {
                position = Head.position + new Vector3(around.x, Random.Range(25f, 40f), around.y),
                velocity = heading * Random.Range(35f, 55f),
                startSize = Random.Range(0.15f, 0.3f),
                startLifetime = 1.2f,
                startColor = Color.Lerp(Color.white, new Color(0.6f, 0.8f, 1f), Random.value)
            }, 1);
        }

        public static void DisableMeteorShower() => DestroyParticles(ref meteors);

        private static Piece buddyHead, buddyBody, buddyLeft, buddyRight;

        /// <summary>A glowing buddy in front of you that copies every move, like a mirror.</summary>
        public static void MirrorBuddy()
        {
            float scale = Scale;
            Color color = MyColor;
            Ensure(ref buddyHead, PrimitiveType.Sphere, color);
            Ensure(ref buddyBody, PrimitiveType.Capsule, color);
            Ensure(ref buddyLeft, PrimitiveType.Sphere, color);
            Ensure(ref buddyRight, PrimitiveType.Sphere, color);

            // The mirror stands a little in front of you and turns as you do.
            Vector3 normal = LookFlat();
            Vector3 plane = Extras.BodyPosition + normal * (1.2f * scale);
            Vector3 Mirror(Vector3 point) => point - 2f * Vector3.Dot(point - plane, normal) * normal;

            buddyHead.Object.transform.position = Mirror(Head.position);
            buddyHead.Object.transform.localScale = Vector3.one * (0.25f * scale);
            buddyBody.Object.transform.position = Mirror(Extras.BodyPosition);
            buddyBody.Object.transform.localScale = new Vector3(0.35f, 0.3f, 0.3f) * scale;

            // Your left hand is its right, as in a real mirror.
            buddyLeft.Object.transform.position = Mirror(GorillaTagger.Instance.rightHandTransform.position);
            buddyRight.Object.transform.position = Mirror(GorillaTagger.Instance.leftHandTransform.position);
            buddyLeft.Object.transform.localScale = buddyRight.Object.transform.localScale = Vector3.one * (0.1f * scale);

            SetAlpha(buddyHead, color, 0.6f);
            SetAlpha(buddyBody, color, 0.45f);
            SetAlpha(buddyLeft, color, 0.6f);
            SetAlpha(buddyRight, color, 0.6f);
        }

        public static void DisableMirrorBuddy()
        {
            Discard(buddyHead);
            Discard(buddyBody);
            Discard(buddyLeft);
            Discard(buddyRight);
            buddyHead = buddyBody = buddyLeft = buddyRight = null;
        }

        // ── Music ───────────────────────────────────────────────────────────────

        private static readonly AudioSource[] pianoVoices = new AudioSource[4];
        private static AudioClip pianoClip;
        private static int nextVoice;
        private static bool pianoHeld;

        // A major pentatonic scale over two octaves: any note you hit sounds good.
        private static readonly int[] PentatonicSteps = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };

        /// <summary>A soft bell note, made once and pitched for every key.</summary>
        private static AudioClip PianoClip()
        {
            if (pianoClip != null)
                return pianoClip;

            const int rate = 44100;
            float[] samples = new float[rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Exp(-t * 4f) * Mathf.Clamp01(t * 200f);
                samples[i] = envelope * (Mathf.Sin(2f * Mathf.PI * 261.63f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 784.88f * t) * 0.1f);
            }

            pianoClip = AudioClip.Create("Nova_PianoNote", samples.Length, 1, rate, false);
            pianoClip.SetData(samples, 0);
            return pianoClip;
        }

        /// <summary>Pull your left trigger to play a note; raise or lower your hand to change it.</summary>
        public static void AirPiano()
        {
            if (!Pressed(leftTrigger > 0.5f, ref pianoHeld))
                return;

            for (int i = 0; i < pianoVoices.Length; i++)
                if (pianoVoices[i] == null)
                {
                    pianoVoices[i] = new GameObject("Nova_PianoVoice").AddComponent<AudioSource>();
                    pianoVoices[i].spatialBlend = 0f;
                    pianoVoices[i].volume = 0.4f;
                    pianoVoices[i].playOnAwake = false;
                }

            // From about your waist to above your head spans the two octaves.
            Vector3 hand = GorillaTagger.Instance.leftHandTransform.position;
            float height = Mathf.InverseLerp(Head.position.y - 0.8f * Scale, Head.position.y + 0.4f * Scale, hand.y);
            int step = PentatonicSteps[Mathf.Clamp(Mathf.RoundToInt(height * (PentatonicSteps.Length - 1)), 0, PentatonicSteps.Length - 1)];

            AudioSource voice = pianoVoices[nextVoice];
            nextVoice = (nextVoice + 1) % pianoVoices.Length;
            voice.transform.position = Head.position;
            // Pitched down an octave and up an octave from the clip's middle C, inside the
            // range Unity can play; four times faster would be capped and sound the same.
            voice.pitch = Mathf.Pow(2f, (step - 12) / 12f);
            voice.clip = PianoClip();
            voice.Play();

            Burst(hand, 8, 0.8f, falling: false);
        }

        public static void DisableAirPiano()
        {
            for (int i = 0; i < pianoVoices.Length; i++)
            {
                if (pianoVoices[i] != null)
                    Object.Destroy(pianoVoices[i].gameObject);
                pianoVoices[i] = null;
            }
        }

        // ── Saved positions ─────────────────────────────────────────────────────

        private static readonly Vector3?[] savedPositions = new Vector3?[3];

        public static void SavePosition(int slot)
        {
            savedPositions[slot] = Extras.BodyPosition;
            NotificationManager.SendNotification($"Saved position {slot + 1}.", 2500);
        }

        public static void GoToPosition(int slot)
        {
            if (!(savedPositions[slot] is Vector3 position))
            {
                NotificationManager.SendNotification($"Position {slot + 1} has not been saved yet.", 3000);
                return;
            }

            // A spot saved in another map has nothing under it once that map is unloaded,
            // and going there would drop you into the void.
            if (!Physics.Raycast(position + Vector3.up * Scale, Vector3.down, 4f * Scale, GTPlayer.Instance.locomotionEnabledLayers))
            {
                NotificationManager.SendNotification($"Position {slot + 1} is in a map that isn't loaded right now.", 4000);
                return;
            }

            TeleportPlayer(position);
        }
    }
}
