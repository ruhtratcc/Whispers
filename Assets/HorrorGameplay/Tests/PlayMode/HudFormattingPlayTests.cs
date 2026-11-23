// Verifica TearDown, FormattingTracksValuesAndDoorStateWithoutAllocatingWhenUnchanged.

using System;
using System.Collections;
using System.Reflection;
using HorrorGame;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    [Category("HudFormatting")]
    public sealed class HudFormattingPlayTests
    {
        Scene fixtureScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixtureScene.IsValid() && fixtureScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(fixtureScene);
        }

        [UnityTest]
        public IEnumerator FormattingTracksValuesAndDoorStateWithoutAllocatingWhenUnchanged()
        {
            // Inactive fixtures cannot run GameSession.Start or write a checkpoint.
            var root = new GameObject("HUD formatting fixture");
            root.SetActive(false);
            fixtureScene = SceneManager.CreateScene("HUD-formatting-" + Guid.NewGuid().ToString("N"));
            SceneManager.MoveGameObjectToScene(root, fixtureScene);
            var lampRoot = new GameObject("Available formatting flashlight");
            SceneManager.MoveGameObjectToScene(lampRoot, fixtureScene);
            try
            {
                var game = root.AddComponent<GameSession>();
                var player = Child<FirstPersonMotor>(root);
                var interaction = Child<PlayerInteraction>(root);
                // Only the lamp is active, outside the office; HUD/session stay inert and cannot autosave.
                var flashlight = lampRoot.AddComponent<FlashlightController>();
                Assert.That(flashlight.IsAvailable, Is.True);
                flashlight.capacitySeconds = 100;
                var hud = root.AddComponent<GameHUD>();
                hud.game = game; hud.player = player; hud.interaction = interaction; hud.flashlight = flashlight;
                hud.prompt = Child<TextMeshProUGUI>(root); hud.interactionKey = Child<TextMeshProUGUI>(root);
                hud.crosshair = Child<TextMeshProUGUI>(root); hud.batteryLabel = Child<TextMeshProUGUI>(root);
                hud.batteryStateText = Child<TextMeshProUGUI>(root); hud.notification = Child<TextMeshProUGUI>(root);
                hud.staminaFill = Child<UnityEngine.UI.Image>(root); hud.batteryFill = Child<UnityEngine.UI.Image>(root);
                hud.staminaGroup = Child<CanvasGroup>(root); hud.batteryGroup = Child<CanvasGroup>(root);
                hud.interactionGroup = Child<CanvasGroup>(root); hud.objectiveGroup = Child<CanvasGroup>(root);
                hud.subtitleGroup = Child<CanvasGroup>(root);
                var step = (Action)typeof(GameHUD).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .CreateDelegate(typeof(Action), hud);
                var door = Child<InteractiveDoor>(root);
                door.displayName = "porta da sala";
                SetTarget(interaction, door);
                SetPrompt(interaction, "[E] " + door.Prompt(interaction));
                flashlight.Restore(50.25f, true);
                step();
                Assert.That(hud.batteryLabel.text, Is.EqualTo("51%"));
                Assert.That(hud.interactionKey.text, Is.EqualTo("E"));
                Assert.That(hud.prompt.text, Is.EqualTo("Abrir porta da sala"));

                door.SetOpen(true, true);
                SetPrompt(interaction, "[E] " + door.Prompt(interaction)); step();
                Assert.That(hud.prompt.text, Is.EqualTo("Fechar porta da sala"));
                door.SetLocked(true);
                SetPrompt(interaction, "[E] " + door.Prompt(interaction)); step();
                Assert.That(hud.prompt.text, Is.EqualTo("Destrancar porta da sala"));
                Assert.That(interaction.Target, Is.SameAs(door));

                flashlight.Restore(49.25f, false); step();
                Assert.That(hud.batteryLabel.text, Is.EqualTo("50%"));
                Assert.That(hud.batteryStateText.text, Is.EqualTo("LANTERNA DESLIGADA"));
                flashlight.Restore(0, false); step();
                Assert.That(hud.batteryLabel.text, Is.EqualTo("0%"));
                Assert.That(hud.batteryStateText.text, Is.EqualTo("SEM CARGA — PROCURE PILHAS [E]"));
                flashlight.Restore(49.25f, true); step();
                Assert.That(hud.batteryStateText.text, Is.EqualTo("LANTERNA LIGADA"));

                SetPrompt(interaction, "[E / G] Soltar  ·  [clique / botão direito] Arremessar"); step();
                Assert.That(hud.interactionKey.text, Is.EqualTo("E / G"));
                Assert.That(hud.interactionKey.fontSize, Is.EqualTo(14));
                Assert.That(hud.prompt.text, Is.EqualTo("Soltar  ·  clique Arremessar"));
                var key = hud.interactionKey;
                hud.interactionKey = null; step();
                Assert.That(hud.prompt.text, Is.EqualTo(interaction.CurrentPrompt));
                hud.interactionKey = key; step();
                Assert.That(hud.prompt.text, Is.EqualTo("Soltar  ·  clique Arremessar"));

                hud.interactionGroup.alpha = 1;
                typeof(GameSession).GetProperty(nameof(GameSession.State)).SetValue(game, GameState.Paused);
                step();
                Assert.That(hud.prompt.text, Is.Empty);
                Assert.That(hud.interactionKey.text, Is.Empty);
                Assert.That(hud.crosshair.gameObject.activeSelf, Is.False);
                Assert.That(hud.interactionGroup.alpha, Is.EqualTo(Mathf.MoveTowards(1, 0, Time.unscaledDeltaTime * 8)).Within(.00001f));
                typeof(GameSession).GetProperty(nameof(GameSession.State)).SetValue(game, GameState.Playing);
                step();
                Assert.That(hud.prompt.text, Is.EqualTo("Soltar  ·  clique Arremessar"));
                Assert.That(hud.crosshair.gameObject.activeSelf, Is.True);

                player.RestoreStamina(player.staminaCapacity * .25f);
                hud.interactionGroup.alpha = hud.staminaGroup.alpha = 0;
                hud.crosshair.color = Color.clear; hud.crosshair.fontSize = 18;
                float dt = Time.unscaledDeltaTime;
                step();
                Assert.That(hud.staminaFill.fillAmount, Is.EqualTo(.25f).Within(.00001f));
                Assert.That(hud.interactionGroup.alpha, Is.EqualTo(Mathf.MoveTowards(0, 1, dt * 8)).Within(.00001f));
                Assert.That(hud.staminaGroup.alpha, Is.EqualTo(Mathf.MoveTowards(0, 1, dt * 4)).Within(.00001f));
                Assert.That(hud.crosshair.fontSize, Is.EqualTo(Mathf.Lerp(18, 22, 1 - Mathf.Exp(-dt * 12))).Within(.00001f));
                var aim = Color.Lerp(Color.clear, new Color(.83f, .70f, .51f, .94f), 1 - Mathf.Exp(-dt * 12));
                Assert.That(hud.crosshair.color.r, Is.EqualTo(aim.r).Within(.00001f));
                SetTarget(interaction, null);
                hud.crosshair.fontSize = 22; step();
                Assert.That(hud.crosshair.text, Is.EqualTo("·"));
                Assert.That(hud.crosshair.fontSize, Is.EqualTo(Mathf.Lerp(22, 18, 1 - Mathf.Exp(-dt * 12))).Within(.00001f));

                for (int i = 0; i < 1000; i++) step();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) step();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero, "Unchanged prompt and integer battery percentage reuse their formatted strings.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(lampRoot); }
            yield return null;
        }

        static T Child<T>(GameObject root) where T : Component
        {
            var child = new GameObject(typeof(T).Name);
            child.transform.SetParent(root.transform, false);
            return child.AddComponent<T>();
        }
        static void SetPrompt(PlayerInteraction interaction, string text) =>
            typeof(PlayerInteraction).GetProperty(nameof(PlayerInteraction.CurrentPrompt)).SetValue(interaction, text);
        static void SetTarget(PlayerInteraction interaction, IInteractable target) =>
            typeof(PlayerInteraction).GetProperty(nameof(PlayerInteraction.Target)).SetValue(interaction, target);
    }
}
