// Permite arrastar uma janela de conversa.

using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorGame
{
    public sealed class PhoneWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PhoneMessagesView view;
        public void OnBeginDrag(PointerEventData e)=>view.BeginWindowDrag(e);
        public void OnDrag(PointerEventData e)=>view.DragWindow(e);
        public void OnEndDrag(PointerEventData e)=>view.EndWindowDrag();
        void OnDisable(){if(view)view.EndWindowDrag();}
    }
}
