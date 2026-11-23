// Verifica EndingSkipsTheChapterCardAndKeepsOneTitleWhenMenuBecomesAvailable.

using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HorrorGame.Tests
{
    [Category("StoryRefinement")]
    public sealed class EndingPresentationTests
    {
        [UnityTest]
        public IEnumerator EndingSkipsTheChapterCardAndKeepsOneTitleWhenMenuBecomesAvailable()
        {
            var root = new GameObject("Ending presentation fixture");
            try
            {
                var view = root.AddComponent<ChapterTransitionView>();
                view.group = root.AddComponent<CanvasGroup>();
                view.heading = new GameObject("Heading", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                view.body = new GameObject("Body", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                view.returnButton = new GameObject("Return", typeof(RectTransform)).AddComponent<Button>();
                view.heading.transform.SetParent(root.transform); view.body.transform.SetParent(root.transform);
                view.returnButton.transform.SetParent(root.transform);
                view.fadeSeconds = view.endingTitleSeconds = .02f;
                string last = ""; int titleEntries = 0;
                var presentation = view.Present(new StoryChapterDefinition { heading = "CAPÍTULO 6" }, null, true);
                while (presentation.MoveNext())
                {
                    Assert.That(view.heading.text, Does.Not.Contain("CONCLUÍDO"));
                    if (view.heading.text != last && view.heading.text == "WHISPERS OF THE PAST") titleEntries++;
                    last = view.heading.text;
                    yield return presentation.Current;
                }
                Assert.That(titleEntries, Is.EqualTo(1));
                Assert.That(view.heading.text, Is.EqualTo("WHISPERS OF THE PAST"));
                Assert.That(view.body.text, Is.EqualTo("FIM"));
                Assert.That(view.returnButton.gameObject.activeSelf, Is.False);
                view.ShowUnavailable(null, true);
                Assert.That(view.heading.text, Is.EqualTo(last));
                Assert.That(view.body.text, Is.EqualTo("FIM"));
                Assert.That(view.returnButton.gameObject.activeSelf && view.group.interactable, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
