// Fornece os dados da câmera usados na apresentação do pulso.

using UnityEngine;
namespace HorrorGame
{
 public sealed class WhistleSenseCamera:MonoBehaviour
 {
  public WhistleSystem owner;
  public bool Active=>owner&&owner.SupernaturalActive&&owner.VisualAge>0&&owner.CanUse&&!PresentationSettings.ReduceEffects;
 }
}
