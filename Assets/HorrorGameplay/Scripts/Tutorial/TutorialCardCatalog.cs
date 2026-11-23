// Reúne os cartões de tutorial pelo ID.

using UnityEngine;using TMPro;
namespace HorrorGame {
 [CreateAssetMenu(menuName="Whispers/Tutorial catalog")]
 public sealed class TutorialCardCatalog:ScriptableObject {
  public TutorialCardData[] cards=System.Array.Empty<TutorialCardData>();public TMP_FontAsset font;
  public TutorialCardData Find(string id){foreach(var card in cards)if(card&&card.id==id)return card;return null;}
 }
}
