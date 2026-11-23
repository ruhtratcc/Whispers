// Mantém os objetivos da escola disponíveis para consulta.

using System.Linq;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>School instance only: keep the current objective available with the existing I notebook.</summary>
    [DefaultExecutionOrder(320)]
    public sealed class SchoolObjectiveJournal : MonoBehaviour
    {
        public GameSession game;
        void LateUpdate()
        {
            if (!game || !game.narrative || (game.narrative.Progress.chapterId != "school" && game.narrative.Progress.chapterId != "left-behind"
                && game.narrative.Progress.chapterId != "memories" && game.narrative.Progress.chapterId != "terrace"
                && game.narrative.Progress.chapterId != "last-exit")
                || !game.hud || !game.hud.inventoryPanel || !game.hud.inventoryPanel.activeSelf) return;
            var inventory = game.interaction.inventory.items;
            string notes = inventory.Count == 0 ? "Ainda não há anotações."
                : string.Join("\n\n", inventory.Select(item => item.title + (item.note ? "\n" + item.text : "")));
            game.hud.inventoryText.text = "OBJETIVO\n" + game.hud.objective.text + "\n\nANOTAÇÕES E EQUIPAMENTO\n" + notes;
        }
    }
}
