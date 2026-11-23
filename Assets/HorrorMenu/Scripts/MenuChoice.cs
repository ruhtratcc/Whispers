// Implementa o comportamento de seleção dos botões do menu.

using UnityEngine.EventSystems;

namespace SchoolMenu
{
    public sealed class MenuChoice : UnityEngine.UI.Button
    {
        public event System.Action<int> Step;
        public void Change(int direction)
        {
            Step?.Invoke(direction);
        }

        public override void OnMove(AxisEventData data)
        {
            if (IsInteractable() && (data.moveDir == MoveDirection.Left || data.moveDir == MoveDirection.Right))
            {
                Change(data.moveDir == MoveDirection.Left ? -1 : 1);
                data.Use();
            }
            else
                base.OnMove(data);
        }

        protected override void Start()
        {
            base.Start();
            onClick.AddListener(() => Change(1));
        }
    }
}
