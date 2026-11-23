// Mantém o relógio interno usado nas cenas e interfaces.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    [Serializable]
    public sealed class GameClockState
    {
        public int version = 1, minute = 1512, anomalies;
        public float elapsed;
        public bool locked;
        public bool Valid => version == 1 && minute >= 0 && minute <= 2880 && elapsed >= 0 && elapsed < 25 && !float.IsNaN(elapsed) && anomalies >= 0 && anomalies <= 3;
    }

    public sealed class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }
        public GameClockState State { get; private set; } = new GameClockState();

        float flash;
        bool backwards;
        public static readonly DateTime NightStart = new DateTime(2026, 10, 26);
        public DateTime Now => NightStart.AddMinutes(State.minute);
        public string TimeText => flash > 0 ? (backwards ? Now.AddMinutes(-1).ToString("HH:mm") : "03:07") : Now.ToString("HH:mm");
        public string DateText => Now.ToString("dd/MM/yyyy");

        public static GameClock Ensure(GameSession game)
        {
            if (!game)
                return Instance;
            var clock = game.GetComponent<GameClock>();
            if (!clock)
                clock = game.gameObject.AddComponent<GameClock>();
            return clock;
        }

        void Awake()
        {
            Instance = this;
            Defaults();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Defaults()
        {
            State = new GameClockState();
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "RoadJourney")
                State.minute = 1590;
            else if (scene == "SchoolCEEP")
                LockAtSchool();
        }

        void Update()
        {
            var game = GameSession.Instance;
            if (!game || game.IsRestoringCheckpoint || game.State == GameState.Paused || game.State == GameState.Tutorial || game.State == GameState.StoryScreen)
                return;
            EnterScene();
            if (flash > 0)
                flash -= Time.deltaTime;
            if (State.locked)
                return;
            State.elapsed += Time.deltaTime * (SceneManager.GetActiveScene().name == "RoadJourney" ? 25 : 1);
            while (State.elapsed >= 25)
            {
                State.elapsed -= 25;
                State.minute = Math.Min(State.minute + 1, SceneManager.GetActiveScene().name == "RoadJourney" ? 1620 : 1589);
            }
        }

        public GameClockState Capture() => JsonUtility.FromJson<GameClockState>(JsonUtility.ToJson(State));
        public void Restore(GameClockState data)
        {
            if (data == null)
            {
                Defaults();
                return;
            }

            if (!data.Valid)
                throw new ArgumentException("Hora inválida.");
            State = JsonUtility.FromJson<GameClockState>(JsonUtility.ToJson(data));
            EnterScene();
        }

        public void EnterScene()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "SchoolCEEP")
                LockAtSchool();
            else if (scene == "RoadJourney")
            {
                State.minute = Math.Max(1590, State.minute);
            }
        }

        public void SetOfficeExit()
        {
            State.minute = 1590;
            State.elapsed = 0;
        }

        public void LockAtSchool()
        {
            State.minute = 1627;
            State.elapsed = 0;
            State.locked = true;
        }

        public void TriggerOfficeAnomaly(bool reverse)
        {
            int bit = reverse ? 2 : 1;
            if ((State.anomalies & bit) != 0 || SceneManager.GetActiveScene().name != "Chapter1Office")
                return;
            State.anomalies |= bit;
            backwards = reverse;
            flash = 1;
        }

        public static string MailDate(EmailMessage mail) => mail.id == "email.08" ? NightStart.AddDays(1).ToString("dd/MM/yyyy") : mail.date;
        public static string MailTime(EmailMessage mail) => mail.id == "email.08" ? "03:07" : mail.time;
    }
}
