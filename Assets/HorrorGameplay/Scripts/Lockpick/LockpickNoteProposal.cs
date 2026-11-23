// Organiza o texto de apoio sobre fechaduras e o grampo.

using UnityEngine;
namespace HorrorGame{
 [CreateAssetMenu(menuName="Whispers/Lockpick note proposal")]
 public sealed class LockpickNoteProposal:ScriptableObject{
  public string status="PROPOSTA — APROVAR",title;
  [TextArea]public string text,context;
  public bool requiresNewFact=true;
 }
}
