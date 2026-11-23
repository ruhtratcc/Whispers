// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionSchoolSceneTests
    {
        static Button ArchiveButton(GameObject root, string name) => root.GetComponentsInChildren<Button>(true)
            .First(button => button.gameObject.activeInHierarchy && button.gameObject.name == name);
        static void ClickArchive(GameObject root, string name) => ArchiveButton(root, name).onClick.Invoke();

        void ArchiveCapture(string name, int width, int height)
        {
            string output = System.Environment.GetEnvironmentVariable("WHISPERS_ARCHIVE_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            var capture = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("HorrorGame.Editor.PresentationCapture")).First(type => type != null);
            capture.GetMethod("Capture").Invoke(null, new object[] { SceneManager.GetActiveScene(),
                game.player.view.playerCamera, Path.Combine(output, name + ".png"), width, height, null, false });
        }

        IEnumerator CloseArchiveInspection()
        {
            inspection.Close();
            yield return Until(() => !inspection.IsCompleting && game.hud.InventoryOpen, "Return to the archive after examining the item.");
        }

        [UnityTest, Timeout(480000)]
        public IEnumerator PersonalArchiveCollectsReadsRestoresAndQueuesRealInventoryEvents()
        {
            var inventory = game.interaction.inventory;
            Assert.That(inventory.Contains(BeforeDemolitionCampaign.WhistleItemId), Is.True, "Story carry was applied.");
            var carriedEmail = inventory.Find("bd.note.email.06");
            Assert.That(carriedEmail.chapterId, Is.EqualTo(BeforeDemolitionCampaign.OfficeChapterId));
            Assert.That(carriedEmail.inspectionSeen, Is.True);
            Assert.That(carriedEmail.text, Is.Not.Empty);
            var archive = game.hud.inventoryPanel;
            yield return Press(Key.Tab);
            yield return Until(() => game.State == GameState.Paused && archive.activeInHierarchy, "Tab opens the archive.");
            Assert.That(ArchiveButton(archive, "Registro bd.note.email.06"), Is.Not.Null);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Cursor.visible, Is.True);
            var rotation = game.player.view.playerCamera.transform.rotation;
            ClickArchive(archive, "Pistas");
            Assert.That(archive.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Nenhum registro encontrado."), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(game.player.view.playerCamera.transform.rotation, Is.EqualTo(rotation));
            yield return Press(Key.I);
            yield return Until(() => game.CanControl && !archive.activeSelf, "I closes the existing archive pause.");
            Assert.That(Cursor.visible, Is.False);
            yield return Press(Key.I);
            yield return Until(() => archive.activeSelf, "I also opens.");
            yield return Press(Key.Escape);
            yield return Until(() => game.CanControl && !archive.activeSelf, "Escape closes.");
            yield return Press(Key.Escape);
            yield return Until(() => game.hud.pausePanel.activeSelf, "Escape opens the ordinary pause menu.");
            yield return Press(Key.Tab);
            Assert.That(game.hud.pausePanel.activeSelf, Is.True);
            Assert.That(archive.activeSelf, Is.False);
            yield return Press(Key.Escape);
            yield return Until(() => game.CanControl, "Resume before collection.");

            var noteObject = new GameObject("Archive pickup test");
            var note = noteObject.AddComponent<CollectibleItem>();
            note.persistentId = "test.archive.collectible";
            note.kind = CollectibleKind.Note;
            note.itemId = "test.archive.note";
            note.title = "Bilhete de teste";
            note.noteText = "Texto completo do bilhete.";
            note.Interact(game.interaction);
            Assert.That(game.hud.notePanel.activeSelf, Is.True);
            Assert.That(inventory.Contains(note.itemId), Is.True);
            Assert.That(inventory.Find(note.itemId).inspectionSeen, Is.True);
            yield return Press(Key.Tab);
            Assert.That(archive.activeSelf, Is.False, "The existing note keeps keyboard priority.");
            game.Resume();
            Assert.That(inventory.Add(new InventoryItem { id = "bd.cups", title = "Pegadas pequenas na poeira",
                text = "Quatro pares de pegadas pequenas na poeira.", note = true }), Is.True);
            Assert.That(inventory.Add(new InventoryItem { id = "bd.cups", title = "Duplicata", note = true }), Is.False);
            Assert.That(inventory.Add(new InventoryItem { id = "bd.whistle.lore.1", title = "Anotação de Helena — o sinal",
                note = true, inspectionId = "bd.whistle.lore.1" }), Is.True);
            Assert.That(inventory.Add(new InventoryItem { id = "test.archive.key", title = "Chave de teste" }), Is.True);

            var layer = game.hud.inventoryPanel.transform.parent;
            yield return Until(() => layer.Find("Notificação de descoberta") &&
                layer.Find("Notificação de descoberta").gameObject.activeSelf, "Collection queues a real discovery.");
            var notice = layer.Find("Notificação de descoberta").gameObject;
            Assert.That(notice.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(notice.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "NOVO DOCUMENTO ENCONTRADO"), Is.True);
            yield return Press(Key.Tab);
            Assert.That(notice.activeSelf, Is.False);
            ClickArchive(archive, "Pistas");
            Assert.That(archive.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Descoberta")), Is.True);
            Assert.That(archive.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Ver documento:")), Is.False,
                "An undiscovered email is not disclosed by its relation.");
            Assert.That(inventory.Add(new InventoryItem { id = "bd.note.email.03", title = "Email de Helena", note = true,
                text = "Email de teste." }), Is.True);
            yield return null;
            yield return null;
            Assert.That(archive.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Ver documento:")), Is.True,
                "A discovered email can be opened through the link.");
            ClickArchive(archive, "Documentos");
            ClickArchive(archive, "Registro bd.whistle.lore.1");
            Assert.That(inventory.Find("bd.whistle.lore.1").inspectionSeen, Is.False);
            yield return new WaitForSecondsRealtime(.25f);
            ArchiveCapture("arquivo-documentos-16x9", 1280, 720);
            ClickArchive(archive, "Ler");
            yield return null;
            Assert.That(inventory.Find("bd.whistle.lore.1").inspectionSeen, Is.True);
            Assert.That(archive.GetComponentsInChildren<RawImage>().Any(i => i.texture && i.transform.parent.parent.name == "Janela"), Is.True);
            ClickArchive(archive, "+");
            ArchiveCapture("arquivo-leitura-16x9", 1280, 720);
            Assert.That(game.State, Is.EqualTo(GameState.Paused));
            Assert.That(game.save.Save(), Is.True);
            Assert.That(game.save.TryReadCurrent(out var snapshot), Is.True);
            Assert.That(snapshot.inventory.Single(i => i.id == "bd.whistle.lore.1").inspectionSeen, Is.True);
            Assert.That(snapshot.inventory.Single(i => i.id == "bd.whistle.lore.1").chapterId, Is.EqualTo("bd.secretary-library"));
            ClickArchive(archive, "Objetos importantes");
            Assert.That(ArchiveButton(archive, "Registro test.archive.key"), Is.Not.Null);
            var keyArt = inventory.Find("test.archive.key");
            keyArt.inspectionId = "bd.library.key";
            ClickArchive(archive, "Registro test.archive.key");
            Assert.That(archive.GetComponentsInChildren<RawImage>().Any(image => image.texture && image.texture.name == "bd.library.key"), Is.True);
            ArchiveCapture("arquivo-objetos-4x3", 1024, 768);
            ClickArchive(archive, "Examinar em 3D");
            yield return Until(() => inspection.IsOpen, "A legacy inspection alias opens its actual model.");
            yield return CloseArchiveInspection();
            Assert.That(inventory.Find("test.archive.key").inspectionSeen, Is.True);
            Assert.That(inventory.Contains("bd.library.key"), Is.False, "Reopening an alias cannot grant the gameplay key.");
            yield return Press(Key.I);
            yield return Until(() => notice.activeSelf, "Notification resumes after archive closes.");
            yield return Until(() => notice.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "NOVA PISTA DESCOBERTA"), "The clue follows in order.", 8);
            yield return Until(() => notice.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "ITEM IMPORTANTE ADQUIRIDO"), "The important item shows after the second document.", 12);
            inventory.Restore(snapshot.inventory);
            yield return null;
            Assert.That(notice.activeSelf, Is.False, "Restoring discards pending notifications.");
            Assert.That(inventory.Find("bd.whistle.lore.1").inspectionSeen, Is.True);
        }
    }

    public sealed partial class BeforeDemolitionFourStudentsTests
    {
        [UnityTest, Timeout(480000)]
        public IEnumerator PersonalArchivePlaysExistingBiaRecordingAndReturnsToMediaCategory()
        {
            yield return Inspect(BeforeDemolitionFourStudentsAction.Bia);
            yield return Close();
            game.hud.ShowInventory();
            var root = game.hud.inventoryPanel;
            var media = root.GetComponentsInChildren<Button>().Single(b => b.name == "Fotografias e mídias");
            media.onClick.Invoke();
            var entry = root.GetComponentsInChildren<Button>().Single(b => b.name == "Registro " + BeforeDemolitionFourStudentsFlow.BiaTapeId);
            entry.onClick.Invoke();
            root.GetComponentsInChildren<Button>().Single(b => b.name == "Ouvir gravação").onClick.Invoke();
            yield return Until(() => inspection.IsOpen && inspection.RecordingPlaying, "The existing recorder starts with the acquired tape.");
            Assert.That(inspection.PlayingRecordingId, Is.EqualTo(BeforeDemolitionFourStudentsFlow.BiaTapeId));
            inspection.Close();
            yield return Until(() => game.State == GameState.Paused && root.activeSelf && !inspection.IsCompleting,
                "Closing the archived playback returns to the same archive.");
            Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "Registro " + BeforeDemolitionFourStudentsFlow.BiaTapeId), Is.True);
            Assert.That(flow.TapeHeard, Is.False, "Opening the archive cannot bypass a tape's completion.");
            game.Resume();
        }
    }
}
#endif

