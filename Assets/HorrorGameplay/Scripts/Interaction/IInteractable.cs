// Define o contrato comum de uma interação, como mostrar a dica e executar a ação.

namespace HorrorGame
{
    public interface IInteractable
    {
        string Prompt(PlayerInteraction player);
        bool CanInteract(PlayerInteraction player);
        void Interact(PlayerInteraction player);
    }
}
