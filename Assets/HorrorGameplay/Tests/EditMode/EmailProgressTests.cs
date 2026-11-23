// Verifica SetUp, TearDown, InitialInboxIsUnreadAndAcceptsOutOfOrderReadsOnlyOnce, CopiesOwnTheirListsAndDoNotChangeTheOriginal, InvalidStatesAreRejectedWithoutRepairOrMutation, StoreRequiresAnExplicitAbsolutePath, FreshLoadDoesNotCreateFilesAndRestartPreservesReadAndDeliveredIds, HealthyBackupLoadsAfterCorruptOrMissingPrimaryWithoutRewritingFiles, BlockedTemporaryWritePreservesBothFilesAndTheSameStateCanBeRetried, InvalidOrIncompleteFilesFailWithoutWritingOrCreatingAFreshState, InvalidSaveIsRejectedBeforeCreatingItsDisposableDirectory.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionEmail")]
    public sealed class EmailProgressTests
    {
        private EmailCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<EmailCatalog>();
            catalog.messages = EmailCatalog.BeforeDemolitionContent();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

        [Test]
        public void InitialInboxIsUnreadAndAcceptsOutOfOrderReadsOnlyOnce()
        {
            var state = EmailInboxState.Create(catalog);
            Assert.That(state.Validate(catalog), Is.True);
            CollectionAssert.AreEqual(new[] { "email.01", "email.02", "email.03", "email.04", "email.05", "email.06", "email.07" }, state.deliveredIds);
            Assert.That(state.readIds, Is.Empty);
            Assert.That(state.IsRead("email.04"), Is.False, "The forged receipt must not mark a real email read.");
            Assert.That(state.MarkRead(catalog, "email.07"), Is.True);
            Assert.That(state.MarkRead(catalog, "email.01"), Is.True);
            Assert.That(state.MarkRead(catalog, "email.07"), Is.False);
            Assert.That(state.MarkRead(catalog, "email.08"), Is.False);
            Assert.That(state.Deliver(catalog, "email.08"), Is.True);
            Assert.That(state.Deliver(catalog, "email.08"), Is.False);
            Assert.That(state.MarkRead(catalog, "email.08"), Is.True);
            Assert.That(state.MarkRead(catalog, "unknown"), Is.False);
            Assert.That(state.Deliver(catalog, "unknown"), Is.False);
            Assert.That(state.MarkRead(catalog, null), Is.False);
            Assert.That(state.Deliver(catalog, null), Is.False);
            CollectionAssert.AreEqual(new[] { "email.07", "email.01", "email.08" }, state.readIds);
        }

        [Test]
        public void CopiesOwnTheirListsAndDoNotChangeTheOriginal()
        {
            var original = EmailInboxState.Create(catalog);
            original.MarkRead(catalog, "email.07");
            var copy = original.Copy();
            copy.Deliver(catalog, "email.08");
            copy.MarkRead(catalog, "email.08");
            copy.storyId = "other-story";
            copy.version = 9;
            Assert.That(original.storyId, Is.EqualTo("before-demolition"));
            Assert.That(original.version, Is.EqualTo(1));
            Assert.That(original.IsDelivered("email.08"), Is.False);
            Assert.That(original.IsRead("email.08"), Is.False);
            Assert.That(copy.deliveredIds, Is.Not.SameAs(original.deliveredIds));
            Assert.That(copy.readIds, Is.Not.SameAs(original.readIds));
            original.readIds.Clear();
            Assert.That(copy.IsRead("email.07"), Is.True);
        }

        [TestCase("story")]
        [TestCase("version")]
        [TestCase("null-delivered")]
        [TestCase("null-read")]
        [TestCase("unknown-delivered")]
        [TestCase("unknown-read")]
        [TestCase("duplicate-delivered")]
        [TestCase("duplicate-read")]
        [TestCase("whitespace-delivered")]
        [TestCase("whitespace-read")]
        [TestCase("null-id")]
        [TestCase("undelivered-read")]
        public void InvalidStatesAreRejectedWithoutRepairOrMutation(string corruption)
        {
            var state = EmailInboxState.Create(catalog);
            switch (corruption)
            {
                case "story": state.storyId = "old-story"; break;
                case "version": state.version = 2; break;
                case "null-delivered": state.deliveredIds = null; break;
                case "null-read": state.readIds = null; break;
                case "unknown-delivered": state.deliveredIds.Add("unknown"); break;
                case "unknown-read": state.readIds.Add("unknown"); break;
                case "duplicate-delivered": state.deliveredIds.Add("email.01"); break;
                case "duplicate-read": state.readIds.AddRange(new[] { "email.01", "email.01" }); break;
                case "whitespace-delivered": state.deliveredIds.Add(" "); break;
                case "whitespace-read": state.readIds.Add(" "); break;
                case "null-id": state.deliveredIds.Add(null); break;
                case "undelivered-read": state.readIds.Add("email.08"); break;
            }
            var before = JsonUtility.ToJson(state);
            Assert.That(state.Validate(catalog), Is.False);
            Assert.That(state.MarkRead(catalog, "email.02"), Is.False);
            Assert.That(state.Deliver(catalog, "email.08"), Is.False);
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("emails.json")]
        public void StoreRequiresAnExplicitAbsolutePath(string path)
        {
            Assert.Throws<ArgumentException>(() => new EmailProgressStore(path));
        }

        [Test]
        public void FreshLoadDoesNotCreateFilesAndRestartPreservesReadAndDeliveredIds()
        {
            WithTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "emails.json");
                var store = new EmailProgressStore(path);
                Assert.That(store.FilePath, Is.EqualTo(path));
                Assert.That(store.TryLoad(catalog, out var state, out var error), Is.True, error);
                Assert.That(Directory.Exists(directory), Is.False);
                state.MarkRead(catalog, "email.07");
                state.Deliver(catalog, "email.08");
                state.MarkRead(catalog, "email.08");
                Assert.That(store.TrySave(state, catalog, out error), Is.True, error);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                var firstBytes = File.ReadAllBytes(path);
                var restarted = new EmailProgressStore(path);
                Assert.That(restarted.TryLoad(catalog, out var loaded, out error), Is.True, error);
                CollectionAssert.AreEqual(state.readIds, loaded.readIds);
                CollectionAssert.AreEqual(state.deliveredIds, loaded.deliveredIds);
                Assert.That(loaded.MarkRead(catalog, "email.07"), Is.False);
                Assert.That(loaded.IsRead("email.04"), Is.False);
                loaded.MarkRead(catalog, "email.01");
                Assert.That(restarted.TrySave(loaded, catalog, out error), Is.True, error);
                CollectionAssert.AreEqual(firstBytes, File.ReadAllBytes(path + ".bak"));
                Assert.That(new EmailProgressStore(path).TryLoad(catalog, out var final, out error), Is.True, error);
                CollectionAssert.AreEqual(new[] { "email.07", "email.08", "email.01" }, final.readIds);
            });
        }

        [Test]
        public void HealthyBackupLoadsAfterCorruptOrMissingPrimaryWithoutRewritingFiles()
        {
            WithTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "emails.json");
                var store = new EmailProgressStore(path);
                var state = EmailInboxState.Create(catalog);
                state.MarkRead(catalog, "email.07");
                Assert.That(store.TrySave(state, catalog, out var error), Is.True, error);
                state.MarkRead(catalog, "email.01");
                Assert.That(store.TrySave(state, catalog, out error), Is.True, error);
                File.WriteAllText(path, "{damaged");
                var primary = File.ReadAllBytes(path);
                var backup = File.ReadAllBytes(path + ".bak");
                Assert.That(store.TryLoad(catalog, out var loaded, out error), Is.True, error);
                Assert.That(error, Is.Empty);
                CollectionAssert.AreEqual(new[] { "email.07" }, loaded.readIds);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".bak"));
                File.Delete(path);
                Assert.That(store.TryLoad(catalog, out loaded, out error), Is.True, error);
                Assert.That(File.Exists(path), Is.False);
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".bak"));
            });
        }

        [Test]
        public void BlockedTemporaryWritePreservesBothFilesAndTheSameStateCanBeRetried()
        {
            WithTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "emails.json");
                var store = new EmailProgressStore(path);
                var state = EmailInboxState.Create(catalog);
                Assert.That(store.TrySave(state, catalog, out var error), Is.True, error);
                state.MarkRead(catalog, "email.01");
                Assert.That(store.TrySave(state, catalog, out error), Is.True, error);
                var primary = File.ReadAllBytes(path);
                var backup = File.ReadAllBytes(path + ".bak");
                Directory.CreateDirectory(path + ".tmp");
                state.MarkRead(catalog, "email.07");
                Assert.That(store.TrySave(state, catalog, out error), Is.False);
                Assert.That(error, Is.Not.Empty);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".bak"));
                Assert.That(state.IsRead("email.07"), Is.True);
                Directory.Delete(path + ".tmp");
                Assert.That(store.TrySave(state, catalog, out error), Is.True, error);
                Assert.That(store.TryLoad(catalog, out var loaded, out error), Is.True, error);
                CollectionAssert.AreEqual(new[] { "email.01", "email.07" }, loaded.readIds);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(path + ".bak"));
            });
        }

        [TestCase("{damaged", "{broken")]
        [TestCase("{}", "{}")]
        [TestCase("{\"storyId\":\"before-demolition\",\"deliveredIds\":[],\"readIds\":[]}", "{\"storyId\":\"old-story\",\"version\":1,\"deliveredIds\":[],\"readIds\":[]}")]
        public void InvalidOrIncompleteFilesFailWithoutWritingOrCreatingAFreshState(string main, string backup)
        {
            WithTemporaryDirectory(directory =>
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "emails.json");
                File.WriteAllText(path, main);
                File.WriteAllText(path + ".bak", backup);
                File.WriteAllText(path + ".tmp", "uncommitted disposable data");
                var primaryBytes = File.ReadAllBytes(path);
                var backupBytes = File.ReadAllBytes(path + ".bak");
                var temporaryBytes = File.ReadAllBytes(path + ".tmp");
                var store = new EmailProgressStore(path);
                Assert.That(store.TryLoad(catalog, out var loaded, out var error), Is.False);
                Assert.That(loaded, Is.Null);
                Assert.That(error, Is.Not.Empty);
                CollectionAssert.AreEqual(primaryBytes, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(backupBytes, File.ReadAllBytes(path + ".bak"));
                CollectionAssert.AreEqual(temporaryBytes, File.ReadAllBytes(path + ".tmp"));
                Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(3));
            });
        }

        [Test]
        public void InvalidSaveIsRejectedBeforeCreatingItsDisposableDirectory()
        {
            WithTemporaryDirectory(directory =>
            {
                var state = EmailInboxState.Create(catalog);
                state.readIds.Add("email.08");
                var store = new EmailProgressStore(Path.Combine(directory, "emails.json"));
                Assert.That(store.TrySave(state, catalog, out var error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(Directory.Exists(directory), Is.False);
            });
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            var name = "Whispers-email-" + Guid.NewGuid().ToString("N");
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var directory = Path.GetFullPath(Path.Combine(temporaryRoot, name));
            if (!directory.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(directory) != name)
                throw new InvalidOperationException("Disposable email test path escaped its temporary directory.");
            try { action(directory); }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
