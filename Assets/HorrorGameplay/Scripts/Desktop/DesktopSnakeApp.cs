// Controla a Cobrinha, suas entradas e o estado da partida.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorGame.Desktop
{
    public static class SnakeText
    {
        public const string Title = "Cobrinha", NewGame = "Novo jogo", Play = "Jogar", Pause = "Pausar", Resume = "Continuar";
        public const string Controls = "Setas ou WASD: mover · Espaço: pausar · R: novo jogo";
        public const string Ready = "Pegue as maçãs. Use as setas ou clique em Jogar.";
        public const string Running = "Cuidado com as paredes e com a própria cauda.";
        public const string Paused = "Partida pausada. Clique em Continuar.";
        public const string Lost = "Fim de jogo. Vamos tentar de novo?", Won = "Você completou o tabuleiro!";
        public const string Points = "Pontos: ";
    }

    // Session-only minigame: closing the window does not change campaign or checkpoint data.
    public sealed class DesktopSnakeApp : DesktopApp
    {
        const float StepSeconds = .14f;
        SnakeBoard board;
        Image[,] tiles;
        TMP_Text score, status, playLabel;
        float elapsed;
        bool started, paused;
        public SnakeBoard Board => board;
        public bool Paused => paused;
        public bool Started => started;

        public override void Build() { NewGame(); }
        public override void OnResize() { BuildLayout(); Draw(); }

        void NewGame()
        {
            board = new SnakeBoard(Environment.TickCount);
            elapsed = 0; started = paused = false;
            BuildLayout(); Draw();
        }

        void BuildLayout()
        {
            DesktopUI.Clear(Body);
            float width = Body.rect.width, height = Body.rect.height;
            score = DesktopUI.Label(Body, "", 16, 12, 180, 30, 18);
            var play = DesktopUI.Button(Body, SnakeText.Play, width - 250, 8, 110, 32, TogglePause);
            playLabel = play.GetComponentInChildren<TMP_Text>();
            DesktopUI.Button(Body, SnakeText.NewGame, width - 132, 8, 116, 32, NewGame);
            int size = Mathf.Max(8, Mathf.FloorToInt(Mathf.Min((width - 32) / board.Width, (height - 135) / board.Height)));
            int fieldWidth = board.Width * size, fieldHeight = board.Height * size;
            var field = DesktopUI.Panel(Body, "Tabuleiro da cobrinha", Mathf.Floor((width - fieldWidth) / 2), 54, fieldWidth, fieldHeight);
            var background = field.GetComponent<Image>(); background.sprite = null; background.color = new Color32(21, 39, 25, 255);
            tiles = new Image[board.Width, board.Height];
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    var tile = DesktopUI.Rect(field, "Casa", x * size + 1, y * size + 1, size - 2, size - 2).gameObject.AddComponent<Image>();
                    tile.raycastTarget = false; tiles[x, y] = tile;
                }
            status = DesktopUI.Label(Body, "", 16, height - 65, width - 32, 30, 14);
            DesktopUI.Label(Body, SnakeText.Controls, 16, height - 31, width - 32, 25, 12);
        }

        void TogglePause()
        {
            if (board.HasLost || board.HasWon) NewGame();
            if (!started) { started = true; paused = false; }
            else paused = !paused;
            elapsed = 0; RefreshText();
        }

        public override void Tick(float deltaTime)
        {
            if (Window.Minimized || !Window.IsFocused)
            {
                if (started && !paused) { paused = true; elapsed = 0; RefreshText(); }
                return;
            }
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.rKey.wasPressedThisFrame) { NewGame(); return; }
                if (keys.spaceKey.wasPressedThisFrame) TogglePause();
                if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) Turn(Vector2Int.up);
                else if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) Turn(Vector2Int.down);
                else if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) Turn(Vector2Int.left);
                else if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) Turn(Vector2Int.right);
            }
            if (!started || paused || board.HasLost || board.HasWon) return;
            elapsed += Mathf.Min(deltaTime, .25f);
            while (elapsed >= StepSeconds && !board.HasLost && !board.HasWon)
            {
                elapsed -= StepSeconds;
                int points = board.Score;
                board.Step();
                if (board.Score != points) Context.PlaySound("click");
                else if (board.HasLost) Context.PlaySound("error");
                Draw();
            }
        }

        void Turn(Vector2Int direction)
        {
            // Board coordinates grow downward, unlike the arrow vectors.
            direction.y = -direction.y;
            if (board.Turn(direction) || direction == board.Direction)
            {
                if (!started) { started = true; paused = false; RefreshText(); }
            }
        }

        void Draw()
        {
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                    tiles[x, y].color = ((x + y) & 1) == 0 ? new Color32(28, 48, 31, 255) : new Color32(31, 52, 34, 255);
            if (!board.HasWon) tiles[board.Food.x, board.Food.y].color = new Color32(231, 75, 52, 255);
            for (int i = board.Segments.Count - 1; i >= 0; i--)
            {
                var p = board.Segments[i];
                tiles[p.x, p.y].color = i == 0 ? new Color32(199, 244, 101, 255) : new Color32(102, 183, 65, 255);
            }
            RefreshText();
        }

        void RefreshText()
        {
            score.text = SnakeText.Points + board.Score;
            status.text = board.HasLost ? SnakeText.Lost : board.HasWon ? SnakeText.Won : !started ? SnakeText.Ready : paused ? SnakeText.Paused : SnakeText.Running;
            playLabel.text = !started || board.HasLost || board.HasWon ? SnakeText.Play : paused ? SnakeText.Resume : SnakeText.Pause;
        }
    }

    public sealed class SnakeBoard
    {
        readonly List<Vector2Int> segments = new List<Vector2Int>();
        readonly System.Random random;
        Vector2Int queuedDirection;
        bool turnQueued;
        public IReadOnlyList<Vector2Int> Segments { get; }
        public int Width { get; }
        public int Height { get; }
        public int Score => segments.Count - 3;
        public Vector2Int Direction { get; private set; } = Vector2Int.right;
        public Vector2Int Food { get; private set; }
        public bool HasLost { get; private set; }
        public bool HasWon => segments.Count == Width * Height;

        public SnakeBoard(int seed, int width = 20, int height = 14)
        {
            if (width < 6 || height < 4) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Height = height; random = new System.Random(seed);
            for (int i = 0; i < 3; i++) segments.Add(new Vector2Int(width / 2 - i, height / 2));
            Segments = segments.AsReadOnly(); queuedDirection = Direction; SpawnFood();
        }

        public bool Turn(Vector2Int direction)
        {
            if (HasLost || HasWon || turnQueued || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1
                || direction == -Direction || direction == Direction) return false;
            queuedDirection = direction; turnQueued = true; return true;
        }

        public bool Step()
        {
            if (HasLost || HasWon) return false;
            Direction = queuedDirection; turnQueued = false;
            Vector2Int next = segments[0] + Direction;
            bool eating = next == Food;
            int occupied = segments.Count - (eating ? 0 : 1);
            int hit = segments.IndexOf(next);
            if (next.x < 0 || next.x >= Width || next.y < 0 || next.y >= Height
                || hit >= 0 && hit < occupied) { HasLost = true; return false; }
            segments.Insert(0, next);
            if (!eating) segments.RemoveAt(segments.Count - 1);
            else if (!HasWon) SpawnFood();
            return true;
        }

        void SpawnFood()
        {
            int free = random.Next(Width * Height - segments.Count);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!segments.Contains(cell) && free-- == 0) { Food = cell; return; }
                }
        }
    }
}
