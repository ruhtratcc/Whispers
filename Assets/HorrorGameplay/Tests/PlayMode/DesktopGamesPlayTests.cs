// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    public sealed partial class DesktopOfficePlayTests
    {
        [UnityTest, Category("PCGames"), Timeout(90000)]
        public IEnumerator MinesNumbersHaveVisibleGeometryAfterARealCellClick()
        {
            yield return Enter();
            var window = pc.OpenApp("mines");
            var app = (DesktopMinesApp)window.App;
            var cells = window.body.GetComponentsInChildren<DesktopMineRightClick>();
            yield return Click(cells[3 * app.Board.Width + 3].GetComponent<Button>());
            Assert.That(app.Board.HasLost, Is.False);
            var numbers = cells.Select(c => c.GetComponentInChildren<TMP_Text>())
                .Where(t => int.TryParse(t.text, out var n) && n > 0).ToArray();
            Assert.That(numbers, Is.Not.Empty);
            foreach (var text in numbers) text.ForceMeshUpdate();
            Directory.CreateDirectory(VisualDirectory);
            File.WriteAllText(Path.Combine(VisualDirectory, "mines-number-metrics.json"),
                "[" + string.Join(",", numbers.Select(t => "{\"text\":\"" + t.text + "\",\"fontSize\":" + t.fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ",\"height\":" + t.rectTransform.rect.height.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ",\"visibleCharacters\":" + t.textInfo.characterInfo.Take(t.textInfo.characterCount).Count(c => c.isVisible) + "}")) + "]");
            yield return Capture("mines-revealed", false);
            foreach (var text in numbers)
                Assert.That(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c => c.isVisible), Is.True,
                    "A revealed number must generate visible glyphs: " + text.text);

            var hidden = cells.First(c => !app.Board.IsRevealed(Array.IndexOf(cells, c) % app.Board.Width, Array.IndexOf(cells, c) / app.Board.Width));
            yield return Click(hidden.GetComponent<Button>(), true);
            AssertVisibleGlyph(hidden.GetComponentInChildren<TMP_Text>(), "F");
            yield return Click(hidden.GetComponent<Button>(), true);
            int mine = Enumerable.Range(0,cells.Length).First(i => app.Board.IsMine(i % app.Board.Width, i / app.Board.Width));
            yield return Click(cells[mine].GetComponent<Button>());Assert.That(app.Board.HasLost, Is.True);
            yield return Click(Button(window, "Novo jogo"));Assert.That(app.Board.HasLost, Is.False);
            pc.State.minesGames = 15;
            yield return Click(Button(window, "Novo jogo"));Assert.That(app.Board.IsRarePattern, Is.True);
            cells = window.body.GetComponentsInChildren<DesktopMineRightClick>();
            yield return Click(cells[0].GetComponent<Button>(), true);AssertVisibleGlyph(cells[0].GetComponentInChildren<TMP_Text>(), "F");
            var rare = app.Board;
            yield return Click(Button(window, "□"));yield return Click(Button(window, "□"));
            Assert.That(app.Board, Is.SameAs(rare));Assert.That(app.Board.IsFlagged(0,0), Is.True);
            cells = window.body.GetComponentsInChildren<DesktopMineRightClick>();
            AssertVisibleGlyph(cells[0].GetComponentInChildren<TMP_Text>(), "F");
            yield return Click(cells[0].GetComponent<Button>(), true);yield return Click(cells[0].GetComponent<Button>());
            foreach(var label in cells.Select(c=>c.GetComponentInChildren<TMP_Text>()).Where(t=>t.text.Length>0))AssertVisibleGlyph(label,label.text);
            yield return Capture("mines-rare-revealed", false);
        }

        static void AssertVisibleGlyph(TMP_Text label, string expected)
        {
            Assert.That(label.text, Is.EqualTo(expected));label.ForceMeshUpdate();
            Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.isVisible), Is.True, "Visible text required: " + expected);
        }

        [UnityTest, Category("PCGames"), Timeout(120000)]
        public IEnumerator SnakeUsesTheDesktopIconRealKeysPauseFocusResizeAndRestart()
        {
            yield return Enter();
            var icon=pc.DesktopRoot.GetComponentsInChildren<DesktopIcon>().Single(i=>i.AppId=="snake");
            var iconRect=(RectTransform)icon.transform;
            Assert.That(-iconRect.anchoredPosition.y+iconRect.rect.height,Is.LessThanOrEqualTo(726));
            var point=DesktopPoint(iconRect,iconRect.rect.center);pc.Pointer.ClickAt(point);pc.Pointer.ClickAt(point);yield return null;
            Assert.That(pc.Windows.ContainsKey("snake"),Is.True);
            var window=pc.Windows["snake"];var app=(DesktopSnakeApp)window.App;
            Assert.That(app.Started,Is.False);Assert.That(app.Board.Score,Is.Zero);
            var head=app.Board.Segments[0];
            yield return Press(Key.UpArrow);yield return Until(()=>app.Board.Segments[0].y<head.y,"Up moves the snake toward the top",3);
            yield return Press(Key.Space);Assert.That(app.Paused,Is.True);
            head=app.Board.Segments[0];yield return new WaitForSeconds(.25f);Assert.That(app.Board.Segments[0],Is.EqualTo(head));
            var board=app.Board;yield return Click(Button(window,"□"));yield return Click(Button(window,"□"));
            Assert.That(app.Board,Is.SameAs(board));Assert.That(app.Board.Segments[0],Is.EqualTo(head));
            yield return Click(Button(window,SnakeText.Resume));Assert.That(app.Paused,Is.False);
            var mail=pc.OpenApp("mail");yield return null;Assert.That(app.Paused,Is.True);
            head=app.Board.Segments[0];yield return new WaitForSeconds(.25f);Assert.That(app.Board.Segments[0],Is.EqualTo(head));
            pc.CloseApp("mail");yield return null;
            yield return Press(Key.F10);Assert.That(game.State,Is.EqualTo(GameState.Paused));
            yield return new WaitForSecondsRealtime(.2f);Assert.That(app.Board.Segments[0],Is.EqualTo(head));
            yield return Press(Key.Escape);Assert.That(pc.IsSeated,Is.True);
            yield return Press(Key.R);Assert.That(app.Started,Is.False);Assert.That(app.Board.Score,Is.Zero);
            yield return Click(Button(window,SnakeText.Play));yield return Click(Button(window,"_"));yield return null;
            Assert.That(app.Paused,Is.True);head=app.Board.Segments[0];yield return new WaitForSeconds(.25f);Assert.That(app.Board.Segments[0],Is.EqualTo(head));
            yield return Click(window.TaskButton);yield return Press(Key.R);
            yield return Click(Button(window,SnakeText.Play));yield return Until(()=>app.Board.HasLost,"Wall collision ends the round",5);
            Assert.That(Text(window),Does.Contain(SnakeText.Lost));
            yield return Click(Button(window,SnakeText.NewGame));Assert.That(app.Board.HasLost,Is.False);Assert.That(app.Board.Segments.Count,Is.EqualTo(3));
            foreach(var label in window.body.GetComponentsInChildren<TMP_Text>().Where(t=>t.text.Length>0))AssertVisibleGlyph(label,label.text);
            yield return Capture("cobrinha",false);pc.CloseApp("snake");yield return null;
            yield return Click(DesktopButton("Iniciar"));yield return Click(DesktopButton(DesktopSystemText.AllPrograms+"  >"));
            var programs=pc.DesktopRoot.Find("Menu Iniciar/Todos os Programas");
            yield return Click(programs.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text==SnakeText.Title));
            Assert.That(pc.Windows["snake"].App,Is.InstanceOf<DesktopSnakeApp>());
        }
    }
}
#endif
