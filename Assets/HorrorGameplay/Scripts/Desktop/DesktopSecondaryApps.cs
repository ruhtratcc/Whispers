// Implementa Bloco de Notas, Media Player e Campo Minado.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorGame.Desktop
{
    public sealed class DesktopNotepadApp : DesktopApp
    {
        public override void Build() { }

        public override void OnOpen(string argument)
        {
            DesktopUI.Clear(Body);
            Window.SetTitle("nao_ler.txt — Bloco de Notas");
            int visit = Mathf.Clamp(Context.State.visits, 1, 5);
            DesktopNote selected = null;
            foreach (var note in Context.Data.notes ?? Array.Empty<DesktopNote>())
                if (note && note.visit == visit) { selected = note; break; }

            DesktopUI.Label(Body, "Arquivo     Editar     Exibir", 16, 7, 720, 28, 15);
            var paragraphs = string.IsNullOrWhiteSpace(argument)
                ? selected ? selected.paragraphs : Array.Empty<EmailParagraph>()
                : new[] { new EmailParagraph { text = argument, child = true } };
            DesktopParagraphs.Draw(Body, paragraphs, 16, 44, 744, 346);
            DesktopUI.Label(Body, "Texto local • somente leitura", 16, 398, 720, 24, 13);
        }
    }

    public sealed class DesktopMediaApp : DesktopApp
    {
        readonly List<DesktopTrack> playlist = new List<DesktopTrack>();
        AudioSource source;
        RectTransform tracksArea;
        readonly Dictionary<string, Button> trackButtons = new Dictionary<string, Button>();
        TMP_Text trackTitle, status, time;
        Button playButton;
        Slider progress;
        DesktopTrack selected;
        bool playing;
        float position;

        public bool IsPlaying => playing;
        public float PositionSeconds => position;
        public float DurationSeconds => selected == null ? 0 : selected.clip
            ? selected.clip.length : Mathf.Max(1, selected.placeholderSeconds);
        public string SelectedTrackId => selected?.id;
        public int PlaylistCount => playlist.Count;

        public override void Build()
        {
            source = Body.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.volume = .8f;
            AudioManager.Instance?.Route(source,AudioChannel.Voice);
            BuildLayout();
        }

        void BuildLayout()
        {
            DesktopUI.Clear(Body);
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 446;
            float listWidth = Mathf.Clamp(width * .32f, 238, 290);
            float viewWidth = width - listWidth - 28;
            float viewHeight = Mathf.Max(180, height - 160);
            float controlsY = 38 + viewHeight;
            DesktopUI.Label(Body, "Arquivo     Exibir     Reproduzir     Ferramentas     Ajuda", 9, 3, width - 18, 23, 14);
            var playback = DesktopUI.Panel(Body, "Reprodução", 8, 29, viewWidth, viewHeight);
            var screen = DesktopUI.Panel(playback, "Visor de áudio", 3, 3, viewWidth - 6, viewHeight - 6);
            var screenImage = screen.GetComponent<Image>(); screenImage.sprite = null; screenImage.color = new Color(.025f, .045f, .075f);
            trackTitle = DesktopUI.Label(screen, "Selecione uma faixa", 18, 19, viewWidth - 42, 58, 21);
            trackTitle.color = new Color(.88f, .94f, 1); trackTitle.fontStyle = FontStyles.Bold;
            var subtitle = DesktopUI.Label(screen, "ARQUIVO DE ÁUDIO LOCAL", 19, 84, viewWidth - 44, 24, 12);
            subtitle.color = new Color(.46f, .64f, .83f);
            var app = Context.Data.FindApp("media");
            if (app != null && app.icon) DesktopUI.Image(screen, app.icon, viewWidth * .5f - 27, 119, 48, 48);
            status = DesktopUI.Label(screen, "Parado", 19, viewHeight - 94, viewWidth - 44, 79, 14);
            status.color = new Color(.73f, .82f, .92f);
            tracksArea = DesktopUI.Panel(Body, "ListaDeReproducao", viewWidth + 16, 29, listWidth, viewHeight);
            var listImage = tracksArea.GetComponent<Image>(); listImage.sprite = null; listImage.color = Color.white;
            var transport = DesktopUI.Panel(Body, "Controles de reprodução", 8, controlsY, width - 16, 81);
            progress = DesktopUI.Slider(transport, 12, 8, width - 175, 20, Seek);
            time = DesktopUI.Label(transport, "00:00 / 00:00", width - 151, 7, 127, 25, 14);
            playButton = DesktopUI.Button(transport, "Reproduzir", 10, 37, 114, 34, TogglePlay);
            DesktopUI.Button(transport, "Parar", 130, 37, 72, 34, Stop);
            DesktopUI.Button(transport, "Anterior", 208, 37, 84, 34, () => StepTrack(-1));
            DesktopUI.Button(transport, "Próxima", 298, 37, 84, 34, () => StepTrack(1));
            DesktopUI.Label(transport, "Volume", width - 244, 43, 64, 22, 13);
            var volume = DesktopUI.Slider(transport, width - 174, 43, 137, 20, value => source.volume = value);
            volume.SetValueWithoutNotify(source.volume);
            DesktopUI.Label(Body, "Arquivo local     |     Lista de reprodução", 10, height - 25, width - 20, 21, 12);
        }

        public override void OnResize()
        {
            BuildLayout();
            DrawPlaylist();
            UpdateDisplay();
        }

        public override void OnOpen(string argument)
        {
            playlist.Clear();
            foreach (var track in Context.Data.tracks ?? Array.Empty<DesktopTrack>())
                if (track != null && track.unlockVisit <= Mathf.Max(1, Context.State.visits)) playlist.Add(track);
            DrawPlaylist();
            DesktopTrack requested = playlist.Find(track => track.id == argument);
            if (requested != null) SelectTrack(requested);
            else if (selected == null || !playlist.Contains(selected))
            {
                if (playlist.Count > 0) SelectTrack(playlist[0]);
                else UpdateDisplay();
            }
            else UpdateDisplay();
        }

        void DrawPlaylist()
        {
            DesktopUI.Clear(tracksArea);
            trackButtons.Clear();
            float width = tracksArea.rect.width;
            var header = DesktopUI.Panel(tracksArea, "Cabeçalho da lista", 1, 1, width - 2, 30, Context.Data.titleSprite);
            var heading = DesktopUI.Label(header, "Lista de reprodução", 9, 5, width - 20, 23, 14);
            heading.color = Color.white;
            for (int i = 0; i < playlist.Count; i++)
            {
                var track = playlist[i];
                string title = (i + 1).ToString("00") + "   " + track.title + (track.clip ? string.Empty : "\n[ÁUDIO INDISPONÍVEL]");
                var button = DesktopUI.Button(tracksArea, title, 5, 37 + i * 66, width - 10, 61, () => SelectTrack(track));
                var label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 13; label.alignment = TextAlignmentOptions.MidlineLeft;
                trackButtons[track.id] = button;
            }
        }

        void StepTrack(int direction)
        {
            if (playlist.Count == 0) return;
            int current = selected == null ? 0 : playlist.IndexOf(selected);
            int next = Mathf.Clamp(current + direction, 0, playlist.Count - 1);
            if (next == current) return;
            bool resume = playing;
            SelectTrack(playlist[next]);
            if (resume) TogglePlay();
        }

        void SelectTrack(DesktopTrack track)
        {
            Stop();
            selected = track;
            source.clip = selected.clip;
            UpdateDisplay();
        }

        public void TogglePlay()
        {
            if (selected == null) return;
            if (playing)
            {
                if (selected.clip) source.Pause();
                playing = false;
            }
            else
            {
                if (position >= DurationSeconds) position = 0;
                if (selected.clip)
                {
                    source.time = Mathf.Clamp(position, 0, Mathf.Max(0, DurationSeconds - .01f));
                    source.Play();
                }
                playing = true;
            }
            UpdateDisplay();
        }

        public void Stop()
        {
            if (source) source.Stop();
            playing = false;
            position = 0;
            if (status) UpdateDisplay();
        }

        public void Seek(float normalized)
        {
            if (selected == null) return;
            position = Mathf.Clamp01(normalized) * DurationSeconds;
            if (selected.clip)
                source.time = Mathf.Clamp(position, 0, Mathf.Max(0, DurationSeconds - .01f));
            UpdateDisplay();
        }

        public override void Tick(float delta)
        {
            if (!playing || selected == null) return;
            position = selected.clip ? source.time : Mathf.Min(DurationSeconds, position + Mathf.Max(0, delta));
            if (position >= DurationSeconds || (selected.clip && !source.isPlaying))
            {
                position = DurationSeconds;
                playing = false;
                if (source) source.Stop();
            }
            UpdateDisplay();
        }

        public override void OnClose() => Stop();

        void UpdateDisplay()
        {
            trackTitle.text = selected?.title ?? "Nenhuma faixa disponível";
            status.text = selected == null ? "Nenhum arquivo de áudio encontrado."
                : !selected.clip ? "ÁUDIO INDISPONÍVEL\nPrévia sem som.\n" + (playing ? "Prévia sem som. Mostrando a duração." : "Arquivo de áudio ausente.")
                : playing ? "Reproduzindo" : position >= DurationSeconds ? "Concluído" : position > 0 ? "Pausado" : "Parado";
            time.text = FormatTime(position) + " / " + FormatTime(DurationSeconds);
            progress.SetValueWithoutNotify(DurationSeconds > 0 ? position / DurationSeconds : 0);
            progress.interactable = selected != null;
            playButton.interactable = selected != null;
            var label = playButton.GetComponentInChildren<TMP_Text>();
            if (label) label.text = playing ? "Pausar" : "Reproduzir";
            foreach (var row in trackButtons)
            {
                bool active = selected != null && selected.id == row.Key;
                row.Value.GetComponent<Image>().color = active ? new Color(.20f, .47f, .85f) : Color.white;
                var text = row.Value.GetComponentInChildren<TMP_Text>();
                text.color = active ? Color.white : new Color(.08f, .16f, .29f);
            }
        }

        static string FormatTime(float value)
        {
            int seconds = Mathf.Max(0, Mathf.FloorToInt(value));
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }
    }

    public sealed class DesktopMinesApp : DesktopApp
    {
        MinesBoard board;
        RectTransform grid, frame;
        TMP_Text status, mineCounter, flagCounter;
        Button[,] cells;
        Image[,] mineIcons;
        public MinesBoard Board => board;

        public override void Build()
        {
            BuildLayout();
            NewGame();
        }

        void BuildLayout()
        {
            DesktopUI.Clear(Body);
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 446;
            DesktopUI.Label(Body, "Jogo     Ajuda", 9, 3, width - 18, 23, 14);
            frame = DesktopUI.Panel(Body, "Campo clássico", 12, 30, width - 24, height - 104);
            frame.GetComponent<Image>().color = new Color(.82f, .82f, .80f);
            status = DesktopUI.Label(Body, "", 14, height - 66, width - 28, 34, 13);
            DesktopUI.Label(Body, "Clique: revelar     Botão direito: bandeira", 14, height - 27, width - 28, 22, 12);
        }

        public override void OnResize()
        {
            BuildLayout();
            if (board != null) DrawBoard();
        }

        public void NewGame()
        {
            Context.State.minesGames++;
            bool rare = Context.State.minesGames % 16 == 0 && !Context.HasFlag("desktop.mines.0307");
            if (rare) Context.Unlock("desktop.mines.0307");
            Context.Changed();
            board = new MinesBoard(unchecked(Environment.TickCount ^ Context.State.minesGames * 7919), rare);
            DrawBoard();
        }

        void DrawBoard()
        {
            DesktopUI.Clear(frame);
            float width = Body.rect.width > 0 ? Body.rect.width : 780;
            float height = Body.rect.height > 0 ? Body.rect.height : 446;
            int size = board.IsRarePattern ? 28 : 32;
            float fieldWidth = board.Width * size;
            float fieldHeight = board.Height * size;
            frame.anchoredPosition = new Vector2(Mathf.Max(8, (width - fieldWidth - 20) * .5f), -30);
            frame.sizeDelta = new Vector2(fieldWidth + 20, Mathf.Min(height - 108, fieldHeight + 80));
            var toolbar = DesktopUI.Panel(frame, "Contadores", 7, 7, fieldWidth + 6, 49);
            toolbar.GetComponent<Image>().color = new Color(.80f, .80f, .78f);
            var remaining = DesktopUI.Panel(toolbar, "Minas restantes", 5, 7, 66, 33);
            var remainingImage = remaining.GetComponent<Image>(); remainingImage.sprite = null; remainingImage.color = new Color(.055f, .035f, .03f);
            mineCounter = DesktopUI.Label(remaining, "010", 5, 1, 58, 30, 24);
            if(Context.Data.retroFont)mineCounter.font=Context.Data.retroFont;
            mineCounter.color = new Color(1, .12f, .045f); mineCounter.fontStyle = FontStyles.Bold;
            mineCounter.alignment = TextAlignmentOptions.MidlineRight;
            mineCounter.characterSpacing = 2;
            DesktopUI.Button(toolbar, "Novo jogo", (fieldWidth - 104) * .5f + 3, 8, 104, 32, NewGame);
            var marked = DesktopUI.Panel(toolbar, "Bandeiras marcadas", fieldWidth - 65, 7, 66, 33);
            var markedImage = marked.GetComponent<Image>(); markedImage.sprite = null; markedImage.color = new Color(.055f, .035f, .03f);
            flagCounter = DesktopUI.Label(marked, "000", 5, 1, 58, 30, 24);
            if(Context.Data.retroFont)flagCounter.font=Context.Data.retroFont;
            flagCounter.color = new Color(1, .12f, .045f); flagCounter.fontStyle = FontStyles.Bold;
            flagCounter.alignment = TextAlignmentOptions.MidlineRight; flagCounter.characterSpacing = 2;
            grid = DesktopUI.Panel(frame, "Campo", 8, 64, fieldWidth + 4, fieldHeight + 4);
            grid.GetComponent<Image>().color = new Color(.48f, .48f, .46f);
            cells = new Button[board.Width, board.Height];
            mineIcons = new Image[board.Width, board.Height];
            var app = Context.Data.FindApp("mines");
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    int cellX = x, cellY = y;
                    var button = DesktopUI.Button(grid, "", 2 + x * size, 2 + y * size, size - 1, size - 1,
                        () => Reveal(cellX, cellY));
                    button.GetComponent<Image>().color = new Color(.83f, .83f, .81f);
                    var colors = button.colors; colors.disabledColor = Color.white;
                    colors.highlightedColor = new Color(.96f, .96f, .95f); colors.pressedColor = new Color(.69f, .69f, .67f); button.colors = colors;
                    var label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 16;
                    // The sharp font's line height exceeded the old box and TMP truncated the entire number.
                    label.rectTransform.anchoredPosition = new Vector2(1, -1);
                    label.rectTransform.sizeDelta = new Vector2(size - 3, size - 3);
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.fontStyle = FontStyles.Bold;
                    if (app != null && app.icon)
                    {
                        mineIcons[x, y] = DesktopUI.Image(button.transform, app.icon, 5, 5, size - 11, size - 11);
                        mineIcons[x, y].gameObject.SetActive(false);
                    }
                    button.gameObject.AddComponent<DesktopMineRightClick>().Clicked = () => Flag(cellX, cellY);
                    cells[x, y] = button;
                }
            UpdateBoard();
        }

        public void Reveal(int x, int y)
        {
            if (!board.Reveal(x, y)) return;
            Context.PlaySound(board.HasLost ? "error" : "click");
            UpdateBoard();
        }

        public void Flag(int x, int y)
        {
            if (!board.ToggleFlag(x, y)) return;
            Context.PlaySound("click");
            UpdateBoard();
        }

        void UpdateBoard()
        {
            bool done = board.HasLost || board.HasWon;
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    var button = cells[x, y];
                    var label = button.GetComponentInChildren<TMP_Text>();
                    bool revealed = board.IsRevealed(x, y);
                    int nearby = board.AdjacentMines(x, y);
                    bool showMine = done && board.IsMine(x, y);
                    if (mineIcons[x, y]) mineIcons[x, y].gameObject.SetActive(showMine);
                    label.text = showMine ? mineIcons[x, y] ? "" : "*" : board.IsFlagged(x, y) ? "F"
                        : !revealed || nearby == 0 ? "" : nearby.ToString();
                    button.interactable = !done && !revealed;
                    var face = button.GetComponent<Image>();
                    face.sprite = revealed ? Context.Data.windowSprite : Context.Data.buttonSprite;
                    face.color = revealed ? new Color(.71f, .71f, .69f) : new Color(.85f, .85f, .83f);
                    label.color = board.IsFlagged(x, y) ? new Color(.80f, .08f, .04f) : NumberColor(nearby);
                }
            mineCounter.text = Mathf.Clamp(board.MineCount - board.Flags, -99, 999).ToString("000");
            flagCounter.text = Mathf.Clamp(board.Flags, 0, 999).ToString("000");
            status.text = board.HasLost ? "Você acertou uma mina. Clique em Novo jogo para tentar de novo."
                : board.HasWon ? "Campo concluído."
                : "Minas: " + board.MineCount + "    Bandeiras: " + board.Flags;
        }

        static Color NumberColor(int number)
        {
            switch (number)
            {
                case 1: return new Color(.02f, .08f, .78f);
                case 2: return new Color(.02f, .42f, .05f);
                case 3: return new Color(.86f, .03f, .02f);
                case 4: return new Color(.04f, .02f, .40f);
                case 5: return new Color(.44f, .03f, .02f);
                case 6: return new Color(.02f, .42f, .43f);
                case 7: return new Color(.04f, .04f, .04f);
                default: return new Color(.32f, .32f, .32f);
            }
        }
    }

    public sealed class DesktopMineRightClick : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;
        public void OnPointerClick(PointerEventData eventData)
        {
            var button = GetComponent<Button>();
            if (eventData.button == PointerEventData.InputButton.Right && button && button.interactable)
                Clicked?.Invoke();
        }
    }

    // Pure board state; first normal click is safe, and the rare board preserves the four digit shapes.
    public sealed class MinesBoard
    {
        readonly bool[,] mines, revealed, flags;
        readonly int seed;
        bool started;
        int revealedSafe;

        public int Width { get; }
        public int Height { get; }
        public int MineCount { get; private set; }
        public int Flags { get; private set; }
        public bool IsRarePattern { get; }
        public bool HasLost { get; private set; }
        public bool HasWon => !HasLost && revealedSafe == Width * Height - MineCount;

        public MinesBoard(int seed, bool rarePattern = false)
        {
            this.seed = seed;
            IsRarePattern = rarePattern;
            Width = rarePattern ? 16 : 8;
            Height = rarePattern ? 7 : 8;
            mines = new bool[Width, Height];
            revealed = new bool[Width, Height];
            flags = new bool[Width, Height];
            if (rarePattern) PlaceDigits();
            else PlaceNormal(-10, -10);
        }

        public bool IsMine(int x, int y) => Inside(x, y) && mines[x, y];
        public bool IsRevealed(int x, int y) => Inside(x, y) && revealed[x, y];
        public bool IsFlagged(int x, int y) => Inside(x, y) && flags[x, y];

        public int AdjacentMines(int x, int y)
        {
            if (!Inside(x, y)) return 0;
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && IsMine(x + dx, y + dy)) count++;
            return count;
        }

        public bool ToggleFlag(int x, int y)
        {
            if (!Inside(x, y) || revealed[x, y] || HasLost || HasWon) return false;
            flags[x, y] = !flags[x, y];
            Flags += flags[x, y] ? 1 : -1;
            return true;
        }

        public bool Reveal(int x, int y)
        {
            if (!Inside(x, y) || flags[x, y] || revealed[x, y] || HasLost || HasWon) return false;
            if (!started)
            {
                if (!IsRarePattern) PlaceNormal(x, y);
                started = true;
            }
            if (mines[x, y])
            {
                revealed[x, y] = true;
                HasLost = true;
                return true;
            }
            var pending = new Queue<int>();
            pending.Enqueue(y * Width + x);
            while (pending.Count > 0)
            {
                int next = pending.Dequeue();
                int cellX = next % Width, cellY = next / Width;
                if (revealed[cellX, cellY] || flags[cellX, cellY] || mines[cellX, cellY]) continue;
                revealed[cellX, cellY] = true;
                revealedSafe++;
                if (AdjacentMines(cellX, cellY) != 0) continue;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cellX + dx, ny = cellY + dy;
                        if (Inside(nx, ny) && !revealed[nx, ny] && !mines[nx, ny])
                            pending.Enqueue(ny * Width + nx);
                    }
            }
            return true;
        }

        void PlaceNormal(int safeX, int safeY)
        {
            Array.Clear(mines, 0, mines.Length);
            MineCount = 10;
            var choices = new List<int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (Math.Abs(x - safeX) > 1 || Math.Abs(y - safeY) > 1) choices.Add(y * Width + x);
            var random = new System.Random(seed);
            for (int i = 0; i < MineCount; i++)
            {
                int index = random.Next(choices.Count);
                int value = choices[index];
                choices.RemoveAt(index);
                mines[value % Width, value / Width] = true;
            }
        }

        void PlaceDigits()
        {
            string[][] digits =
            {
                new[] { "111", "101", "101", "101", "111" },
                new[] { "111", "001", "111", "001", "111" },
                new[] { "111", "101", "101", "101", "111" },
                new[] { "111", "001", "001", "001", "001" }
            };
            for (int digit = 0; digit < digits.Length; digit++)
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 3; x++)
                        if (digits[digit][y][x] == '1')
                        {
                            mines[digit * 4 + x, y + 1] = true;
                            MineCount++;
                        }
        }

        bool Inside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    }

    static class DesktopParagraphs
    {
        public static void Draw(RectTransform parent, EmailParagraph[] paragraphs, float x, float y,
            float width, float height)
        {
            float contentHeight = 16;
            foreach (var paragraph in paragraphs ?? Array.Empty<EmailParagraph>())
                if (paragraph != null) contentHeight += EstimateHeight(paragraph, width) + 16;
            var content = DesktopUI.Scroll(parent, x, y, width, height, Mathf.Max(height, contentHeight));
            var paper = content.parent.parent.GetComponent<Image>();
            if (paper) { paper.sprite = null; paper.color = Color.white; }
            float nextY = 8;
            foreach (var paragraph in paragraphs ?? Array.Empty<EmailParagraph>())
            {
                if (paragraph == null) continue;
                float labelHeight = EstimateHeight(paragraph, width);
                var label = DesktopUI.Label(content, paragraph.text, 10, nextY, width - 34, labelHeight,
                    paragraph.child ? 21 : 17, paragraph.child);
                label.richText = false;
                nextY += labelHeight + 16;
            }
        }

        static float EstimateHeight(EmailParagraph paragraph, float width)
        {
            int charactersPerLine = Mathf.Max(12, Mathf.FloorToInt((width - 42) / (paragraph.child ? 12 : 9)));
            int lines = 0;
            foreach (string line in (paragraph.text ?? string.Empty).Split('\n'))
                lines += Mathf.Max(1, Mathf.CeilToInt((float)line.Length / charactersPerLine));
            return lines * (paragraph.child ? 30 : 25) + 12;
        }
    }
}
