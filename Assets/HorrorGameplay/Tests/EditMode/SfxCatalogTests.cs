// Verifica VariantsUseTheExistingNonRepeatingSoundSet, MissingOrEmptySoundsReturnNoVariant, LookupMatchesOnlyTheExactNameAndSkipsNullEntries.

using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    public sealed class SfxCatalogTests
    {
        [Test]
        public void VariantsUseTheExistingNonRepeatingSoundSet()
        {
            var sound = ScriptableObject.CreateInstance<SoundSet>();
            var a = AudioClip.Create("SFX test A", 441, 1, 44100, false);
            var b = AudioClip.Create("SFX test B", 441, 1, 44100, false);
            var state = Random.state;
            try
            {
                sound.clips = new[] { a, null, b };
                var entry = new SfxCatalogEntry { name = "door_open", sound = sound };
                Assert.That(entry.Variants, Is.SameAs(sound.clips));
                AudioClip previous = null;
                for (int i = 0; i < 32; i++)
                {
                    var next = entry.Next();
                    Assert.That(next, Is.Not.Null.And.Not.SameAs(previous));
                    previous = next;
                }
                Assert.That(entry.Pitch, Is.EqualTo(sound.pitch));
            }
            finally
            {
                Random.state = state;
                Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(sound);
            }
        }

        [Test]
        public void MissingOrEmptySoundsReturnNoVariant()
        {
            var catalog = ScriptableObject.CreateInstance<SfxCatalog>();
            var sound = ScriptableObject.CreateInstance<SoundSet>();
            try
            {
                var missing = new SfxCatalogEntry();
                Assert.That(missing.Variants, Is.Empty);
                Assert.That(missing.Next(), Is.Null);
                sound.clips = new AudioClip[0];
                Assert.That(new SfxCatalogEntry { sound = sound }.Next(), Is.Null);
                Assert.That(catalog.Find(null), Is.Null);
                Assert.That(catalog.Play("absent", Vector3.zero), Is.Null);
                catalog.entries = null;
                Assert.That(catalog.Find("absent"), Is.Null);
            }
            finally { Object.DestroyImmediate(catalog); Object.DestroyImmediate(sound); }
        }

        [Test]
        public void LookupMatchesOnlyTheExactNameAndSkipsNullEntries()
        {
            var catalog = ScriptableObject.CreateInstance<SfxCatalog>();
            try
            {
                var entry = new SfxCatalogEntry { name = "door_open" };
                catalog.entries = new[] { null, entry };
                Assert.That(catalog.Find("door_open"), Is.SameAs(entry));
                Assert.That(catalog.Find("Door_Open"), Is.Null);
                Assert.That(catalog.Find("door_open_01"), Is.Null);
                Assert.That(catalog.Find(" "), Is.Null);
            }
            finally { Object.DestroyImmediate(catalog); }
        }
    }
}
