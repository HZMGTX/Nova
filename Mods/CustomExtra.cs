/*
 * Nova Menu  Mods/CustomExtra.cs
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
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static Nova.Menu.Main;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Nova.Mods
{
    /// <summary>The fourth set of Custom Mods: fishing, Simon Says, parkour, drums, a dragon and the sky.</summary>
    public static partial class Custom
    {
        // ── Shared ──────────────────────────────────────────────────────────────

        private static int handsFrame = -2;
        private static Vector3 handsLeft, handsRight, leftHandMotion, rightHandMotion;

        /// <summary>How fast each hand moves compared with your body, worked out once a frame.</summary>
        /// <remarks>
        /// Your own movement is taken off, so swinging through the air doesn't count as a
        /// punch. After a frame without a reading the last positions are stale and would
        /// look like a huge swing, so that frame reads as still.
        /// </remarks>
        private static void HandMotion(out Vector3 left, out Vector3 right)
        {
            int frame = Time.frameCount;
            if (handsFrame != frame)
            {
                Vector3 nowLeft = GorillaTagger.Instance.leftHandTransform.position;
                Vector3 nowRight = GorillaTagger.Instance.rightHandTransform.position;
                float dt = Time.deltaTime;

                if (handsFrame == frame - 1 && dt > 0f)
                {
                    Vector3 body = Extras.Body.linearVelocity;
                    leftHandMotion = (nowLeft - handsLeft) / dt - body;
                    rightHandMotion = (nowRight - handsRight) / dt - body;
                }
                else
                    leftHandMotion = rightHandMotion = Vector3.zero;

                handsLeft = nowLeft;
                handsRight = nowRight;
                handsFrame = frame;
            }

            left = leftHandMotion;
            right = rightHandMotion;
        }

        private static readonly AudioSource[] voices = new AudioSource[8];
        private static int nextSharedVoice;

        /// <summary>Plays a sound only you hear, on a small pool of voices kept for every mod.</summary>
        private static void PlaySound(AudioClip clip, float pitch, float volume)
        {
            AudioSource voice = voices[nextSharedVoice];
            if (voice == null)
            {
                voice = voices[nextSharedVoice] = new GameObject("Nova_CustomVoice").AddComponent<AudioSource>();
                voice.spatialBlend = 0f;
                voice.playOnAwake = false;
                Object.DontDestroyOnLoad(voice.gameObject);
            }

            nextSharedVoice = (nextSharedVoice + 1) % voices.Length;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.clip = clip;
            voice.Play();
        }

        /// <summary>Floats text at a spot in the world, turned to face you.</summary>
        private static void PlaceFacingMe(TextMeshPro text, Vector3 at)
        {
            text.SafeSetFont(activeFont);
            text.transform.localScale = Vector3.one * (0.1f * Scale);
            text.transform.position = at;
            text.transform.LookAt(Head.position);
            text.transform.Rotate(0f, 180f, 0f);
        }

        /// <summary>The ground under a spot, looked for from just above your head so a roof isn't mistaken for it.</summary>
        private static float GroundUnder(Vector3 at, float fallback)
        {
            Vector3 from = new Vector3(at.x, Head.position.y + 0.5f * Scale, at.z);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 10f * Scale, GTPlayer.Instance.locomotionEnabledLayers)
                ? hit.point.y
                : fallback;
        }

        private static void Vibrate(bool left, float strength) =>
            GorillaTagger.Instance.StartVibration(left, GorillaTagger.Instance.tagHapticStrength * Mathf.Clamp01(strength), 0.05f);

        // ── Mini-games ──────────────────────────────────────────────────────────

        private enum Fishing { Idle, Flying, Waiting, Bite, Reeling }

        private sealed class Fish
        {
            public string Name;
            public Color Color;
            public float Least, Most, Chance;
        }

        private static readonly Fish[] FishTypes =
        {
            new Fish { Name = "Minnow", Color = new Color(0.75f, 0.8f, 0.85f), Least = 0.05f, Most = 0.2f, Chance = 30f },
            new Fish { Name = "Bass", Color = new Color(0.3f, 0.55f, 0.25f), Least = 0.5f, Most = 3f, Chance = 22f },
            new Fish { Name = "Salmon", Color = new Color(1f, 0.5f, 0.45f), Least = 2f, Most = 8f, Chance = 16f },
            new Fish { Name = "Pufferfish", Color = new Color(0.95f, 0.85f, 0.3f), Least = 0.3f, Most = 1.5f, Chance = 10f },
            new Fish { Name = "Rainbow Trout", Color = new Color(0.6f, 0.4f, 1f), Least = 0.5f, Most = 4f, Chance = 10f },
            new Fish { Name = "Old Boot", Color = new Color(0.35f, 0.22f, 0.12f), Least = 0.8f, Most = 1.2f, Chance = 6f },
            new Fish { Name = "Golden Carp", Color = new Color(1f, 0.8f, 0.15f), Least = 3f, Most = 12f, Chance = 4f },
            new Fish { Name = "Banana Fish", Color = new Color(1f, 0.95f, 0.2f), Least = 0.2f, Most = 0.6f, Chance = 1.5f },
            new Fish { Name = "Gorilla Shark", Color = new Color(0.4f, 0.5f, 0.65f), Least = 50f, Most = 150f, Chance = 0.5f }
        };

        private static Fishing fishing = Fishing.Idle;
        private static Piece rod, bobber, hookedFish;
        private static LineRenderer fishingLine;
        private static TextMeshPro fishText;
        private static Vector3 bobberPosition, bobberVelocity, reelFrom;
        private static float fishingAt, biteAt;
        private static Fish caught;
        private static float caughtWeight, bestWeight;
        private static string bestCatch, fishMessage;
        private static int fishCount;
        private static bool fishHeld;

        /// <summary>Pull the right trigger to cast; when the bobber dips, pull again to catch a fish.</summary>
        public static void FishingRod()
        {
            float scale = Scale;
            float now = Time.time;
            var hand = ControllerUtilities.GetTrueRightHand();
            Vector3 tip = hand.position + hand.forward * (0.9f * scale);

            Ensure(ref rod, PrimitiveType.Cylinder, new Color(0.45f, 0.3f, 0.15f));
            rod.Object.transform.SetPositionAndRotation(hand.position + hand.forward * (0.45f * scale), Quaternion.LookRotation(hand.forward) * Quaternion.Euler(90f, 0f, 0f));
            rod.Object.transform.localScale = new Vector3(0.015f, 0.45f, 0.015f) * scale;

            Ensure(ref bobber, PrimitiveType.Sphere, new Color(1f, 0.2f, 0.2f));
            bobber.Object.transform.localScale = Vector3.one * (0.05f * scale);

            if (fishingLine == null)
            {
                fishingLine = MakeLine("Nova_FishingLine", 0.002f);
                fishingLine.positionCount = 3;
                fishingLine.startColor = fishingLine.endColor = new Color(1f, 1f, 1f, 0.7f);
            }

            if (fishText == null)
            {
                fishText = MakeWristText("Nova_Fishing", new Color(0.5f, 0.85f, 1f));
                ShowCatches(null);
            }

            bool pressed = Pressed(rightTrigger > 0.5f, ref fishHeld);

            switch (fishing)
            {
                case Fishing.Idle:
                    bobberPosition = tip + Vector3.down * (0.15f * scale);
                    if (pressed)
                    {
                        fishing = Fishing.Flying;
                        fishingAt = now;
                        bobberPosition = tip;
                        bobberVelocity = hand.forward * (8f * scale) + Vector3.up * (2f * scale) + Extras.Body.linearVelocity;
                        fishMessage = null;
                        ShowCatches("Waiting for a bite...");
                    }
                    break;

                case Fishing.Flying:
                    float dt = Time.deltaTime;
                    bobberVelocity += Physics.gravity * (dt * scale);
                    Vector3 move = bobberVelocity * dt;
                    float distance = move.magnitude;

                    if (distance > 0f && Physics.SphereCast(bobberPosition, 0.025f * scale, move / distance, out RaycastHit hit, distance, GTPlayer.Instance.locomotionEnabledLayers))
                    {
                        bobberPosition = hit.point + hit.normal * (0.025f * scale);
                        fishing = Fishing.Waiting;
                        biteAt = now + Random.Range(2f, 7f);
                        Burst(bobberPosition, 10, 1f, falling: true, direction: hit.normal);
                    }
                    else
                        bobberPosition += move;

                    if (pressed || now - fishingAt > 5f)
                        Reel(null);
                    break;

                case Fishing.Waiting:
                case Fishing.Bite:
                    if (TooFar(bobberPosition, 25f))
                    {
                        Reel(null);
                        break;
                    }

                    if (fishing == Fishing.Waiting && now >= biteAt)
                    {
                        fishing = Fishing.Bite;
                        fishingAt = now;
                        Burst(bobberPosition, 16, 1.2f, falling: true, direction: Vector3.up);
                        Vibrate(false, 0.8f);
                        ShowCatches("<color=yellow>Bite! Pull now!</color>");
                    }

                    if (fishing == Fishing.Bite && now - fishingAt > 1f)
                    {
                        fishing = Fishing.Waiting;
                        biteAt = now + Random.Range(3f, 7f);
                        ShowCatches("It got away! Waiting...");
                    }

                    if (pressed)
                    {
                        if (fishing == Fishing.Bite)
                            Reel(PickFish());
                        else
                        {
                            fishMessage = "Too soon, nothing bit yet.";
                            Reel(null);
                        }
                    }
                    break;

                case Fishing.Reeling:
                    float reeled = Mathf.Clamp01((now - fishingAt) / 0.5f);
                    bobberPosition = Vector3.Lerp(reelFrom, tip, reeled);
                    if (reeled >= 1f)
                    {
                        // A catch stays on the wrist until the next cast; an empty reel says why, or
                        // goes back to your best.
                        if (caught != null)
                            Landed();
                        else
                            ShowCatches(fishMessage);
                        fishMessage = null;
                        fishing = Fishing.Idle;
                    }
                    break;
            }

            // The bobber sits on the water line, and ducks under when something bites.
            Vector3 shown = bobberPosition;
            if (fishing == Fishing.Waiting)
                shown += Vector3.up * (Mathf.Sin(now * 2f) * 0.008f * scale);
            else if (fishing == Fishing.Bite)
                shown += Vector3.down * (Mathf.Abs(Mathf.Sin(now * 18f)) * 0.04f * scale);
            bobber.Object.transform.position = shown;

            bool showFish = fishing == Fishing.Reeling && caught != null;
            if (showFish)
            {
                Ensure(ref hookedFish, PrimitiveType.Sphere, caught.Color);
                SetColor(hookedFish, caught.Color);
                hookedFish.Object.SetActive(true);
                hookedFish.Object.transform.SetPositionAndRotation(shown + Vector3.down * (0.08f * scale), Quaternion.LookRotation(Vector3.down));
                hookedFish.Object.transform.localScale = new Vector3(0.06f, 0.05f, 0.14f) * scale;
            }
            else if (hookedFish != null && hookedFish.Alive)
                hookedFish.Object.SetActive(false);

            // The line sags while it is slack in the water.
            float sag = fishing == Fishing.Waiting || fishing == Fishing.Bite ? 0.25f * scale : 0f;
            fishingLine.startWidth = fishingLine.endWidth = 0.002f * scale;
            fishingLine.SetPosition(0, tip);
            fishingLine.SetPosition(1, Vector3.Lerp(tip, shown, 0.5f) + Vector3.down * sag);
            fishingLine.SetPosition(2, shown);

            PlaceOnWrist(fishText, 0.36f);
        }

        private static void Reel(Fish fish)
        {
            caught = fish;
            caughtWeight = fish == null ? 0f : Mathf.Round(Random.Range(fish.Least, fish.Most) * 10f) / 10f;
            reelFrom = bobberPosition;
            fishingAt = Time.time;
            fishing = Fishing.Reeling;
        }

        private static Fish PickFish()
        {
            float total = 0f;
            foreach (Fish fish in FishTypes)
                total += fish.Chance;

            float roll = Random.value * total;
            foreach (Fish fish in FishTypes)
            {
                roll -= fish.Chance;
                if (roll <= 0f)
                    return fish;
            }

            return FishTypes[0];
        }

        private static void Landed()
        {
            fishCount++;
            bool rare = caught.Chance <= 4f;
            if (caughtWeight > bestWeight)
            {
                bestWeight = caughtWeight;
                bestCatch = caught.Name;
            }

            Burst(GorillaTagger.Instance.rightHandTransform.position, rare ? 60 : 20, rare ? 3f : 1.5f, falling: true);
            Vibrate(false, 1f);
            ShowCatches($"{caught.Name} {caughtWeight:0.0} kg");
            NotificationManager.SendNotification(rare
                ? $"<color=yellow>Rare catch!</color> You caught a {caught.Name} ({caughtWeight:0.0} kg)!"
                : $"You caught a {caught.Name} ({caughtWeight:0.0} kg).", 3000);
            caught = null;
        }

        private static void ShowCatches(string last) =>
            fishText.SafeSetText($"Fish: {fishCount}\n<size=70%>{last ?? (bestCatch == null ? "Pull the trigger to cast" : $"Best: {bestCatch} {bestWeight:0.0} kg")}</size>");

        public static void DisableFishingRod()
        {
            Discard(rod);
            Discard(bobber);
            Discard(hookedFish);
            rod = bobber = hookedFish = null;
            DestroyLine(ref fishingLine);
            DestroyText(ref fishText);
            fishing = Fishing.Idle;
            caught = null;
            fishMessage = null;
        }

        private enum Simon { Intro, Showing, Input, Passed, Failed }

        private static readonly Color[] SimonColors = { new Color(1f, 0.2f, 0.2f), new Color(0.2f, 0.9f, 0.3f), new Color(0.25f, 0.45f, 1f), new Color(1f, 0.85f, 0.15f) };
        private static readonly int[] SimonNotes = { 0, 4, 7, 12 };
        private static readonly Piece[] simonPads = new Piece[4];
        private static readonly float[] simonLitUntil = new float[4];
        private static readonly List<int> simonSequence = new List<int>();
        private static Simon simon = Simon.Intro;
        private static Vector3 simonCentre, simonFacing;
        private static bool simonPlaced;
        private static float simonAt;
        private static int simonStep, simonBest;
        private static readonly int[] simonHandOn = { -1, -1 };
        private static TextMeshPro simonText;

        /// <summary>Coloured pads light up in a growing pattern; touch them in the same order.</summary>
        public static void SimonSays()
        {
            float scale = Scale;
            float now = Time.time;

            if (!simonPlaced || TooFar(simonCentre, 2.5f))
            {
                simonFacing = LookFlat();
                simonCentre = Head.position + simonFacing * (0.45f * scale) + Vector3.down * (0.35f * scale);
                if (!simonPlaced)
                {
                    simon = Simon.Intro;
                    simonAt = now + 1.5f;
                }
                simonPlaced = true;
            }

            if (simonText == null)
                simonText = MakeWristText("Nova_SimonSays", Color.white);

            Vector3 right = Vector3.Cross(Vector3.up, simonFacing);
            float reach = 0.1f * scale;
            Vector3 leftHand = GorillaTagger.Instance.leftHandTransform.position;
            Vector3 rightHand = GorillaTagger.Instance.rightHandTransform.position;
            int leftOn = -1, rightOn = -1;

            for (int i = 0; i < simonPads.Length; i++)
            {
                Ensure(ref simonPads[i], PrimitiveType.Sphere, SimonColors[i]);
                float across = i - 1.5f;
                Vector3 at = simonCentre + right * (across * 0.22f * scale) - simonFacing * (Mathf.Abs(across) * 0.06f * scale);

                bool lit = now < simonLitUntil[i];
                simonPads[i].Object.transform.position = at;
                simonPads[i].Object.transform.localScale = Vector3.one * ((lit ? 0.17f : 0.14f) * scale);
                SetAlpha(simonPads[i], lit ? SimonColors[i] : SimonColors[i] * 0.4f, lit ? 1f : 0.6f);

                if ((leftHand - at).sqrMagnitude < reach * reach)
                    leftOn = i;
                if ((rightHand - at).sqrMagnitude < reach * reach)
                    rightOn = i;
            }

            // A touch counts once, when a hand reaches a pad, not for every frame it stays
            // there; each hand is followed on its own, so lifting one never counts the other.
            int touched = Touch(0, leftOn);
            int touchedRight = Touch(1, rightOn);
            if (touched < 0)
                touched = touchedRight;

            switch (simon)
            {
                case Simon.Intro:
                    simonText.SafeSetText("Simon Says\n<size=70%>Watch the pads</size>");
                    if (now >= simonAt)
                    {
                        simonSequence.Clear();
                        simonSequence.Add(Random.Range(0, 4));
                        StartShowing(now + 0.5f);
                    }
                    break;

                case Simon.Showing:
                    if (now >= simonAt)
                    {
                        if (simonStep < simonSequence.Count)
                        {
                            // Longer patterns play a little faster.
                            float gap = 0.6f - Mathf.Min(0.25f, simonSequence.Count * 0.02f);
                            LightPad(simonSequence[simonStep], gap * 0.75f);
                            simonStep++;
                            simonAt = now + gap;
                        }
                        else
                        {
                            simon = Simon.Input;
                            simonStep = 0;
                            simonAt = now + 6f;
                            simonText.SafeSetText($"Your turn!\n<size=70%>Round {simonSequence.Count}</size>");
                        }
                    }
                    break;

                case Simon.Input:
                    if (touched >= 0)
                    {
                        LightPad(touched, 0.3f);
                        if (touched != simonSequence[simonStep])
                            SimonFailed(now);
                        else if (++simonStep == simonSequence.Count)
                        {
                            simon = Simon.Passed;
                            simonAt = now + 1f;
                            simonBest = Mathf.Max(simonBest, simonSequence.Count);
                            simonText.SafeSetText($"Round {simonSequence.Count} cleared!\n<size=70%>Best: {simonBest}</size>");
                            Burst(simonCentre + Vector3.up * (0.2f * scale), 25, 1.5f, falling: true);
                        }
                        else
                            simonAt = now + 6f;
                    }
                    else if (now >= simonAt)
                        SimonFailed(now);
                    break;

                case Simon.Passed:
                    if (now >= simonAt)
                    {
                        simonSequence.Add(Random.Range(0, 4));
                        StartShowing(now + 0.3f);
                    }
                    break;

                case Simon.Failed:
                    if (now >= simonAt)
                    {
                        simon = Simon.Intro;
                        simonAt = now + 1f;
                    }
                    break;
            }

            PlaceFacingMe(simonText, simonCentre + Vector3.up * (0.3f * scale));
        }

        private static int Touch(int hand, int pad)
        {
            int touched = pad >= 0 && pad != simonHandOn[hand] ? pad : -1;
            simonHandOn[hand] = pad;
            return touched;
        }

        private static void StartShowing(float at)
        {
            simon = Simon.Showing;
            simonStep = 0;
            simonAt = at;
            simonText.SafeSetText($"Watch...\n<size=70%>Round {simonSequence.Count}</size>");
        }

        private static void LightPad(int pad, float time)
        {
            simonLitUntil[pad] = Time.time + time;
            PlaySound(PianoClip(), Mathf.Pow(2f, SimonNotes[pad] / 12f), 0.45f);
        }

        private static void SimonFailed(float now)
        {
            int score = simonSequence.Count - 1;
            simon = Simon.Failed;
            simonAt = now + 2.5f;
            PlaySound(PianoClip(), 0.5f, 0.5f);
            simonText.SafeSetText($"<color=red>Wrong!</color> You got {score}\n<size=70%>Best: {simonBest}</size>");
        }

        public static void DisableSimonSays()
        {
            for (int i = 0; i < simonPads.Length; i++)
            {
                Discard(simonPads[i]);
                simonPads[i] = null;
                simonLitUntil[i] = 0f;
            }

            DestroyText(ref simonText);
            simonPlaced = false;
            simonHandOn[0] = simonHandOn[1] = -1;
        }

        private const int CourseRings = 6, RingBeads = 14;
        private static readonly Vector3[] courseCentres = new Vector3[CourseRings];
        private static readonly Vector3[] courseFacing = new Vector3[CourseRings];
        private static readonly Piece[] ringBeads = new Piece[RingBeads * 2];
        private static int courseRing = -1, ringsShown = -1, ringsFrame = -2;
        private static float courseStart, courseFinished, ringsLayoutScale, courseBest = float.MaxValue, ringsNextText;
        private static Vector3 ringsLastHead;
        private static TextMeshPro ringText;

        /// <summary>A course of rings to go through as fast as you can; your best time is kept.</summary>
        public static void ParkourRings()
        {
            float scale = Scale;
            float now = Time.time;
            Vector3 head = Head.position;

            if (ringText == null)
                ringText = MakeWristText("Nova_ParkourRings", new Color(0.4f, 1f, 0.9f));

            bool finished = courseRing >= CourseRings;
            if (courseRing < 0 || (finished && now - courseFinished > 3f) || (!finished && TooFar(courseCentres[courseRing], 25f)))
            {
                LayCourse();
                finished = false;
            }

            bool layout = ringsShown != courseRing || !Mathf.Approximately(ringsLayoutScale, scale);
            for (int i = 0; i < ringBeads.Length; i++)
                if (ringBeads[i] == null || !ringBeads[i].Alive)
                {
                    Ensure(ref ringBeads[i], PrimitiveType.Sphere, Color.white);
                    layout = true;
                }

            float radius = 0.55f * scale;
            if (layout)
            {
                ringsShown = courseRing;
                ringsLayoutScale = scale;

                // The ring to go through now, and a fainter one after it so you know where to head.
                for (int ring = 0; ring < 2; ring++)
                {
                    int index = courseRing + ring;
                    bool show = !finished && index < CourseRings;
                    for (int b = 0; b < RingBeads; b++)
                    {
                        Piece bead = ringBeads[ring * RingBeads + b];
                        bead.Object.SetActive(show);
                        if (!show)
                            continue;

                        Vector3 across = Vector3.Cross(Vector3.up, courseFacing[index]);
                        float angle = b * Mathf.PI * 2f / RingBeads;
                        bead.Object.transform.position = courseCentres[index] + (across * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * radius;
                        bead.Object.transform.localScale = Vector3.one * ((ring == 0 ? 0.06f : 0.04f) * scale);
                        if (ring == 1)
                            SetAlpha(bead, new Color(1f, 1f, 1f), 0.3f);
                    }
                }
            }

            if (!finished)
            {
                Color glow = Color.Lerp(new Color(0.3f, 1f, 0.8f), Color.white, Mathf.PingPong(now * 2f, 1f));
                for (int b = 0; b < RingBeads; b++)
                    SetColor(ringBeads[b], glow);

                // Going through means your head crossed the ring's flat face inside its edge,
                // from either side.
                if (ringsFrame == Time.frameCount - 1)
                {
                    Vector3 centre = courseCentres[courseRing];
                    Vector3 facing = courseFacing[courseRing];
                    float before = Vector3.Dot(ringsLastHead - centre, facing);
                    float after = Vector3.Dot(head - centre, facing);
                    Vector3 onFace = head - facing * after;

                    if (Mathf.Sign(before) != Mathf.Sign(after) && (onFace - centre).sqrMagnitude < radius * radius)
                        PassRing(now, centre);
                }

                if (courseRing < CourseRings && now >= ringsNextText)
                {
                    ringsNextText = now + 0.1f;
                    ringText.SafeSetText(courseRing == 0
                        ? $"Ring 1/{CourseRings}\n<size=70%>Go through to start</size>"
                        : $"Ring {courseRing + 1}/{CourseRings}\n<size=70%>{now - courseStart:0.0} s</size>");
                }
            }

            ringsLastHead = head;
            ringsFrame = Time.frameCount;

            int labelled = Mathf.Min(courseRing, CourseRings - 1);
            PlaceFacingMe(ringText, courseCentres[labelled] + Vector3.up * (radius + 0.2f * scale));
        }

        private static void PassRing(float now, Vector3 centre)
        {
            Burst(centre, 25, 2f, falling: false);
            Vibrate(true, 0.5f);
            Vibrate(false, 0.5f);

            if (courseRing == 0)
                courseStart = now;

            if (++courseRing < CourseRings)
                return;

            float taken = now - courseStart;
            bool record = taken < courseBest;
            courseBest = Mathf.Min(courseBest, taken);
            courseFinished = now;
            Burst(centre, 80, 4f, falling: true);
            ringText.SafeSetText($"{(record ? "<color=yellow>New best!</color>" : "Finished!")} {taken:0.00} s\n<size=70%>Best: {courseBest:0.00} s</size>");
        }

        /// <summary>Lays a new course of rings ahead of you, winding a little left and right.</summary>
        private static void LayCourse()
        {
            float scale = Scale;
            Vector3 heading = LookFlat();
            Vector3 at = Extras.BodyPosition;

            for (int i = 0; i < CourseRings; i++)
            {
                heading = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * heading;
                Vector3 next = at + heading * (Random.Range(3f, 5f) * scale) + Vector3.Cross(Vector3.up, heading) * (Random.Range(-1.5f, 1.5f) * scale);
                next.y = GroundUnder(next, Extras.BodyPosition.y - 0.6f * scale) + Random.Range(0.9f, 1.8f) * scale;

                Vector3 facing = next - at;
                facing.y = 0f;
                courseFacing[i] = facing.sqrMagnitude > 0.0001f ? facing.normalized : heading;
                courseCentres[i] = next;
                at = next;
            }

            courseRing = 0;
            ringsShown = -1;
        }

        public static void DisableParkourRings()
        {
            for (int i = 0; i < ringBeads.Length; i++)
            {
                Discard(ringBeads[i]);
                ringBeads[i] = null;
            }

            DestroyText(ref ringText);
            courseRing = ringsShown = -1;
        }

        private static Piece dummyPost, dummyBody, dummyHead;
        private static Vector3 dummyBase, dummyTilt, dummyTiltSpeed;
        private static bool dummyPlaced, dummyLeftIn, dummyRightIn;
        private static float dummyLastHit, dummyBestHit;
        private static int dummyCombo;
        private static TextMeshPro dummyText;

        /// <summary>A training dummy in front of you to punch; it shows how hard you hit and your combo.</summary>
        public static void TrainingDummy()
        {
            float scale = Scale;
            float now = Time.time;
            float dt = Time.deltaTime;

            if (!dummyPlaced || TooFar(dummyBase, 30f))
            {
                dummyBase = GroundInFront(1f);
                dummyTilt = dummyTiltSpeed = Vector3.zero;
                dummyPlaced = true;
            }

            Ensure(ref dummyPost, PrimitiveType.Cylinder, new Color(0.4f, 0.28f, 0.15f));
            Ensure(ref dummyBody, PrimitiveType.Capsule, new Color(0.85f, 0.75f, 0.5f));
            Ensure(ref dummyHead, PrimitiveType.Sphere, new Color(0.85f, 0.75f, 0.5f));

            if (dummyText == null)
            {
                dummyText = MakeWristText("Nova_TrainingDummy", Color.white);
                dummyText.SafeSetText("Punch me!");
                dummyCombo = 0;
            }

            dummyPost.Object.transform.position = dummyBase + Vector3.up * (0.2f * scale);
            dummyPost.Object.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f) * scale;

            // It rocks on a spring when hit and settles back upright.
            dummyTiltSpeed += (-dummyTilt * 60f - dummyTiltSpeed * 6f) * dt;
            dummyTilt += dummyTiltSpeed * dt;
            dummyTilt = Vector3.ClampMagnitude(dummyTilt, 0.8f);

            Quaternion lean = Quaternion.FromToRotation(Vector3.up, (Vector3.up + dummyTilt).normalized);
            Vector3 hip = dummyBase + Vector3.up * (0.4f * scale);
            Vector3 chest = hip + lean * Vector3.up * (0.25f * scale);
            Vector3 face = hip + lean * Vector3.up * (0.6f * scale);

            dummyBody.Object.transform.SetPositionAndRotation(chest, lean);
            dummyBody.Object.transform.localScale = new Vector3(0.32f, 0.22f, 0.32f) * scale;
            dummyHead.Object.transform.position = face;
            dummyHead.Object.transform.localScale = Vector3.one * (0.22f * scale);

            float flash = Mathf.Clamp01(1f - (now - dummyLastHit) / 0.3f);
            Color skin = Color.Lerp(new Color(0.85f, 0.75f, 0.5f), new Color(1f, 0.3f, 0.25f), flash);
            SetColor(dummyBody, skin);
            SetColor(dummyHead, skin);

            HandMotion(out Vector3 leftMotion, out Vector3 rightMotion);
            Punch(GorillaTagger.Instance.leftHandTransform.position, leftMotion, true, ref dummyLeftIn, chest, face, lean, now);
            Punch(GorillaTagger.Instance.rightHandTransform.position, rightMotion, false, ref dummyRightIn, chest, face, lean, now);

            if (dummyCombo > 0 && now - dummyLastHit > 1.5f)
            {
                dummyCombo = 0;
                dummyText.SafeSetText($"Punch me!\n<size=70%>Best hit: {dummyBestHit:0.0} m/s</size>");
            }

            PlaceFacingMe(dummyText, face + Vector3.up * (0.3f * scale));
        }

        private static void Punch(Vector3 hand, Vector3 motion, bool left, ref bool wasIn, Vector3 chest, Vector3 face, Quaternion lean, float now)
        {
            float scale = Scale;

            // The body is a capsule around its middle line, the head a ball on top.
            Vector3 up = lean * Vector3.up;
            Vector3 along = chest + up * Mathf.Clamp(Vector3.Dot(hand - chest, up), -0.1f * scale, 0.1f * scale);
            float body = 0.21f * scale, head = 0.16f * scale;
            bool inside = (hand - along).sqrMagnitude < body * body || (hand - face).sqrMagnitude < head * head;

            bool entered = inside && !wasIn;
            wasIn = inside;

            float speed = motion.magnitude / scale;
            if (!entered || speed < 1.5f)
                return;

            dummyCombo = now - dummyLastHit < 1.5f ? dummyCombo + 1 : 1;
            dummyLastHit = now;
            dummyBestHit = Mathf.Max(dummyBestHit, Mathf.Min(speed, 30f));

            Vector3 push = motion / scale;
            push.y = 0f;
            dummyTiltSpeed += Vector3.ClampMagnitude(push, 12f) * 0.35f;

            Burst(hand, Mathf.Clamp((int)(speed * 4f), 6, 40), 1.5f, falling: true, direction: -motion);
            Vibrate(left, speed / 8f);
            dummyText.SafeSetText($"{Mathf.Min(speed, 30f):0.0} m/s\n<size=70%>Combo x{dummyCombo}  ·  Best {dummyBestHit:0.0}</size>");
        }

        public static void DisableTrainingDummy()
        {
            Discard(dummyPost);
            Discard(dummyBody);
            Discard(dummyHead);
            dummyPost = dummyBody = dummyHead = null;
            DestroyText(ref dummyText);
            dummyPlaced = dummyLeftIn = dummyRightIn = false;
        }

        // ── Music ───────────────────────────────────────────────────────────────

        private static readonly string[] DrumNames = { "Hi-Hat", "Snare", "Tom", "Kick" };
        private static readonly Color[] DrumColors = { new Color(1f, 0.8f, 0.2f), new Color(0.95f, 0.95f, 0.95f), new Color(0.9f, 0.25f, 0.2f), new Color(0.2f, 0.3f, 0.8f) };
        private static readonly Piece[] drums = new Piece[4];
        private static readonly float[] drumHitAt = new float[4];
        private static readonly int[] drumHandIn = { -1, -1 };
        private static AudioClip[] drumClips;
        private static Vector3 kitCentre, kitFacing;
        private static bool kitPlaced, kitTurning;

        /// <summary>Four drums around your waist; hit them with your hands to play. Only you hear them.</summary>
        public static void DrumKit()
        {
            float scale = Scale;
            float now = Time.time;
            float dt = Time.deltaTime;

            // The kit comes with you, but only turns once you face well away from it, so it
            // stays put while you play.
            Vector3 waist = Extras.BodyPosition + Vector3.down * (0.2f * scale);
            Vector3 look = LookFlat();
            if (!kitPlaced || TooFar(kitCentre, 3f))
            {
                kitCentre = waist;
                kitFacing = look;
                kitPlaced = true;
            }

            kitCentre = Vector3.Lerp(kitCentre, waist, 1f - Mathf.Exp(-4f * dt));
            float off = Vector3.Angle(kitFacing, look);
            if (off > 50f)
                kitTurning = true;
            if (kitTurning)
            {
                kitFacing = Vector3.Slerp(kitFacing, look, 1f - Mathf.Exp(-3f * dt)).normalized;
                kitTurning = off > 5f;
            }

            if (drumClips == null)
                drumClips = MakeDrumClips();

            HandMotion(out Vector3 leftMotion, out Vector3 rightMotion);
            Vector3 leftHand = GorillaTagger.Instance.leftHandTransform.position;
            Vector3 rightHand = GorillaTagger.Instance.rightHandTransform.position;
            int leftIn = -1, rightIn = -1;
            float radius = 0.13f * scale;

            for (int i = 0; i < drums.Length; i++)
            {
                Ensure(ref drums[i], PrimitiveType.Cylinder, DrumColors[i]);

                float angle = (-50f + i * 33.3f) * Mathf.Deg2Rad;
                Vector3 outward = kitFacing * Mathf.Cos(angle) + Vector3.Cross(Vector3.up, kitFacing) * Mathf.Sin(angle);
                Vector3 top = kitCentre + outward * (0.5f * scale) + Vector3.up * ((i == 0 ? 0.12f : i == 3 ? -0.1f : 0f) * scale);

                drums[i].Object.transform.position = top + Vector3.down * (0.03f * scale);
                drums[i].Object.transform.localScale = new Vector3(0.24f, 0.03f, 0.24f) * scale;

                float flash = Mathf.Clamp01(1f - (now - drumHitAt[i]) / 0.15f);
                SetColor(drums[i], Color.Lerp(DrumColors[i], Color.white, flash));

                if (OnDrum(leftHand, top, radius, scale))
                    leftIn = i;
                if (OnDrum(rightHand, top, radius, scale))
                    rightIn = i;
            }

            HitDrum(0, leftIn, leftMotion, leftHand, now);
            HitDrum(1, rightIn, rightMotion, rightHand, now);
        }

        private static bool OnDrum(Vector3 hand, Vector3 top, float radius, float scale)
        {
            Vector3 flat = hand - top;
            float height = flat.y;
            flat.y = 0f;
            return flat.sqrMagnitude < radius * radius && height > -0.06f * scale && height < 0.08f * scale;
        }

        // A hit is a hand coming down onto a drum, played as loud as it came down hard.
        private static void HitDrum(int hand, int drum, Vector3 motion, Vector3 at, float now)
        {
            bool entered = drum >= 0 && drum != drumHandIn[hand];
            drumHandIn[hand] = drum;

            float down = -motion.y / Scale;
            if (!entered || down < 1f)
                return;

            float strength = Mathf.Clamp01(down / 5f);
            drumHitAt[drum] = now;
            PlaySound(drumClips[drum], 1f, 0.3f + strength * 0.6f);
            Burst(at, 5 + (int)(strength * 10f), 1f, falling: true, direction: Vector3.up);
            Vibrate(hand == 0, 0.3f + strength * 0.7f);
        }

        /// <summary>The four drum sounds, made once from sine waves and noise.</summary>
        private static AudioClip[] MakeDrumClips()
        {
            const int rate = 44100;
            System.Random noise = new System.Random(7);
            float Noise() => (float)noise.NextDouble() * 2f - 1f;

            AudioClip Make(string name, float seconds, Func<float, float> sample)
            {
                float[] samples = new float[(int)(rate * seconds)];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = Mathf.Clamp(sample((float)i / rate), -1f, 1f);

                AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
                clip.SetData(samples, 0);
                return clip;
            }

            // Hi-hat: the difference between neighbouring noise samples, which keeps only the hiss.
            float lastNoise = 0f;
            AudioClip hat = Make("Nova_HiHat", 0.1f, t =>
            {
                float n = Noise();
                float hiss = n - lastNoise;
                lastNoise = n;
                return hiss * 0.35f * Mathf.Exp(-t * 55f);
            });

            AudioClip snare = Make("Nova_Snare", 0.25f, t =>
                Noise() * 0.6f * Mathf.Exp(-t * 18f) + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.45f * Mathf.Exp(-t * 25f));

            // Toms and kicks fall in pitch as they ring, so their phase is summed as they go.
            float tomPhase = 0f;
            AudioClip tom = Make("Nova_Tom", 0.4f, t =>
            {
                tomPhase += 2f * Mathf.PI * (110f + 70f * Mathf.Exp(-t * 12f)) / rate;
                return Mathf.Sin(tomPhase) * 0.8f * Mathf.Exp(-t * 8f);
            });

            float kickPhase = 0f;
            AudioClip kick = Make("Nova_Kick", 0.45f, t =>
            {
                kickPhase += 2f * Mathf.PI * (48f + 120f * Mathf.Exp(-t * 30f)) / rate;
                return Mathf.Sin(kickPhase) * Mathf.Exp(-t * 7f);
            });

            return new[] { hat, snare, tom, kick };
        }

        public static void DisableDrumKit()
        {
            for (int i = 0; i < drums.Length; i++)
            {
                Discard(drums[i]);
                drums[i] = null;
            }

            kitPlaced = kitTurning = false;
            drumHandIn[0] = drumHandIn[1] = -1;
        }

        // ── Toys ────────────────────────────────────────────────────────────────

        private const int MaxBlobs = 6, MaxSplats = 40;
        private static readonly Piece[] blobs = new Piece[MaxBlobs];
        private static readonly List<Piece> splats = new List<Piece>();
        private static int nextBlob, nextSplat;
        private static bool splatHeld;

        /// <summary>Press B to throw paint that splats on walls and floors. Only you see it.</summary>
        public static void PaintSplats()
        {
            float scale = Scale;
            float dt = Time.deltaTime;

            if (Pressed(rightSecondary, ref splatHeld))
            {
                var hand = ControllerUtilities.GetTrueRightHand();
                Color color = MyColor;

                Piece blob = Ensure(ref blobs[nextBlob], PrimitiveType.Sphere, color);
                nextBlob = (nextBlob + 1) % MaxBlobs;

                blob.Born = Time.time;
                blob.Velocity = hand.forward * (9f * scale) + Extras.Body.linearVelocity;
                blob.Object.SetActive(true);
                blob.Object.transform.position = hand.position + hand.forward * (0.12f * scale);
                blob.Object.transform.localScale = Vector3.one * (0.06f * scale);
                SetColor(blob, color);
            }

            int layers = GTPlayer.Instance.locomotionEnabledLayers;
            foreach (Piece blob in blobs)
            {
                if (blob == null || !blob.Alive || !blob.Object.activeSelf)
                    continue;

                if (Time.time - blob.Born > 4f)
                {
                    blob.Object.SetActive(false);
                    continue;
                }

                Transform body = blob.Object.transform;
                blob.Velocity += Physics.gravity * (dt * scale);
                Vector3 move = blob.Velocity * dt;
                float distance = move.magnitude;

                if (distance > 0f && Physics.SphereCast(body.position, 0.03f * scale, move / distance, out RaycastHit hit, distance, layers))
                {
                    blob.Object.SetActive(false);
                    Splat(hit.point, hit.normal, blob.Renderer.sharedMaterial.color);
                }
                else
                    body.position += move;
            }
        }

        // The oldest splat is reused once there are forty, so painting all day costs nothing more.
        private static void Splat(Vector3 at, Vector3 normal, Color color)
        {
            float scale = Scale;
            Piece splat;
            if (splats.Count < MaxSplats)
            {
                splat = MakePiece(PrimitiveType.Cylinder, color, Vector3.one);
                splats.Add(splat);
            }
            else
            {
                if (!splats[nextSplat].Alive)
                    splats[nextSplat] = MakePiece(PrimitiveType.Cylinder, color, Vector3.one);
                splat = splats[nextSplat];
                nextSplat = (nextSplat + 1) % MaxSplats;
            }

            // Each splat sits a hair further out than the last, so overlapping ones don't flicker.
            float lift = (0.003f + (Time.frameCount % 20) * 0.0002f) * scale;
            float size = Random.Range(0.18f, 0.32f) * scale;

            splat.Object.transform.SetPositionAndRotation(at + normal * lift, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            splat.Object.transform.localScale = new Vector3(size, 0.002f * scale, size * Random.Range(0.7f, 1.3f));
            SetAlpha(splat, color, 0.9f);

            Burst(at, 12, 1.5f, falling: true, direction: normal);
        }

        public static void DisablePaintSplats()
        {
            for (int i = 0; i < blobs.Length; i++)
            {
                Discard(blobs[i]);
                blobs[i] = null;
            }

            ClearPieces(splats);
            nextBlob = nextSplat = 0;
        }

        private static Piece dragonBody, dragonHead, dragonSnout, dragonLeftWing, dragonRightWing;
        private static readonly Piece[] dragonTail = new Piece[4];
        private static readonly Vector3[] dragonTailAt = new Vector3[4];
        private static Vector3 dragonPosition, dragonFacing = Vector3.forward;
        private static bool dragonPlaced;
        private static float dragonNextBreath;

        /// <summary>A little dragon flies beside your head, flapping its wings and now and then breathing sparks.</summary>
        public static void PetDragon()
        {
            float scale = Scale;
            float now = Time.time;
            float dt = Time.deltaTime;
            Color color = MyColor;
            Color wing = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);

            Ensure(ref dragonBody, PrimitiveType.Capsule, color);
            Ensure(ref dragonHead, PrimitiveType.Sphere, color);
            Ensure(ref dragonSnout, PrimitiveType.Cube, color);
            Ensure(ref dragonLeftWing, PrimitiveType.Cube, wing);
            Ensure(ref dragonRightWing, PrimitiveType.Cube, wing);
            for (int i = 0; i < dragonTail.Length; i++)
                Ensure(ref dragonTail[i], PrimitiveType.Sphere, color);

            Vector3 look = LookFlat();
            Vector3 perch = Head.position + Vector3.Cross(Vector3.up, look) * (0.55f * scale) + Vector3.up * ((0.3f + Mathf.Sin(now * 1.7f) * 0.06f) * scale);

            if (!dragonPlaced || TooFar(dragonPosition, 15f))
            {
                dragonPosition = perch;
                dragonPlaced = true;
                for (int i = 0; i < dragonTailAt.Length; i++)
                    dragonTailAt[i] = perch;
            }

            Vector3 before = dragonPosition;
            dragonPosition = Vector3.Lerp(dragonPosition, perch, 1f - Mathf.Exp(-3f * dt));

            // It faces where it flies, and the way you look once it has caught up.
            Vector3 flying = dragonPosition - before;
            flying.y = 0f;
            Vector3 wanted = dt > 0f && flying.sqrMagnitude > 0.09f * scale * scale * dt * dt ? flying.normalized : look;
            dragonFacing = Vector3.Slerp(dragonFacing, wanted, 1f - Mathf.Exp(-4f * dt)).normalized;
            Quaternion turn = Quaternion.LookRotation(dragonFacing);

            dragonBody.Object.transform.SetPositionAndRotation(dragonPosition, turn * Quaternion.Euler(90f, 0f, 0f));
            dragonBody.Object.transform.localScale = new Vector3(0.1f, 0.12f, 0.1f) * scale;
            dragonHead.Object.transform.position = dragonPosition + turn * new Vector3(0f, 0.06f, 0.16f) * scale;
            dragonHead.Object.transform.localScale = Vector3.one * (0.09f * scale);
            dragonSnout.Object.transform.SetPositionAndRotation(dragonPosition + turn * new Vector3(0f, 0.05f, 0.22f) * scale, turn);
            dragonSnout.Object.transform.localScale = new Vector3(0.05f, 0.04f, 0.07f) * scale;

            float flap = Mathf.Sin(now * 9f) * 35f;
            PlaceDragonWing(dragonLeftWing, turn, -1f, flap, scale);
            PlaceDragonWing(dragonRightWing, turn, 1f, flap, scale);

            // The tail trails behind like a rope, each piece a set distance from the one before.
            Vector3 previous = dragonPosition - dragonFacing * (0.13f * scale);
            float spacing = 0.06f * scale;
            for (int i = 0; i < dragonTail.Length; i++)
            {
                Vector3 offset = dragonTailAt[i] - previous;
                dragonTailAt[i] = previous + (offset.sqrMagnitude > 0.000001f ? offset.normalized : -dragonFacing) * spacing;
                dragonTail[i].Object.transform.position = dragonTailAt[i];
                dragonTail[i].Object.transform.localScale = Vector3.one * ((0.07f - i * 0.01f) * scale);
                previous = dragonTailAt[i];
            }

            SetColor(dragonBody, color);
            SetColor(dragonHead, color);
            SetColor(dragonSnout, color);
            SetColor(dragonLeftWing, wing);
            SetColor(dragonRightWing, wing);
            foreach (Piece piece in dragonTail)
                SetColor(piece, color);

            if (now >= dragonNextBreath)
            {
                dragonNextBreath = now + Random.Range(5f, 9f);
                Burst(dragonSnout.Object.transform.position, 25, 2.5f, falling: false, direction: dragonFacing);
            }
        }

        private static void PlaceDragonWing(Piece piece, Quaternion turn, float side, float flap, float scale)
        {
            Quaternion swing = turn * Quaternion.Euler(0f, 0f, side * flap);
            Vector3 hinge = dragonPosition + turn * new Vector3(side * 0.05f, 0.04f, 0f) * scale;
            piece.Object.transform.SetPositionAndRotation(hinge + swing * new Vector3(side * 0.1f, 0f, 0f) * scale, swing);
            piece.Object.transform.localScale = new Vector3(0.2f, 0.01f, 0.12f) * scale;
        }

        public static void DisablePetDragon()
        {
            Discard(dragonBody);
            Discard(dragonHead);
            Discard(dragonSnout);
            Discard(dragonLeftWing);
            Discard(dragonRightWing);
            dragonBody = dragonHead = dragonSnout = dragonLeftWing = dragonRightWing = null;
            for (int i = 0; i < dragonTail.Length; i++)
            {
                Discard(dragonTail[i]);
                dragonTail[i] = null;
            }

            dragonPlaced = false;
        }

        private static readonly Piece[] snowman = new Piece[12];
        private static Vector3 snowmanAt, snowmanFacing;
        private static float snowmanBuilt = -1f, snowmanLayoutScale;

        /// <summary>Builds a snowman on the ground in front of you; turn it off and on to build another.</summary>
        public static void Snowman()
        {
            float scale = Scale;
            float now = Time.time;

            if (snowmanBuilt < 0f)
            {
                snowmanAt = GroundInFront(1.5f);
                snowmanFacing = -LookFlat();
                snowmanBuilt = now;
                snowmanLayoutScale = 0f;
            }

            Color snow = new Color(0.95f, 0.97f, 1f), coal = new Color(0.06f, 0.06f, 0.06f);
            Color carrot = new Color(1f, 0.5f, 0.1f), stick = new Color(0.4f, 0.25f, 0.12f);
            bool rebuilt = false;
            for (int i = 0; i < snowman.Length; i++)
                if (snowman[i] == null || !snowman[i].Alive)
                {
                    Ensure(ref snowman[i], i >= 9 ? PrimitiveType.Cylinder : PrimitiveType.Sphere,
                        i < 3 ? snow : i == 9 ? carrot : i >= 10 ? stick : coal);
                    rebuilt = true;
                }

            // It rolls together over a couple of seconds, then stands still and costs nothing.
            // Every part is full size once grown passes 1.3 (the last starts at 0.9 and takes a third).
            const float done = 1.3f;
            float grown = (now - snowmanBuilt) / 1.5f;
            if (grown > done && !rebuilt && Mathf.Approximately(snowmanLayoutScale, scale))
                return;
            snowmanLayoutScale = grown > done ? scale : 0f;

            float Grow(float from) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((grown - from) * 3f));
            Quaternion turn = Quaternion.LookRotation(snowmanFacing);
            Vector3 Spot(float x, float y, float z) => snowmanAt + turn * new Vector3(x, y, z) * scale;

            void Place(int i, Vector3 at, float size, float grow, Quaternion? rotation = null, Vector3? shape = null)
            {
                snowman[i].Object.SetActive(grow > 0f);
                snowman[i].Object.transform.SetPositionAndRotation(at, rotation ?? turn);
                snowman[i].Object.transform.localScale = (shape ?? Vector3.one) * (size * scale * grow);
            }

            Place(0, Spot(0f, 0.28f, 0f), 0.56f, Grow(0f));
            Place(1, Spot(0f, 0.7f, 0f), 0.4f, Grow(0.3f));
            Place(2, Spot(0f, 1.02f, 0f), 0.28f, Grow(0.6f));

            float details = Grow(0.9f);
            Place(3, Spot(-0.05f, 1.06f, 0.12f), 0.035f, details);
            Place(4, Spot(0.05f, 1.06f, 0.12f), 0.035f, details);
            for (int b = 0; b < 4; b++)
                Place(5 + b, Spot(0f, 0.62f + b * 0.08f - (b == 3 ? 0.5f : 0f), b == 3 ? 0.27f : 0.19f), 0.035f, details);
            Place(9, Spot(0f, 1.01f, 0.18f), 1f, details, turn * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.025f, 0.06f, 0.025f));
            Place(10, Spot(-0.32f, 0.78f, 0f), 1f, details, turn * Quaternion.Euler(0f, 0f, 60f), new Vector3(0.015f, 0.17f, 0.015f));
            Place(11, Spot(0.32f, 0.78f, 0f), 1f, details, turn * Quaternion.Euler(0f, 0f, -60f), new Vector3(0.015f, 0.17f, 0.015f));

            if (grown > done && grown - Time.deltaTime / 1.5f <= done)
                Burst(Spot(0f, 1.1f, 0f), 30, 1.5f, falling: true);
        }

        public static void DisableSnowman()
        {
            for (int i = 0; i < snowman.Length; i++)
            {
                Discard(snowman[i]);
                snowman[i] = null;
            }

            snowmanBuilt = -1f;
        }

        // ── Sky ─────────────────────────────────────────────────────────────────

        private const int ArcBands = 7, ArcPoints = 33;
        private static readonly LineRenderer[] arcBands = new LineRenderer[ArcBands];
        private static readonly Vector3[] arcLine = new Vector3[ArcPoints];
        private static readonly Color[] ArcColors =
        {
            Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green, new Color(0.2f, 0.5f, 1f), new Color(0.3f, 0.2f, 0.8f), new Color(0.6f, 0.2f, 0.9f)
        };
        private static Vector3 arcCentre;
        private static bool arcPlaced;

        /// <summary>A rainbow arches over the land in front of you. Only you see it.</summary>
        public static void RainbowArc()
        {
            // The sky doesn't grow or shrink with you, so neither does how far away it counts as left behind.
            bool layout = !arcPlaced || (arcCentre - Extras.BodyPosition).sqrMagnitude > 90f * 90f;
            for (int i = 0; i < ArcBands; i++)
                if (arcBands[i] == null)
                {
                    arcBands[i] = MakeLine("Nova_RainbowArc", 0.6f);
                    arcBands[i].positionCount = ArcPoints;

                    // The ends fade into the ground instead of stopping sharply.
                    Gradient fade = new Gradient();
                    fade.SetKeys(
                        new[] { new GradientColorKey(ArcColors[i], 0f), new GradientColorKey(ArcColors[i], 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.15f), new GradientAlphaKey(0.45f, 0.85f), new GradientAlphaKey(0f, 1f) });
                    arcBands[i].colorGradient = fade;
                    layout = true;
                }

            // The rainbow stands still, so it is only drawn when it is put down.
            if (!layout)
                return;

            Vector3 look = LookFlat();
            Vector3 across = Vector3.Cross(Vector3.up, look);
            arcCentre = Head.position + look * 45f + Vector3.down * 4f;
            arcPlaced = true;

            for (int band = 0; band < ArcBands; band++)
            {
                float radius = 30f - band * 0.6f;
                for (int p = 0; p < ArcPoints; p++)
                {
                    float angle = p * Mathf.PI / (ArcPoints - 1);
                    arcLine[p] = arcCentre + (across * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * radius;
                }

                arcBands[band].SetPositions(arcLine);
            }
        }

        public static void DisableRainbowArc()
        {
            for (int i = 0; i < arcBands.Length; i++)
                DestroyLine(ref arcBands[i]);
            arcPlaced = false;
        }

        private const int AuroraRibbons = 3, AuroraPoints = 40;
        private static readonly LineRenderer[] aurora = new LineRenderer[AuroraRibbons];
        private static readonly Vector3[] auroraLine = new Vector3[AuroraPoints];

        /// <summary>Green and violet ribbons wave slowly across the sky above you. Only you see them.</summary>
        public static void NorthernLights()
        {
            for (int i = 0; i < AuroraRibbons; i++)
                if (aurora[i] == null)
                {
                    aurora[i] = MakeLine("Nova_NorthernLights", 5f);
                    aurora[i].positionCount = AuroraPoints;

                    Gradient glow = new Gradient();
                    glow.SetKeys(
                        new[] { new GradientColorKey(new Color(0.2f, 1f, 0.5f), 0f), new GradientColorKey(new Color(0.2f, 0.9f, 0.9f), 0.5f), new GradientColorKey(new Color(0.6f, 0.3f, 1f), 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.2f), new GradientAlphaKey(0.35f, 0.8f), new GradientAlphaKey(0f, 1f) });
                    aurora[i].colorGradient = glow;
                }

            float now = Time.time;
            Vector3 above = Head.position + Vector3.up * 35f;
            for (int ribbon = 0; ribbon < AuroraRibbons; ribbon++)
            {
                float offset = ribbon * 2.1f;
                for (int p = 0; p < AuroraPoints; p++)
                {
                    float along = (float)p / (AuroraPoints - 1) - 0.5f;
                    float x = along * 120f;
                    float z = -20f + ribbon * 18f + Mathf.Sin(along * 6f + now * 0.3f + offset) * 8f + Mathf.Sin(along * 13f - now * 0.5f) * 3f;
                    float y = Mathf.Sin(along * 4f + now * 0.4f + offset) * 4f;
                    auroraLine[p] = above + new Vector3(x, y, z);
                }

                aurora[ribbon].SetPositions(auroraLine);
                aurora[ribbon].startWidth = aurora[ribbon].endWidth = 4f + Mathf.Sin(now * 0.7f + offset) * 1.5f;
            }
        }

        public static void DisableNorthernLights()
        {
            for (int i = 0; i < aurora.Length; i++)
                DestroyLine(ref aurora[i]);
        }

        private static Piece holeCore, holeGlow;
        private static ParticleSystem holeDisk;
        private static Vector3 holeAt;

        /// <summary>A black hole hangs in front of you with glowing matter swirling into it. Only you see it.</summary>
        public static void BlackHole()
        {
            float scale = Scale;
            if (holeDisk == null || TooFar(holeAt, 30f))
            {
                DestroyParticles(ref holeDisk);
                holeAt = Head.position + LookFlat() * (4f * scale) + Vector3.up * (0.5f * scale);

                holeDisk = MakeParticles("Nova_BlackHole", null, system =>
                {
                    var main = system.main;
                    main.loop = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.startLifetime = 2.6f;
                    main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(0.7f, 0.3f, 1f));
                    main.maxParticles = 600;

                    var emission = system.emission;
                    emission.rateOverTime = 220f;

                    // Matter starts on a ring, circles the middle and falls in.
                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 1.4f;
                    shape.radiusThickness = 0.25f;

                    var velocity = system.velocityOverLifetime;
                    velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.Local;
                    velocity.x = velocity.y = velocity.z = new ParticleSystem.MinMaxCurve(0f);
                    velocity.orbitalZ = new ParticleSystem.MinMaxCurve(2.4f);
                    velocity.radial = new ParticleSystem.MinMaxCurve(-0.5f);

                    var size = system.sizeOverLifetime;
                    size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
                });

                // The disk is tipped a little toward you, so you see it swirl.
                Vector3 toward = -LookFlat();
                holeDisk.transform.SetPositionAndRotation(holeAt, Quaternion.LookRotation(Vector3.Slerp(Vector3.up, toward, 0.25f)));
            }

            if (holeCore == null || !holeCore.Alive)
            {
                // The core writes depth and draws first, so the swirl behind it is hidden.
                Ensure(ref holeCore, PrimitiveType.Sphere, Color.black);
                holeCore.Renderer.sharedMaterial.SetFloat("_ZWrite", 1f);
                holeCore.Renderer.sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1;
            }

            Ensure(ref holeGlow, PrimitiveType.Sphere, new Color(0.6f, 0.3f, 1f, 0.25f));

            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.05f;
            holeCore.Object.transform.position = holeAt;
            holeCore.Object.transform.localScale = Vector3.one * (0.45f * scale);
            holeGlow.Object.transform.position = holeAt;
            holeGlow.Object.transform.localScale = Vector3.one * (0.62f * scale * pulse);
            holeDisk.transform.localScale = Vector3.one * scale;
        }

        public static void DisableBlackHole()
        {
            Discard(holeCore);
            Discard(holeGlow);
            holeCore = holeGlow = null;
            DestroyParticles(ref holeDisk);
        }

        // ── Tools ───────────────────────────────────────────────────────────────

        private static TextMeshPro statsText;
        private static Vector3 statsLast;
        private static int statsFrame = -2, statsJumps;
        private static float statsDistance, statsTopSpeed, statsNextText;
        private static bool statsWasGrounded = true;

        /// <summary>How far you have gone, how often you jumped, your top speed and your play time.</summary>
        /// <remarks>Counts while it is on, and keeps the count when turned off and on again.</remarks>
        public static void WristStats()
        {
            if (statsText == null)
            {
                statsText = MakeWristText("Nova_WristStats", new Color(0.7f, 1f, 0.6f));
                statsNextText = 0f;
            }

            Vector3 body = Extras.BodyPosition;
            Vector3 velocity = Extras.Body.linearVelocity;
            bool grounded = Extras.Grounded();
            float dt = Time.deltaTime;

            // The first frame after a gap, and teleports, are not walking.
            if (statsFrame == Time.frameCount - 1 && dt > 0f)
            {
                float moved = (body - statsLast).magnitude;
                if (moved / dt < 40f)
                    statsDistance += moved;

                if (statsWasGrounded && !grounded && velocity.y > 1.5f * Scale)
                    statsJumps++;
            }

            float speed = velocity.magnitude;
            if (speed < 60f)
                statsTopSpeed = Mathf.Max(statsTopSpeed, speed);

            statsLast = body;
            statsWasGrounded = grounded;
            statsFrame = Time.frameCount;

            if (Time.time >= statsNextText)
            {
                statsNextText = Time.time + 0.25f;
                TimeSpan played = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
                string distance = statsDistance >= 1000f ? $"{statsDistance / 1000f:0.00} km" : $"{statsDistance:0} m";
                statsText.SafeSetText($"{distance}  ·  {statsJumps} jumps\n<size=70%>Top {statsTopSpeed:0.0} m/s  ·  Played {(int)played.TotalHours}:{played.Minutes:00}:{played.Seconds:00}</size>");
            }

            PlaceOnWrist(statsText, 0.44f);
        }

        public static void DisableWristStats() => DestroyText(ref statsText);
    }
}
