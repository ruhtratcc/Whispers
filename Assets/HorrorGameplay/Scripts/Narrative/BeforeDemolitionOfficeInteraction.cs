// Liga portas e objetos do escritório às ações narrativas.

using UnityEngine;

namespace HorrorGame
{
    public enum BeforeDemolitionOfficeAction { Computer, SecondDrawer, Whistle, Exit, Closet }

    public sealed class BeforeDemolitionOfficeInteraction : MonoBehaviour, IInteractable
    {
        public BeforeDemolitionOfficeFlow flow;
        public BeforeDemolitionOfficeAction action;

        public string Prompt(PlayerInteraction player)
        {
            switch (action)
            {
                case BeforeDemolitionOfficeAction.Computer: return "Conferir emails no computador";
                case BeforeDemolitionOfficeAction.SecondDrawer:
                    return !flow || !flow.DrawerUnlocked ? "Examinar segunda gaveta — trancada"
                        : flow.secondDrawer && flow.secondDrawer.IsOpen ? "Fechar segunda gaveta" : "Abrir segunda gaveta";
                case BeforeDemolitionOfficeAction.Whistle: return "Examinar apito";
                case BeforeDemolitionOfficeAction.Exit: return "Sair do escritório";
                case BeforeDemolitionOfficeAction.Closet: return "Examinar armário";
                default: return "";
            }
        }

        public bool CanInteract(PlayerInteraction player)
        {
            if (!isActiveAndEnabled || !flow || !flow.IsActiveOffice || !player) return false;
            if (action == BeforeDemolitionOfficeAction.Whistle) return flow.CanCollectWhistle;
            if (!flow.game || !flow.game.CanControl) return false;
            return action != BeforeDemolitionOfficeAction.Exit || !flow.director.HasSignal(BeforeDemolitionCampaign.ExitSignal);
        }

        public void Interact(PlayerInteraction player)
        {
            if (!CanInteract(player)) return;
            switch (action)
            {
                case BeforeDemolitionOfficeAction.Computer:
                    var desktop=flow.game.GetComponent<HorrorGame.Desktop.RetroComputer>();
                    if(desktop)desktop.Open();else flow.OpenComputer();
                    break;
                case BeforeDemolitionOfficeAction.SecondDrawer: flow.ToggleDrawer(player); break;
                case BeforeDemolitionOfficeAction.Whistle:
                    if (!InspectionManager.TryInspect(this, player)) flow.CollectWhistle(player);
                    break;
                case BeforeDemolitionOfficeAction.Exit: flow.TryExit(player); break;
                case BeforeDemolitionOfficeAction.Closet: flow.InspectCloset(player); break;
            }
        }
    }
}
