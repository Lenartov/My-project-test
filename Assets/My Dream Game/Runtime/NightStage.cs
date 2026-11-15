using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyDreamGame
{
    public sealed class NightStage : MonoBehaviour
    {
        private enum State
        {
            Ready,
            Countdown,
            Driving,
            Paused,
            Finished,
            Retired
        }

        private const float SimulationStep = RallyRecording.StepSeconds;
        private const string BestTimeKey = "MyDreamGame.BlackRidge.Stage01.BestSeconds";
        [SerializeField] private string musicPath;

        private readonly List<Object> generatedAssets = new List<Object>();
        private readonly RallyCar car = new RallyCar();
        private readonly int[] splitTicks = new int[2];
        private readonly List<RallyRecording.Pose> recording = new List<RallyRecording.Pose>(2200);
        private RallyCourse course;
        private RallyView view;
        private RallyHud hud;
        private RallyAudio stageAudio;
        private RallyGhost ghost;
        private RallyRecording reference;
        private State state;
        private State pausedState;
        private float elapsed;
        private float accumulator;
        private float animationTime;
        private float countdown;
        private float refreshDelay;
        private float messageUntil;
        private string message;
        private int splitCount;
        private int simulationTick;
        private bool showGhost;

        public static NightStage Active { get; private set; }

        public void SelectMusic(string path)
        {
            musicPath = path;
            if (stageAudio != null)
            {
                stageAudio.SelectMusic(path);
            }
        }

        private void Start()
        {
            course = new RallyCourse();
            RallyScenery scenery = new RallyScenery(course, transform, generatedAssets);
            Color fog = scenery.Build();
            view = new RallyView(transform, scenery, fog, generatedAssets);
            ghost = new RallyGhost(transform, scenery, generatedAssets);
            hud = new RallyHud(transform);
            stageAudio = new RallyAudio(this, course, generatedAssets);
            reference = RallyRecording.Load(course);
            if (!string.IsNullOrEmpty(musicPath))
            {
                stageAudio.SelectMusic(musicPath);
            }

            car.Reset(course);
            view.Reset(car, course);
            Active = this;
            hud.ShowMenu("NIGHT RALLY  /  1.92 KM", "BLACK RIDGE",
                "Clear all three sectors in 02:25 with the car intact.\n" +
                "UP throttle   DOWN brake / hold for reverse\n" +
                "LEFT / RIGHT steer   ESC pause   R restart\n" +
                "G reference car   C co-driver",
                "UP   START STAGE");
            RefreshHud();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                if (state == State.Driving || state == State.Countdown)
                {
                    Pause();
                }

                return;
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                stageAudio.ToggleMusic();
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                showGhost = !showGhost;
                if (state == State.Driving)
                {
                    ShowMessage(reference == null ? "FINISH A RUN TO RECORD A REFERENCE" :
                        showGhost ? "REFERENCE CAR ON" : "REFERENCE CAR OFF", 1.8f);
                }
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                bool enabled = stageAudio.ToggleCoDriver();
                if (state == State.Driving)
                {
                    ShowMessage(enabled ? "CO-DRIVER ON" : "CO-DRIVER MUTED", 1.8f);
                }
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                StartCountdown();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (state == State.Driving || state == State.Countdown)
                {
                    Pause();
                }
                else if (state == State.Paused)
                {
                    Resume();
                }
            }
            else if (state == State.Ready && keyboard.upArrowKey.wasPressedThisFrame)
            {
                StartCountdown();
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.10f);
            bool throttle = keyboard.upArrowKey.isPressed;
            if (state != State.Paused)
            {
                animationTime += dt;
                if (state == State.Countdown)
                {
                    TickCountdown(dt);
                }
                else if (state == State.Driving)
                {
                    float brake = keyboard.downArrowKey.isPressed ? 1f : 0f;
                    float steer = (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                        (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                    Simulate(throttle ? 1f : 0f, brake, steer, dt);
                }

                bool driving = state == State.Driving;
                view.Update(car, course, animationTime, dt, driving);
                stageAudio.Update(car, driving, driving && throttle, dt);
                if (driving && showGhost && reference != null && elapsed <= reference.Time)
                {
                    ghost.Show(reference, course, elapsed, car.Position);
                }
                else
                {
                    ghost.Hide();
                }
            }

            refreshDelay -= dt;
            if (refreshDelay <= 0f)
            {
                RefreshHud();
                refreshDelay = 0.05f;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && (state == State.Driving || state == State.Countdown))
            {
                Pause();
            }
        }

        private void StartCountdown()
        {
            car.Reset(course);
            view.Reset(car, course);
            view.SetPaused(false);
            stageAudio.SetPaused(false);
            stageAudio.ResetCalls();
            ghost.Hide();
            recording.Clear();
            recording.Add(new RallyRecording.Pose(car));
            simulationTick = 0;
            elapsed = 0f;
            accumulator = 0f;
            countdown = 3f;
            splitCount = 0;
            message = null;
            messageUntil = 0f;
            state = State.Countdown;
            hud.HideMenu();
            hud.SetCountdown(3);
            stageAudio.PlayTick(false);
            RefreshHud();
        }

        private void TickCountdown(float dt)
        {
            int previous = Mathf.CeilToInt(countdown);
            countdown -= dt;
            int current = Mathf.CeilToInt(countdown);
            if (countdown <= 0f)
            {
                state = State.Driving;
                hud.SetCountdown(0);
                stageAudio.PlayTick(true);
                ShowMessage("GO", 1.05f);
            }
            else if (current != previous)
            {
                hud.SetCountdown(current);
                stageAudio.PlayTick(false);
            }
        }

        private void Simulate(float throttle, float brake, float steering, float dt)
        {
            accumulator += dt;
            float strongestImpact = 0f;
            while (accumulator >= SimulationStep && state == State.Driving)
            {
                accumulator -= SimulationStep;
                simulationTick++;
                elapsed = simulationTick * SimulationStep;
                car.Step(course, throttle, brake, steering, SimulationStep);
                if (simulationTick % RallyRecording.SampleStride == 0)
                {
                    recording.Add(new RallyRecording.Pose(car));
                }

                strongestImpact = Mathf.Max(strongestImpact, car.Impact);

                if (car.Damage >= 1f)
                {
                    Retire("The car is too damaged to continue.");
                }
                else
                {
                    RecordProgress();
                    if (state == State.Driving && elapsed >= RallyCourse.TimeLimit)
                    {
                        elapsed = RallyCourse.TimeLimit;
                        Retire("The stage closed before you reached the finish.");
                    }
                }
            }

            if (strongestImpact > 0f)
            {
                stageAudio.PlayImpact(strongestImpact);
                view.Impact(strongestImpact);
                if (strongestImpact > 2.5f && state == State.Driving)
                {
                    ShowMessage("GUARDRAIL IMPACT", 1.15f);
                }
            }
        }

        private void RecordProgress()
        {
            if (splitCount == 0 && car.Distance >= RallyCourse.FirstSplit)
            {
                RecordSplit();
            }
            else if (splitCount == 1 && car.Distance >= RallyCourse.SecondSplit)
            {
                RecordSplit();
            }
            else if (splitCount == splitTicks.Length && car.Distance >= course.Finish)
            {
                Finish();
            }
        }

        private void RecordSplit()
        {
            splitTicks[splitCount] = simulationTick;
            string split = SplitDisplay(splitCount, reference);
            splitCount++;
            ShowMessage("SPLIT  " + splitCount + "   " + split, 3f);
            stageAudio.PlayTick(true);
        }

        private void Finish()
        {
            state = State.Finished;
            stageAudio.ResetCalls();
            RallyRecording previous = reference;
            float legacyBest = PlayerPrefs.GetFloat(BestTimeKey, float.PositiveInfinity);
            float previousBest = Mathf.Min(legacyBest, previous != null ? previous.Time : float.PositiveInfinity);
            bool personalBest = elapsed < previousBest;
            string referenceStatus = string.Empty;
            if (previous == null || elapsed < previous.Time)
            {
                if (simulationTick % RallyRecording.SampleStride != 0)
                {
                    recording.Add(new RallyRecording.Pose(car));
                }

                reference = new RallyRecording(recording.ToArray(), splitTicks[0], splitTicks[1],
                    simulationTick, Mathf.RoundToInt(course.Finish));
                referenceStatus = reference.TrySave() ? "    REFERENCE SAVED" : "    SESSION REFERENCE";
            }

            string splitTimes = SplitDisplay(0, previous) + "   /   " + SplitDisplay(1, previous);
            string rank = elapsed <= 108f ? "GOLD" : elapsed <= 125f ? "SILVER" : "BRONZE";
            hud.ShowMenu("STAGE COMPLETE  /  " + rank, RallyHud.FormatTime(elapsed),
                "SPLITS    " + splitTimes + "\n" +
                "BEST       " + RallyHud.FormatTime(Mathf.Min(elapsed, previousBest)) + (personalBest ? "    NEW PERSONAL BEST" : "") + "\n" +
                "INTEGRITY    " + Mathf.RoundToInt((1f - car.Damage) * 100f) + "%" + referenceStatus,
                "R   RUN AGAIN");
            stageAudio.PlayTick(true);
        }

        private void Retire(string reason)
        {
            state = State.Retired;
            stageAudio.ResetCalls();
            hud.ShowMenu("STAGE FAILED", "RETIRED", reason + "\n" +
                "DISTANCE    " + Mathf.Max(0, Mathf.RoundToInt(car.Distance)) + " M  /  " + course.Finish + " M",
                "R   RUN AGAIN");
        }

        private void Pause()
        {
            pausedState = state;
            state = State.Paused;
            hud.SetCountdown(0);
            hud.ShowMenu("BLACK RIDGE", "PAUSED",
                "UP throttle   DOWN brake / hold for reverse\n" +
                "LEFT / RIGHT steer   R restart\n" +
                "G reference car   C co-driver", "ESC   RESUME");
            view.SetPaused(true);
            stageAudio.SetPaused(true);
        }

        private void Resume()
        {
            state = pausedState;
            hud.HideMenu();
            if (state == State.Countdown)
            {
                hud.SetCountdown(Mathf.CeilToInt(countdown));
            }

            view.SetPaused(false);
            stageAudio.SetPaused(false);
        }

        private void ShowMessage(string text, float duration)
        {
            message = text;
            messageUntil = elapsed + duration;
        }

        private string SplitDisplay(int index, RallyRecording comparison)
        {
            float time = splitTicks[index] * SimulationStep;
            string display = RallyHud.FormatTime(time);
            if (comparison != null)
            {
                display += "  (" + RallyHud.FormatDelta(time - comparison.SplitTime(index)) + ")";
            }

            return display;
        }

        private void RefreshHud()
        {
            RallyCourse.Turn? next = null;
            RallyCourse.Puddle? waterAhead = null;
            if (state == State.Driving)
            {
                foreach (RallyCourse.Turn turn in course.Turns)
                {
                    if (turn.End >= car.Distance)
                    {
                        if (turn.Start <= car.Distance + 132f)
                        {
                            next = turn;
                        }

                        break;
                    }
                }

                foreach (RallyCourse.Puddle puddle in course.Puddles)
                {
                    float remaining = puddle.Distance - puddle.HalfLength - car.Distance;
                    if (remaining >= 0f)
                    {
                        if (remaining <= Mathf.Clamp(car.Speed * 3.1f, 38f, 105f))
                        {
                            waterAhead = puddle;
                        }

                        break;
                    }
                }
            }

            string flash = state == State.Driving && elapsed < messageUntil ? message : string.Empty;
            float? delta = null;
            if (state == State.Driving && reference != null && car.Distance > 8f)
            {
                delta = elapsed - reference.TimeAtDistance(car.Distance);
            }

            hud.Update(car, elapsed, splitCount, next, waterAhead, delta, flash, stageAudio.MusicStatus);
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            stageAudio?.Dispose();
            foreach (Object asset in generatedAssets)
            {
                Destroy(asset);
            }
        }
    }
}
